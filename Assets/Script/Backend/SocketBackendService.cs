using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Best.SocketIO;
using Best.SocketIO.Events;

/// <summary>
/// ENHANCED SocketBackendService with:
/// - IsQuitSelf check to prevent reconnection after user quit
/// - Connection state management matching reference game
/// - Proper ping/pong handling for unstable/restored states
/// </summary>
public class SocketBackendService : IBackendService
{
    private const float PING_INTERVAL = 2f;
    private const float PONG_TIMEOUT = 3f;
    private const int MAX_MISSED_PONGS = 5;
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
            GameLogger.LogConnectionError("Cannot send draw request - socket not connected");
            GameEvents.TriggerConnectionError("Connection lost. Please wait...");
            return;
        }

        _onResult = onResult;
        int betIndex = _eventHandler.GetBetIndex(bet);

        if (betIndex == -1)
        {
            GameLogger.LogError($"Invalid bet amount: {bet}");
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
        GameLogger.LogServerSend("request", json);
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

        GameLogger.LogConnection("Socket connection closed");
    }

    private IEnumerator InitializeWithWebGLAuth()
    {
        JSBridge.RequestAuthToken();
        GameLogger.LogConnection("Requesting auth token from JavaScript");

        float elapsed = 0f;

        while (!JSBridge.HasAuthToken && elapsed < AUTH_TOKEN_TIMEOUT)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (!JSBridge.HasAuthToken)
        {
            GameLogger.LogConnectionError("Failed to receive auth token from JavaScript - timeout");
            GameEvents.TriggerConnectionError("Authentication failed - timeout");
            yield break;
        }

        if (string.IsNullOrEmpty(JSBridge.AuthToken) || string.IsNullOrEmpty(JSBridge.SocketURL))
        {
            GameLogger.LogConnectionError("Invalid auth token or socket URL received");
            GameEvents.TriggerConnectionError("Authentication failed - invalid data");
            yield break;
        }

        GameLogger.LogConnection($"Auth token received successfully after {elapsed:F2}s");
        GameLogger.LogConnection($"Socket URL: {JSBridge.SocketURL}");
        GameLogger.LogConnection($"Namespace: {JSBridge.Namespace}");

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

        _socket = string.IsNullOrEmpty(finalNamespace)
            ? _manager.Socket
            : _manager.GetSocket($"/{finalNamespace}");

        SetupEventListeners();
        _manager.Open();

        GameLogger.LogConnection($"Connecting to {finalURL} with namespace: {finalNamespace}");
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
    }

    private void OnConnected(ConnectResponse resp)
    {
        GameLogger.LogConnection("Connected to server successfully");

        _isConnected = true;
        _reconnectAttempts = 0;

        // MATCHING REFERENCE GAME: Close popups if reconnected
        if (_hasEverConnected)
        {
            // Get UIController to close popups
            UIController uiManager = UnityEngine.Object.FindObjectOfType<UIController>();
            if (uiManager != null)
            {
                uiManager.CheckAndClosePopups();
            }

            GameEvents.TriggerConnectionRestored();
        }

        _hasEverConnected = true;
        ResetHeartbeat();
        StartHeartbeat();

        JSBridge.NotifyGameEntered();
    }

    /// <summary>
    /// CRITICAL: OnDisconnected with IsQuitSelf check
    /// MATCHING REFERENCE GAME LOGIC
    /// </summary>
    private void OnDisconnected()
    {
        GameLogger.LogConnectionWarning("Disconnected from server");
        GameLogger.Log("On Disconnected Called");

        _isConnected = false;

        // CRITICAL: Check if user is quitting to prevent showing disconnect popup
        UIController uiManager = UnityEngine.Object.FindObjectOfType<UIController>();
        bool isUserQuitting = (uiManager != null && uiManager.IsQuitSelf);

        if (!isUserQuitting)
        {
            // Only show disconnect popup if NOT user-initiated
            uiManager?.DisconnectionPopup();
            AttemptReconnection();
        }
        else
        {
            GameLogger.LogConnection("User-initiated disconnect (IsQuitSelf=true) - skipping reconnection");
        }

        StopHeartbeat();
    }

    private void OnError(Error error)
    {
        GameLogger.LogConnectionError($"Socket Error: {error}");
        GameEvents.TriggerConnectionError($"Connection error: {error}");

#if UNITY_WEBGL && !UNITY_EDITOR
        JSBridge.SendMessage("error");
#endif
    }

    private void OnGameInit(string data)
    {
        GameLogger.LogServerResponse("game:init", data);

        GameInitData initData = _eventHandler.ParseInitData(data);

        if (initData != null)
        {
            GameLogger.LogConnection("Game initialization data parsed successfully");
            _onInitialized?.Invoke(initData);
        }
        else
        {
            GameLogger.LogConnectionError("Failed to parse game initialization data");
            GameEvents.TriggerConnectionError("Failed to initialize game");
        }
    }

    private void OnResult(string data)
    {
        GameLogger.LogServerResponse("result", data);

        GameResultData resultData = _eventHandler.ParseResultData(data);

        if (resultData != null)
        {
            GameLogger.Log($"Game result parsed - Hits: {resultData.hits.Count}, Win: ${resultData.currentWinning:F2}");
            _onResult?.Invoke(resultData);
            _onResult = null;
        }
        else
        {
            GameLogger.LogConnectionError("Failed to parse game result data");
            ErrorPopupManager.ShowError(ErrorMessages.SERVER_ERROR);
        }
    }

    /// <summary>
    /// MATCHING REFERENCE GAME: Pong received handler
    /// </summary>
    private void OnPongReceived(string data)
    {
        // GameLogger.LogConnection($"Pong received - Latency: {(Time.time - _lastPongTime) * 1000:F0}ms");

        _waitingForPong = false;
        _missedPongs = 0;
        _lastPongTime = Time.time;

        // If we had unstable connection, notify it's restored
        if (_hasEverConnected)
        {
            GameEvents.TriggerConnectionRestored();
        }
    }

    private void OnInternalError(string data)
    {
        GameLogger.LogConnectionError($"Internal server error: {data}");
        ErrorPopupManager.ShowError(ErrorMessages.SERVER_ERROR);
    }

    private void OnAlert(string data)
    {
        GameLogger.LogConnectionWarning($"Server alert: {data}");
    }

    private void OnAnotherDevice(string data)
    {
        GameLogger.LogConnectionError("Account logged in from another device");
        // Note: Reference game doesn't show popup, just logs
        GameEvents.TriggerAnotherDeviceLogin();
        Close();
    }

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

    /// <summary>
    /// MATCHING REFERENCE GAME: Heartbeat with unstable/restored state management
    /// - After 0 missed pongs: Check and close popups (connection stable)
    /// - After 2 missed pongs: Show reconnecting popup (unstable)
    /// - After 5 missed pongs: Show disconnect popup (lost)
    /// </summary>
    private IEnumerator HeartbeatLoop()
    {
        while (true)
        {
            // GameLogger.Log($"PingCheck | waitingForPong: {_waitingForPong}, missedPongs: {_missedPongs}");

            // MATCHING REFERENCE GAME: Check and close popups when connection is stable
            if (_missedPongs == 0)
            {
                UIController uiManager = UnityEngine.Object.FindObjectOfType<UIController>();
                uiManager?.CheckAndClosePopups();
            }

            if (_waitingForPong)
            {
                // MATCHING REFERENCE GAME: After 2 missed pongs, show reconnecting popup
                if (_missedPongs == 2)
                {
                    UIController uiManager = UnityEngine.Object.FindObjectOfType<UIController>();
                    uiManager?.ReconnectionPopup();
                    GameEvents.TriggerConnectionUnstable();
                }

                _missedPongs++;
                GameLogger.LogConnectionWarning($"Pong missed #{_missedPongs}/{MAX_MISSED_PONGS}");

                // After 5 missed pongs - connection lost
                if (_missedPongs >= MAX_MISSED_PONGS)
                {
                    GameLogger.LogConnectionError($"Unable to connect to server � {MAX_MISSED_PONGS} consecutive pongs missed.");
                    _isConnected = false;

                    UIController uiManager = UnityEngine.Object.FindObjectOfType<UIController>();
                    uiManager?.DisconnectionPopup();

                    GameEvents.TriggerConnectionLost();
                    yield break;
                }
            }

            // Send next ping
            _waitingForPong = true;
            _lastPongTime = Time.time;

            // GameLogger.LogServerSend("ping");
            EmitEvent("ping");

            yield return new WaitForSeconds(PING_INTERVAL);
        }
    }

    private void AttemptReconnection()
    {
        if (_reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
        {
            GameLogger.LogConnectionError("Max reconnection attempts reached");
            GameEvents.TriggerConnectionError("Unable to reconnect to server");
            return;
        }

        _reconnectAttempts++;
        GameLogger.LogConnection($"Attempting reconnection {_reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS}");

        CoroutineRunner.Instance.StartCoroutine(ReconnectAfterDelay());
    }

    private IEnumerator ReconnectAfterDelay()
    {
        float delay = RECONNECT_DELAY * _reconnectAttempts;
        GameLogger.LogConnection($"Reconnecting in {delay} seconds...");

        yield return new WaitForSeconds(delay);

        // CRITICAL: Check again if user is quitting before reconnecting
        UIController uiManager = UnityEngine.Object.FindObjectOfType<UIController>();
        bool isUserQuitting = (uiManager != null && uiManager.IsQuitSelf);

        if (_manager != null && !_isConnected && !isUserQuitting)
        {
            _manager.Open();
        }
        else if (isUserQuitting)
        {
            GameLogger.LogConnection("User quit during reconnection delay - aborting reconnection");
        }
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
        else
        {
            GameLogger.LogConnectionWarning($"Cannot emit '{eventName}' - socket not connected");
        }
    }
}
