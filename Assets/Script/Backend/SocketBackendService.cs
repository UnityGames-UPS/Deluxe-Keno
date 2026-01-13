using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Best.SocketIO;
using Best.SocketIO.Events;

public class SocketBackendService : IBackendService
{
    private const float PING_INTERVAL = 2f;
    private const float PONG_TIMEOUT = 3f;
    private const int MAX_MISSED_PONGS = 5;
    private const int MAX_RECONNECT_ATTEMPTS = 5;
    private const float RECONNECT_DELAY = 2f;

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

        float timeout = 10f;
        float elapsed = 0f;

        while (!JSBridge.HasAuthToken && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (!JSBridge.HasAuthToken)
        {
            GameLogger.LogConnectionError("Failed to receive auth token from JavaScript");
            GameEvents.TriggerConnectionError("Authentication failed");
            yield break;
        }

        GameLogger.LogConnection("Auth token received successfully");
        InitializeWithToken(JSBridge.AuthToken);
    }

    private void InitializeWithToken(string token)
    {
        SocketOptions options = new SocketOptions
        {
            AutoConnect = false,
            Reconnection = false,
            Timeout = TimeSpan.FromSeconds(5),
            ConnectWith = Best.SocketIO.Transports.TransportTypes.WebSocket,
            Auth = (manager, socket) => new { token = token }
        };

        _manager = new SocketManager(new Uri(_serverURL), options);
        _socket = string.IsNullOrEmpty(_namespace) ? _manager.Socket : _manager.GetSocket($"/{_namespace}");

        SetupEventListeners();
        _manager.Open();

        GameLogger.LogConnection($"Connecting to {_serverURL} with namespace: {_namespace}");
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

        if (_hasEverConnected)
            GameEvents.TriggerConnectionRestored();

        _hasEverConnected = true;
        ResetHeartbeat();
        StartHeartbeat();
    }

    private void OnDisconnected()
    {
        GameLogger.LogConnectionWarning("Disconnected from server");

        _isConnected = false;
        StopHeartbeat();
        GameEvents.TriggerConnectionLost();
        AttemptReconnection();
    }

    private void OnError(Error error)
    {
        GameLogger.LogConnectionError($"Socket Error: {error}");
        GameEvents.TriggerConnectionError($"Connection error: {error}");
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

    private void OnPongReceived(string data)
    {
        GameLogger.LogConnection($"Pong received - Latency: {(Time.time - _lastPongTime) * 1000:F0}ms");

        _waitingForPong = false;
        _missedPongs = 0;
        _lastPongTime = Time.time;

        if (_hasEverConnected)
            GameEvents.TriggerConnectionRestored();
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

    private IEnumerator HeartbeatLoop()
    {
        while (true)
        {
            if (_waitingForPong)
            {
                _missedPongs++;

                if (_missedPongs == 2)
                {
                    GameLogger.LogConnectionWarning("Connection unstable - missed pongs");
                    GameEvents.TriggerConnectionUnstable();
                }

                if (_missedPongs >= MAX_MISSED_PONGS)
                {
                    GameLogger.LogConnectionError($"Max missed pongs ({MAX_MISSED_PONGS}) - connection lost");
                    _isConnected = false;
                    GameEvents.TriggerConnectionLost();
                    yield break;
                }

                GameLogger.LogConnectionWarning($"Missed pong #{_missedPongs}/{MAX_MISSED_PONGS}");
            }

            _waitingForPong = true;
            _lastPongTime = Time.time;

            GameLogger.LogServerSend("ping");
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

        if (_manager != null && !_isConnected)
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
        else
        {
            GameLogger.LogConnectionWarning($"Cannot emit '{eventName}' - socket not connected");
        }
    }
}