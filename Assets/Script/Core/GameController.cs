using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class GameController : MonoBehaviour
{
    #region Singleton
    internal static GameController Instance;
 
    #endregion

    #region Configuration
    [Header("Backend Configuration")]
    [SerializeField] private bool _useDummyBackend = false;
    [SerializeField] private string _serverURL = "https://devrealtime.dingdinghouse.com/";
    [SerializeField] private string _namespace = "playground";
    [SerializeField] private string _gameID = "KN-test";
    [SerializeField] private string _editorTestToken = "your_test_token_here";

    [Header("UI Elements")]
    [SerializeField] private UIController uiController;
    [SerializeField] private GameObject _raycastBlocker;
    #endregion

    #region Dependencies
    private GameModel _model;
    private IBackendService _backendService;
    private bool _isInitialized;
    private Coroutine _autoPlayCoroutine;
    #endregion

    #region Public Accessors & WebGL Receivers
    public GameModel GetModel() => _model;
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// WebGL Platform receiver for SendMessage("SocketManager", "ReceiveAuthToken", jsonData)
    /// </summary>
    public void ReceiveAuthToken(string jsonData)
    {
        Debug.Log($"[GameController] ReceiveAuthToken received on SocketManager: {jsonData}");
        if (JSBridge.Instance != null)
        {
            JSBridge.Instance.ReceiveAuthToken(jsonData);
        }
        else
        {
            try
            {
                AuthTokenData data = JsonUtility.FromJson<AuthTokenData>(jsonData);
                JSBridge.SetAuthTokenData(data.cookie, data.socketURL, data.nameSpace);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameController] Failed to parse ReceiveAuthToken: {ex.Message}");
            }
        }
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        Instance = this;

        EnableRaycastBlocker();
    }

    void Start()
    {
        InitializeServices();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Cleanup();
    }
    #endregion

    #region Initialization
    private void InitializeServices()
    {
        _model = new GameModel();
        _backendService = _useDummyBackend
            ? (IBackendService)new DummyBackendService()
            : new SocketBackendService(_serverURL, _namespace, _gameID, _editorTestToken);

        SubscribeToEvents();
        _backendService.Initialize(OnGameInitialized);
    }

    private void OnGameInitialized(GameInitData initData)
    {
        _model.Initialize(initData);
        _isInitialized = true;

        StartCoroutine(InitializeUIAfterDelay(initData));
    }

    private IEnumerator InitializeUIAfterDelay(GameInitData initData)
    {
        yield return new WaitForSeconds(0.5f);

        StartCoroutine(uiController.WaitForGameControllerAndInitialize());

        GameEvents.TriggerBalanceUpdated(_model.PlayerData.balance);
        GameEvents.TriggerBetChanged(_model.PlayerData.currentBet);
        GameEvents.TriggerWinAmountUpdated(0f);

        DisableRaycastBlocker();
        JSBridge.NotifyGameEntered();
    }
    #endregion

    #region Event Management
    private void SubscribeToEvents()
    {
        GameEvents.OnNumberSelected += HandleNumberSelected;
        GameEvents.OnNumberDeselected += HandleNumberDeselected;
        GameEvents.OnAllNumbersCleared += HandleClearAll;
        GameEvents.OnQuickPickSelected += HandleQuickPick;
        GameEvents.OnBetChanged += HandleBetChanged;
        GameEvents.OnAutoBetRoundsChanged += HandleAutoBetRoundsChanged;
        GameEvents.OnAutoPlayToggled += HandleAutoPlayToggled;
        GameEvents.OnPlayButtonClicked += HandlePlayButtonClicked;
        GameEvents.OnSpeedModeChanged += HandleSpeedModeChanged;
        GameEvents.OnAnimationCompleted += HandleAnimationCompleted;
        GameEvents.OnConnectionLost += HandleConnectionLost;
        GameEvents.OnConnectionRestored += HandleConnectionRestored;
        GameEvents.OnConnectionUnstable += HandleConnectionUnstable;
        GameEvents.OnConnectionError += HandleConnectionError;
        GameEvents.OnAnotherDeviceLogin += HandleAnotherDeviceLogin;
        GameEvents.OnGameExit += HandleGameExit;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnNumberSelected -= HandleNumberSelected;
        GameEvents.OnNumberDeselected -= HandleNumberDeselected;
        GameEvents.OnAllNumbersCleared -= HandleClearAll;
        GameEvents.OnQuickPickSelected -= HandleQuickPick;
        GameEvents.OnBetChanged -= HandleBetChanged;
        GameEvents.OnAutoBetRoundsChanged -= HandleAutoBetRoundsChanged;
        GameEvents.OnAutoPlayToggled -= HandleAutoPlayToggled;
        GameEvents.OnPlayButtonClicked -= HandlePlayButtonClicked;
        GameEvents.OnSpeedModeChanged -= HandleSpeedModeChanged;
        GameEvents.OnAnimationCompleted -= HandleAnimationCompleted;
        GameEvents.OnConnectionLost -= HandleConnectionLost;
        GameEvents.OnConnectionRestored -= HandleConnectionRestored;
        GameEvents.OnConnectionUnstable -= HandleConnectionUnstable;
        GameEvents.OnConnectionError -= HandleConnectionError;
        GameEvents.OnAnotherDeviceLogin -= HandleAnotherDeviceLogin;
        GameEvents.OnGameExit -= HandleGameExit;
    }
    #endregion

    #region Platform & Focus Management (Check 4)
    private bool _hasFocus = true;
    private float _focusLostTime = 0f;
    private Coroutine _focusCheckRoutine;
    private float _maxBackgroundTime = 60f;

    private void OnApplicationFocus(bool focus)
    {
        HandleFocusChange(focus);
    }

    public void OnFocusChanged(string value)
    {
        bool focused = value == "1";
        HandleFocusChange(focused);
    }

    public void HandleFocusChange(bool focus)
    {
        _hasFocus = focus;
        AudioManager.Instance?.SetMuteAll(!focus);
        _backendService?.HandleFocusChange(focus);

        if (!focus)
        {
            if (_focusLostTime <= 0f)
            {
                _focusLostTime = Time.unscaledTime;
            }
            if (_focusCheckRoutine == null)
            {
                _focusCheckRoutine = StartCoroutine(FocusTimeoutCheck());
            }
        }
        else
        {
            float elapsed = _focusLostTime > 0f ? Time.unscaledTime - _focusLostTime : 0f;

            if (_focusLostTime > 0f && elapsed >= _maxBackgroundTime)
            {
                TriggerBackgroundTimeout();
                return;
            }

            _focusLostTime = 0f;
            if (_focusCheckRoutine != null)
            {
                StopCoroutine(_focusCheckRoutine);
                _focusCheckRoutine = null;
            }
        }
    }

    private IEnumerator FocusTimeoutCheck()
    {
        while (!_hasFocus && !CoroutineRunner.IsQuitting)
        {
            float elapsed = _focusLostTime > 0f ? Time.unscaledTime - _focusLostTime : 0f;

            if (_focusLostTime > 0f && elapsed >= _maxBackgroundTime)
            {
                TriggerBackgroundTimeout();
                yield break;
            }

            yield return new WaitForSecondsRealtime(1f);
        }

        _focusCheckRoutine = null;
    }

    private void TriggerBackgroundTimeout()
    {
        if (_focusCheckRoutine != null)
        {
            StopCoroutine(_focusCheckRoutine);
            _focusCheckRoutine = null;
        }

        _focusLostTime = 0f;

        if (_backendService != null)
        {
            _backendService.Close();
        }

        GameEvents.TriggerConnectionLost();
    }


    public void CloseSocket()
    {
        StartCoroutine(CloseSocketCoroutine());
    }

    private IEnumerator CloseSocketCoroutine()
    {
        EnableRaycastBlocker();

        if (_model != null && _model.PlayerData.isAutoPlayActive)
            GameEvents.TriggerAutoPlayToggled(false);

        if (_backendService != null)
            _backendService.Close();

        yield return new WaitForSeconds(0.5f);

#if UNITY_WEBGL && !UNITY_EDITOR
        JSBridge.SendMessage("OnExit");
#endif
    }
    #endregion

    #region Connection Handlers
    private void HandleConnectionLost()
    {
        if (_model.PlayerData.isAutoPlayActive)
            GameEvents.TriggerAutoPlayToggled(false);
    }

    private void HandleConnectionRestored() { }

    private void HandleConnectionUnstable()
    {
        if (_model.PlayerData.isAutoPlayActive)
            GameEvents.TriggerAutoPlayToggled(false);
    }

    private void HandleConnectionError(string message)
    {
        ErrorPopupManager.ShowError($"Connection error: {message}");
    }

    private void HandleAnotherDeviceLogin()
    {
        ErrorPopupManager.ShowError("Your account has been logged in from another device");
        StartCoroutine(ExitAfterDelay(2f));
    }
    #endregion

    #region Game Exit Handling
    private void HandleGameExit()
    {
        CloseSocket();
    }

    private IEnumerator ExitAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        CloseSocket();
    }
    #endregion

    #region Number Selection Handlers
    private void HandleNumberSelected(int number)
    {
        if (!_isInitialized || _model.GameState.isPlaying) return;

        // Check if player already has max selections
        if (_model.PlayerData.selectedNumbers.Count >= _model.InitData.maximumPicks &&
            !_model.PlayerData.selectedNumbers.Contains(number))
        {
            // Show error - do NOT select the button
            ErrorPopupManager.ShowError(ErrorMessages.TOO_MANY_NUMBERS);
            return; // Exit without selecting
        }

        // Check if selection is valid
        if (_model.CanSelectNumber(number))
        {
            // Valid selection - update model
            _model.SelectNumber(number);

            // Tell the specific button to visually select itself
            TriggerButtonSelection(number, true);

            GameEvents.TriggerPaytableUpdate(_model.PlayerData.selectedNumbers.Count);
        }
    }

    private void HandleNumberDeselected(int number)
    {
        if (!_isInitialized || _model.GameState.isPlaying) return;

        // Update model
        _model.DeselectNumber(number);

        // Tell the specific button to visually deselect itself
        TriggerButtonSelection(number, false);

        GameEvents.TriggerPaytableUpdate(_model.PlayerData.selectedNumbers.Count);
    }

    // Helper method to trigger button visual state changes
    private void TriggerButtonSelection(int number, bool select)
    {
        NumbersGridView gridView = FindObjectOfType<NumbersGridView>();
        if (gridView != null)
        {
            // Trigger event to update the specific button's visual state
            if (select)
                GameEvents.TriggerNumberVisualUpdate(number, true);
            else
                GameEvents.TriggerNumberVisualUpdate(number, false);
        }
    }

    private void HandleClearAll()
    {
        if (!_isInitialized || _model.GameState.isPlaying) return;

        _model.ClearSelectedNumbers();
        GameEvents.TriggerPaytableUpdate(0);
    }

    private void HandleQuickPick(System.Collections.Generic.List<int> numbers)
    {
        if (!_isInitialized || _model.GameState.isPlaying) return;

        _model.ClearSelectedNumbers();
        foreach (int num in numbers)
            _model.SelectNumber(num);

        GameEvents.TriggerPaytableUpdate(_model.PlayerData.selectedNumbers.Count);
    }
    #endregion
   
    #region Betting Handlers
    private void HandleBetChanged(float bet)
    {
        if (!_isInitialized || _model.GameState.isPlaying) return;
        _model.SetBet(bet);
    }

    private void HandleAutoBetRoundsChanged(int rounds)
    {
        if (!_isInitialized) return;
        _model.SetAutoPlayRounds(rounds);
    }

    private void HandleAutoPlayToggled(bool isActive)
    {
        if (!_isInitialized) return;

        _model.SetAutoPlay(isActive);
        if (!isActive && _autoPlayCoroutine != null)
        {
            StopCoroutine(_autoPlayCoroutine);
            _autoPlayCoroutine = null;
        }
    }
    #endregion

    #region Game Flow Handlers
    private void HandlePlayButtonClicked()
    {
        if (ValidateGameStart())
            StartGame();
    }

    private bool ValidateGameStart()
    {
        if (!_isInitialized)
        {
            ErrorPopupManager.ShowError(ErrorMessages.SERVER_ERROR);
            return false;
        }

        if (!_backendService.IsConnected)
        {
            ErrorPopupManager.ShowError(ErrorMessages.CONNECTION_LOST);
            return false;
        }

        if (_model.PlayerData.selectedNumbers.Count < GameConfig.MIN_NUMBERS)
        {
            ErrorPopupManager.ShowError(ErrorMessages.NO_NUMBERS_SELECTED);
            return false;
        }

        if (_model.PlayerData.balance < _model.PlayerData.currentBet)
        {
            ErrorPopupManager.ShowError(ErrorMessages.INSUFFICIENT_BALANCE);
            return false;
        }

        if (_model.GameState.isPlaying)
        {
            ErrorPopupManager.ShowError(ErrorMessages.GAME_IN_PROGRESS);
            return false;
        }

        return _model.CanPlay();
    }

    private void HandleSpeedModeChanged(SpeedMode mode)
    {
        if (!_isInitialized) return;
        _model.SetSpeedMode(mode);
        
    }

    private void HandleAnimationCompleted()
    {
        GameEvents.TriggerPaytableHighlight();
        EndGame();
        if (_model.PlayerData.isAutoPlayActive && _model.CanPlay())
        {
            _autoPlayCoroutine = StartCoroutine(AutoPlayNextRound());
        }
    }
    #endregion

        #region Game Flow
    private void StartGame()
    {
        _model.StartGame();
        GameEvents.TriggerGameStarted();
        GameEvents.TriggerBalanceUpdated(_model.PlayerData.balance);

        _backendService.SendDrawRequest(
            _model.PlayerData.currentBet,
            new System.Collections.Generic.List<int>(_model.PlayerData.selectedNumbers),
            OnGameResultReceived
        );
    }

    private void OnGameResultReceived(GameResultData result)
    {
        if (!result.success)
        {
            ErrorPopupManager.ShowError(ErrorMessages.SERVER_ERROR);
            _model.EndGame();
            GameEvents.TriggerGameEnded();
            return;
        }

        _model.ProcessResult(result);
        GameEvents.TriggerGameResultReceived(result);
        GameEvents.TriggerBalanceUpdated(_model.PlayerData.balance);
        GameEvents.TriggerWinAmountUpdated(result.currentWinning);
    }

    private void EndGame()
    {
        _model.EndGame();
        GameEvents.TriggerGameEnded();
        GameEvents.TriggerRoundCompleted();
    }

    private IEnumerator AutoPlayNextRound()
    {
    
        yield return new WaitForSecondsRealtime(0.5f);
        if (_model.PlayerData.balance < _model.PlayerData.currentBet)
        { 
            ErrorPopupManager.ShowError(ErrorMessages.INSUFFICIENT_BALANCE);
            GameEvents.TriggerAutoPlayToggled(false);
           
            yield break;
        }
        if (_model.PlayerData.isAutoPlayActive && _model.CanPlay() && _backendService.IsConnected)  
        {
            StartGame();
            
        }
        else
        {
            if (!_backendService.IsConnected)
                ErrorPopupManager.ShowError(ErrorMessages.CONNECTION_LOST);
            GameEvents.TriggerAutoPlayToggled(false);
        }

        _autoPlayCoroutine = null;
    }
    #endregion

    #region Raycast Blocker
    private void EnableRaycastBlocker()
    {
        if (_raycastBlocker != null)
            _raycastBlocker.SetActive(true);
    }

    private void DisableRaycastBlocker()
    {
        if (_raycastBlocker != null)
            _raycastBlocker.SetActive(false);
    }
    #endregion

    #region Cleanup
    private void Cleanup()
    {
        UnsubscribeFromEvents();

        if (_autoPlayCoroutine != null)
        {
            StopCoroutine(_autoPlayCoroutine);
            _autoPlayCoroutine = null;
        }

        if (_backendService != null)
            _backendService.Close();
    }
    #endregion
}