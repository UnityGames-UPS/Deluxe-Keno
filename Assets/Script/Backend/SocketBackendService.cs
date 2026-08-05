using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Best.SocketIO;
using Best.SocketIO.Events;

public class SocketBackendService : IBackendService
{
    private const float PING_INTERVAL = 2f;
    private const int MAX_MISSED_PONGS = 15;
    private const int MAX_RECONNECT_ATTEMPTS = 5;
    private const float RECONNECT_DELAY = 2f;
    private const float AUTH_TOKEN_TIMEOUT = 20f;

    private readonly string _serverURL;
    private readonly string _namespace;
    private readonly string _gameID;
    private readonly string _editorTestToken;

    private SocketManager _manager;
    private Socket _socket;
    private SocketEventHandler _eventHandler;

    private bool _isConnected;
    private bool _hasEverConnected;
    private int _reconnectAttempts;

    private Coroutine _pingRoutine;
    private float _lastPongTime;
    private bool _waitingForPong;
    private int _missedPongs;

    private Action<GameInitData> _onInitialized;
    private Action<GameResultData> _onResult;

    #region Focus & Background Timeout Fields
    private bool _hasFocus = true;
    private float _focusLostTime = 0f;
    private Coroutine _focusCheckRoutine;
    private float _maxBackgroundTime = 60f;
    #endregion

    public bool IsConnected => _isConnected && _socket != null && _socket.IsOpen;

    public SocketBackendService(string serverURL, string nameSpace, string gameID, string editorToken)
    {
        _serverURL = serverURL;
        _namespace = nameSpace;
        _gameID = gameID;
        _editorTestToken = editorToken;
        _eventHandler = new SocketEventHandler();
    }

    public void Initialize(Action<GameInitData> onInitialized)
    {
        _onInitialized = onInitialized;

#if UNITY_WEBGL && !UNITY_EDITOR
        CoroutineRunner.Instance.StartCoroutine(InitializeWithWebGLAuth());
#else
        InitializeWithToken(_editorTestToken);
#endif
    }

    public void SendDrawRequest(float bet, List<int> picks, Action<GameResultData> onResult)
    {
        if (!IsConnected)
        {
            GameEvents.TriggerConnectionError("Connection lost. Please wait...");
            return;
        }

        _onResult = onResult;
        int betIndex = _eventHandler.GetBetIndex(bet);

        if (betIndex == -1)
        {
            ErrorPopupManager.ShowError(ErrorMessages.INVALID_BET);
            return;
        }

        var request = new DrawRequest
        {
            type = "DRAW",
            payload = new DrawRequestPayload
            {
                betIndex = betIndex,
                picks = picks
            }
        };

        string json = JsonUtility.ToJson(request);
        Debug.Log($"[Socket] Sending request: {json}");
        EmitEvent("request", json);
    }

    public void Close()
    {
        StopHeartbeat();

        if (_manager != null)
        {
            _manager.Close();
            _manager = null;
        }

        _socket = null;
        _isConnected = false;
    }

    private IEnumerator InitializeWithWebGLAuth()
    {
        JSBridge.RequestAuthToken();
        float elapsed = 0f;

        while (!JSBridge.HasAuthToken && elapsed < AUTH_TOKEN_TIMEOUT)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (!JSBridge.HasAuthToken)
        {
            Debug.LogError($"[Socket] Auth token timeout after {AUTH_TOKEN_TIMEOUT}s");
            GameEvents.TriggerConnectionError("Authentication failed - timeout");
            yield break;
        }

        if (string.IsNullOrEmpty(JSBridge.AuthToken) || string.IsNullOrEmpty(JSBridge.SocketURL))
        {
            Debug.LogError("[Socket] Invalid auth data received");
            GameEvents.TriggerConnectionError("Authentication failed - invalid data");
            yield break;
        }

        Debug.Log($"[Socket] Auth received: {JSBridge.SocketURL} / {JSBridge.Namespace}");
        InitializeWithToken(JSBridge.AuthToken, JSBridge.SocketURL, JSBridge.Namespace);
    }

    private void InitializeWithToken(string token, string socketURL = null, string nameSpace = null)
    {
        string finalURL = !string.IsNullOrEmpty(socketURL) ? socketURL : _serverURL;
        string finalNamespace = !string.IsNullOrEmpty(nameSpace) ? nameSpace : _namespace;

        SocketOptions options = new SocketOptions
        {
            AutoConnect = false,
            Reconnection = false,
            Timeout = TimeSpan.FromSeconds(5),
            ConnectWith = Best.SocketIO.Transports.TransportTypes.WebSocket,
            Auth = (manager, socket) => new { token = token }
        };

        _manager = new SocketManager(new Uri(finalURL), options);
        _socket = string.IsNullOrEmpty(finalNamespace) ? _manager.Socket : _manager.GetSocket($"/{finalNamespace}");

        SetupEventListeners();
        _manager.Open();
    }

