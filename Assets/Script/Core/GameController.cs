using System.Collections;
using UnityEngine;

public class GameController : MonoBehaviour
{
    #region Singleton
    private static GameController _instance;
    public static GameController Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<GameController>();
            return _instance;
        }
    }
    #endregion

    #region Configuration
    [Header("Backend Configuration")]
    [SerializeField] private bool _useDummyBackend = false;
    [SerializeField] private string _serverURL = "https://devrealtime.dingdinghouse.com/";
    [SerializeField] private string _namespace = "playground";
    [SerializeField] private string _gameID = "KN-test";
    [SerializeField] private string _editorTestToken = "your_test_token_here";
    #endregion

    #region Dependencies
    private GameModel _model;
    private IBackendService _backendService;
    private bool _isInitialized;
    private Coroutine _autoPlayCoroutine;
    #endregion

    #region Public Accessors
    public GameModel GetModel() => _model;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeServices();
    }

    private void OnDestroy()
    {
        if (_instance == this)
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

        GameEvents.TriggerBalanceUpdated(_model.PlayerData.balance);
        GameEvents.TriggerBetChanged(_model.PlayerData.currentBet);
        GameEvents.TriggerWinAmountUpdated(0f);

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
        GameEvents.OnConnectionError += HandleConnectionError;
        GameEvents.OnAnotherDeviceLogin += HandleAnotherDeviceLogin;
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
        GameEvents.OnConnectionError -= HandleConnectionError;
        GameEvents.OnAnotherDeviceLogin -= HandleAnotherDeviceLogin;
    }
    #endregion

    #region Number Selection Handlers
    private void HandleNumberSelected(int number)
    {
        if (!_isInitialized || _model.GameState.isPlaying) return;

        if (_model.CanSelectNumber(number))
        {
            _model.SelectNumber(number);
            GameEvents.TriggerPaytableUpdate(_model.PlayerData.selectedNumbers.Count);
        }
        else if (_model.PlayerData.selectedNumbers.Count >= _model.InitData.maximumPicks)
        {
            ErrorPopupManager.ShowError(ErrorMessages.TOO_MANY_NUMBERS);
        }
    }

    private void HandleNumberDeselected(int number)
    {
        if (!_isInitialized || _model.GameState.isPlaying) return;

        _model.DeselectNumber(number);
        GameEvents.TriggerPaytableUpdate(_model.PlayerData.selectedNumbers.Count);
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
            _autoPlayCoroutine = StartCoroutine(AutoPlayNextRound());
    }
    #endregion

    #region Connection Handlers
    private void HandleConnectionLost()
    {
        if (_model.PlayerData.isAutoPlayActive)
            GameEvents.TriggerAutoPlayToggled(false);

        ErrorPopupManager.ShowError(ErrorMessages.CONNECTION_LOST);
    }

    private void HandleConnectionRestored()
    {
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
        yield return new WaitForSeconds(0.5f);

        if (_model.PlayerData.isAutoPlayActive && _model.CanPlay() && _backendService.IsConnected)
        {
            StartGame();
        }
        else
        {
            if (_model.PlayerData.balance < _model.PlayerData.currentBet)
                ErrorPopupManager.ShowError(ErrorMessages.INSUFFICIENT_BALANCE);
            else if (!_backendService.IsConnected)
                ErrorPopupManager.ShowError(ErrorMessages.CONNECTION_LOST);

            GameEvents.TriggerAutoPlayToggled(false);
        }

        _autoPlayCoroutine = null;
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

        _backendService?.Close();
    }

    private IEnumerator ExitAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        JSBridge.NotifyGameExit();
    }
    #endregion
}