    private void SetupEventListeners()
    {
        _socket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
        _socket.On(SocketIOEventTypes.Disconnect, OnDisconnected);
        _socket.On<Error>(SocketIOEventTypes.Error, OnError);
        _socket.On<string>("game:init", OnGameInit);
        _socket.On<string>("result", OnResult);
        _socket.On<string>("pong", OnPongReceived);
        _socket.On<string>("internalError", OnInternalError);
        _socket.On<string>("alert", OnAlert);
        _socket.On<string>("AnotherDevice", OnAnotherDevice);
        _socket.On<string>("balance:sync", OnBalanceSync);
    }

    private void OnConnected(ConnectResponse resp)
    {
        _isConnected = true;
        _reconnectAttempts = 0;

        if (_hasEverConnected)
        {
            UIController uiManager = UnityEngine.Object.FindObjectOfType<UIController>();
            uiManager?.CheckAndClosePopups();
            GameEvents.TriggerConnectionRestored();
        }   

        _hasEverConnected = true;
        ResetHeartbeat();
        StartHeartbeat();
        JSBridge.NotifyGameEntered();
    }

    private void OnDisconnected()
    {
        Debug.Log("[Socket] Disconnected");
        _isConnected = false;

        UIController uiManager = UnityEngine.Object.FindObjectOfType<UIController>();
        bool isUserQuitting = (uiManager != null && uiManager.IsQuitSelf);

        if (!isUserQuitting)
        {
            GameEvents.TriggerConnectionLost();
            AttemptReconnection();
        }

        StopHeartbeat();
    }

    private void OnError(Error error)
    {
        Debug.LogError($"[Socket] Error: {error}");
        GameEvents.TriggerConnectionError($"Connection error: {error}");

        if (!string.IsNullOrEmpty(error.message) && error.message.Contains("Session expired"))
          {
            Debug.LogWarning("Session expired detected");
            OnDisconnected();
      #if UNITY_WEBGL && !UNITY_EDITOR
              JSBridge.SendMessage("session_expired");
      #endif
          }
          else
          {
      #if UNITY_WEBGL && !UNITY_EDITOR
              JSBridge.SendMessage("error");
      #endif
          }
    }

    private void OnGameInit(string data)
    {
        Debug.Log($"[Socket] game:init received: {data}");

        GameInitData initData = _eventHandler.ParseInitData(data);
        if (initData != null)
        {
            _onInitialized?.Invoke(initData);
        }
        else
        {
            Debug.LogError("[Socket] Failed to parse init data");
            GameEvents.TriggerConnectionError("Failed to initialize game");
        }
    }

    private void OnResult(string data)
    {
        Debug.Log($"[Socket] result received: {data}");

        GameResultData resultData = _eventHandler.ParseResultData(data);
        if (resultData != null)
        {
            _onResult?.Invoke(resultData);
            _onResult = null;
        }
        else
        {
            Debug.LogError("[Socket] Failed to parse result data");
            ErrorPopupManager.ShowError(ErrorMessages.SERVER_ERROR);
        }
    }

    private void OnPongReceived(string data)
    {
      //  Debug.Log($"[Socket] Pong received - Latency: {(Time.time - _lastPongTime) * 1000:F0}ms");
        _waitingForPong = false;
        _missedPongs = 0;
        _lastPongTime = Time.time;

        if (_hasEverConnected)
            GameEvents.TriggerConnectionRestored();
    }

    private void OnInternalError(string data)
    {
        Debug.LogError($"[Socket] Internal error: {data}");
        ErrorPopupManager.ShowError(ErrorMessages.SERVER_ERROR);
    }

    private void OnAlert(string data)
    {
        Debug.LogWarning($"[Socket] Alert: {data}");
    }

    private void OnAnotherDevice(string data)
    {
        Debug.LogError("[Socket] Another device login");
        GameEvents.TriggerAnotherDeviceLogin();
        Close();
    }

    private void OnBalanceSync(string data)
    {
        try
        {
            BalanceSyncPayload syncPayload = JsonUtility.FromJson<BalanceSyncPayload>(data);
            if (syncPayload == null) return;

            Debug.Log($"[Socket] balance:sync received: {syncPayload.balance}");
            if (GameController.Instance != null && GameController.Instance.GetModel() != null)
            {
                GameController.Instance.GetModel().PlayerData.balance = syncPayload.balance;
            }
            GameEvents.TriggerBalanceUpdated(syncPayload.balance);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Socket] BalanceSync parse error: {e.Message}");
        }
    }

    #region 60-Second Background Timeout (Check 4)
    public void HandleFocusChange(bool focus)
    {
        _hasFocus = focus;

        if (!focus)
        {
            _focusLostTime = Time.time;
            if (_focusCheckRoutine == null)
            {
                _focusCheckRoutine = CoroutineRunner.Instance.StartCoroutine(FocusTimeoutCheck());
            }
        }
        else
        {
            if (_focusCheckRoutine != null)
            {
                CoroutineRunner.Instance.StopCoroutine(_focusCheckRoutine);
                _focusCheckRoutine = null;
            }
        }
    }

    private IEnumerator FocusTimeoutCheck()
    {
        while (!_hasFocus)
        {
            if (Time.time - _focusLostTime >= _maxBackgroundTime)
            {
                Debug.LogWarning("[SOCKET] Background timeout — closing connection");
                _isConnected = false;
                StopHeartbeat();

                Close();

                GameEvents.TriggerConnectionLost();
                _focusCheckRoutine = null;
                yield break;
            }

            yield return new WaitForSecondsRealtime(1f);
        }

        _focusCheckRoutine = null;
    }
    #endregion

    private void StartHeartbeat()
    {
        StopHeartbeat();
        _pingRoutine = CoroutineRunner.Instance.StartCoroutine(HeartbeatLoop());
    }

    private void StopHeartbeat()
    {
        if (_pingRoutine != null)
        {
            CoroutineRunner.Instance.StopCoroutine(_pingRoutine);
            _pingRoutine = null;
        }
    }

    private void ResetHeartbeat()
    {
        _waitingForPong = false;
        _missedPongs = 0;
        _lastPongTime = Time.time;
    }

    private IEnumerator HeartbeatLoop()
    {
        while (true)
        {
            if (_missedPongs == 0)
            {
                UIController uiManager = UnityEngine.Object.FindObjectOfType<UIController>();
                uiManager?.CheckAndClosePopups();
            }

            if (_waitingForPong)
            {
                if (_missedPongs == 2)
                {
                    UIController uiManager = UnityEngine.Object.FindObjectOfType<UIController>();
                    GameEvents.TriggerConnectionUnstable();
                }

                _missedPongs++;
                Debug.LogWarning($"[Socket] Pong missed #{_missedPongs}/{MAX_MISSED_PONGS}");

                if (_missedPongs >= MAX_MISSED_PONGS)
                {
                    Debug.LogError($"[Socket] Connection timeout - {MAX_MISSED_PONGS} pongs missed");
                    _isConnected = false;

                    UIController uiManager = UnityEngine.Object.FindObjectOfType<UIController>();
                    GameEvents.TriggerConnectionLost();
                    yield break;
                }
            }

            _waitingForPong = true;
            _lastPongTime = Time.time;

           // Debug.Log("[Socket] Sending ping");
            EmitEvent("ping");

            yield return new WaitForSeconds(PING_INTERVAL);
        }
    }

    private void AttemptReconnection()
    {
        if (_reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
        {
            Debug.LogError($"[Socket] Max reconnection attempts ({MAX_RECONNECT_ATTEMPTS}) reached");
            GameEvents.TriggerConnectionError("Unable to reconnect to server");
            return;
        }

        _reconnectAttempts++;
        CoroutineRunner.Instance.StartCoroutine(ReconnectAfterDelay());
    }

    private IEnumerator ReconnectAfterDelay()
    {
        float delay = RECONNECT_DELAY * _reconnectAttempts;
        yield return new WaitForSeconds(delay);

        UIController uiManager = UnityEngine.Object.FindObjectOfType<UIController>();
        bool isUserQuitting = (uiManager != null && uiManager.IsQuitSelf);

        if (_manager != null && !_isConnected && !isUserQuitting)
            _manager.Open();
    }

    private void EmitEvent(string eventName, string json = null)
    {
        if (_socket != null && _socket.IsOpen)
        {
            if (!string.IsNullOrEmpty(json))
                _socket.Emit(eventName, json);
            else
                _socket.Emit(eventName);
        }
    }
}
