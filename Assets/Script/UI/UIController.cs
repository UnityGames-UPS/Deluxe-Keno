using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Text;

/// <summary>
/// COMPLETE UIController - Ready for Copy/Paste
/// ENHANCED with connection popup management and quit game logic
/// All existing functions preserved
/// </summary>
public class UIController : MonoBehaviour
{
    #region Serialized Fields - Main Buttons
    [Header("Main Buttons")]
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _pauseButton;
    [SerializeField] private Button _clearButton;
    [SerializeField] private Button _betButton;
    [SerializeField] private GameObject _betButtonSelected;
    [SerializeField] private Button _autoBetButton;
    [SerializeField] private GameObject _autoBetButtonSelected;
    [SerializeField] private GameObject _mainControlContainer;
    [SerializeField] private GameObject _speedControlContainer;
    #endregion

    #region Serialized Fields - Speed Control
    [Header("Speed Control")]
    [SerializeField] private Button _normalSpeedButton;
    [SerializeField] private GameObject _normalSpeedSelected;
    [SerializeField] private Button _turboSpeedButton;
    [SerializeField] private GameObject _turboSpeedSelected;
    [SerializeField] private Button _instantSpeedButton;
    [SerializeField] private GameObject _instantSpeedSelected;

    [Header("Speed Button Text Colors")]
    [SerializeField] private Color _speedNormalColor = Color.white;
    [SerializeField] private Color _speedSelectedColor = Color.yellow;
    #endregion

    #region Serialized Fields - Display Texts
    [Header("Display Texts")]
    [SerializeField] private TextMeshProUGUI _balanceText;
    [SerializeField] private TextMeshProUGUI _betAmountText;
    [SerializeField] private TextMeshProUGUI _winAmountText;
    [SerializeField] private TextMeshProUGUI _autoPlayRoundsText;
    #endregion

    #region NEW - Connection Popups (MATCHING REFERENCE GAME)
    [Header("Disconnection Popup")]
    [SerializeField] private Button _closeDisconnectButton;
    [SerializeField] private GameObject _disconnectPopupObject;

    [Header("Reconnection Popup")]
    [SerializeField] private TextMeshProUGUI _reconnectText;
    [SerializeField] private GameObject _reconnectPopupObject;

    [Header("Main Popup Panel")]
    [SerializeField] private GameObject _mainPopupObject;
    #endregion

    #region NEW - Quit Game Popup (MATCHING REFERENCE GAME)
    [Header("Quit Game Popup")]
    [SerializeField] private GameObject _quitGameObject;
    [SerializeField] private Button _quitGameButton;
    [SerializeField] private Button _yesQuitButton;
    [SerializeField] private Button _noQuitButton;
    #endregion

    #region Serialized Fields - Bet Popup
    [Header("Bet Popup")]
    [SerializeField] private GameObject _betPopupMainPanel;
    [SerializeField] private GameObject _betPopupArea;
    [SerializeField] private Button[] _betButtons = new Button[8];
    [SerializeField] private GameObject[] _betButtonsSelected = new GameObject[8];
    [SerializeField] private Button _betPopupCloseButton;

    [Header("Bet Button Colors")]
    [SerializeField] private Color _betNormalColor = Color.white;
    [SerializeField] private Color _betSelectedColor = Color.yellow;
    #endregion

    #region Serialized Fields - Auto Play Popup
    [Header("Auto Play Popup")]
    [SerializeField] private GameObject _autoBetPopupMainPanel;
    [SerializeField] private GameObject _autoBetPopupArea;
    [SerializeField] private Button[] _autoRoundButtons = new Button[8];
    [SerializeField] private GameObject[] _autoRoundButtonsSelected = new GameObject[8];
    [SerializeField] private Button _startAutoPlayButton;
    [SerializeField] private Button _autoBetPopupCloseButton;

    [Header("Auto Round Button Colors")]
    [SerializeField] private Color _autoNormalColor = Color.white;
    [SerializeField] private Color _autoSelectedColor = Color.yellow;
    #endregion

    #region Serialized Fields - Win Popup
    [Header("Win Popup")]
    [SerializeField] private GameObject _winPopupMainPanel;
    [SerializeField] private GameObject _winPopupArea;
    [SerializeField] private TextMeshProUGUI _winPopupAmountText;
    [SerializeField] private float _winPopupDisplayDuration = 2f;
    #endregion

    #region Serialized Fields - Initialization
    [Header("Initialization")]
    [SerializeField] private string _loadingText = "Loading...";
    #endregion

    #region Private State Variables
    private SpeedMode _currentSpeedMode = SpeedMode.Normal;
    private float _selectedBet;
    private int _selectedAutoRounds;
    private int _remainingAutoRounds;
    private bool _isAutoPlayMode;
    private float _currentWinAmount;
    private Coroutine _winPopupCoroutine;
    private bool _isInitialized;

    // NEW: User quit flag (CRITICAL - must be internal)
    internal bool IsQuitSelf = false;

    // OPTIMIZATION: Cached string builder to reduce GC
    private StringBuilder _stringBuilder = new StringBuilder(32);

    // OPTIMIZATION: Cached format strings
    private const string BALANCE_FORMAT = "{0:F2}";
    private const string BET_FORMAT = "{0:F2}";
    private const string WIN_FORMAT = "{0:F2}";
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        IsQuitSelf = false; // NEW: Reset quit flag

        InitializeUI();
        SubscribeToEvents();
        SetupButtonListeners();

        // Start background music
        AudioManager.Instance.PlayBackgroundMusic();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        RemoveButtonListeners();
        CleanupAnimations();
    }
    #endregion

    #region Initialization
    internal IEnumerator WaitForGameControllerAndInitialize()
    {
        SetLoadingState();

        float timeout = 5f;
        float elapsed = 0f;

        while (GameController.Instance == null || GameController.Instance.GetModel() == null ||
               GameController.Instance.GetModel().InitData == null)
        {
            yield return null;
            elapsed += Time.deltaTime;

            if (elapsed > timeout)
            {
                Debug.LogError("GameController initialization timeout!");
                DisconnectionPopup(); // NEW: Show disconnect on timeout
                yield break;
            }
        }

        yield return new WaitForSeconds(0.1f);

        InitializeFromGameModel();
        _isInitialized = true;
    }

    private void InitializeFromGameModel()
    {
        GameModel model = GameController.Instance.GetModel();

        if (model == null || model.InitData == null)
            return;

        _selectedBet = model.PlayerData.currentBet;
        UpdateBetDisplay(_selectedBet);
        UpdateBalanceDisplay(model.PlayerData.balance);
        UpdateWinDisplay(model.LastWinAmount);
    }

    private void SetLoadingState()
    {
        if (_balanceText != null)
            _balanceText.text = _loadingText;

        if (_betAmountText != null)
            _betAmountText.text = _loadingText;

        if (_winAmountText != null)
            _winAmountText.text = "0.00";
    }

    private void InitializeUI()
    {
        _mainControlContainer?.SetActive(true);
        _speedControlContainer?.SetActive(false);
        _betPopupMainPanel?.SetActive(false);
        _autoBetPopupMainPanel?.SetActive(false);
        _winPopupMainPanel?.SetActive(false);

        // NEW: Initialize connection popups as hidden
        _mainPopupObject?.SetActive(false);
        _reconnectPopupObject?.SetActive(false);
        _disconnectPopupObject?.SetActive(false);
        _quitGameObject?.SetActive(false);

        // Play button always visible and interactable at start
        if (_playButton != null)
        {
            _playButton.gameObject.SetActive(true);
            _playButton.interactable = true;
        }

        if (_pauseButton != null)
            _pauseButton.gameObject.SetActive(false);

        UpdateSpeedButtonStates();

        if (_betButtonSelected != null)
            _betButtonSelected.SetActive(false);

        if (_autoBetButtonSelected != null)
            _autoBetButtonSelected.SetActive(false);

        SetMainControlButtonsInteractable(true);
    }
    #endregion

    #region Button Listeners Setup
    private void SetupButtonListeners()
    {
        // Main control buttons
        _playButton?.onClick.AddListener(OnPlayClicked);
        _pauseButton?.onClick.AddListener(OnPauseClicked);
        _clearButton?.onClick.AddListener(OnClearClicked);
        _betButton?.onClick.AddListener(OnBetClicked);
        _autoBetButton?.onClick.AddListener(OnAutoBetClicked);

        // Speed buttons
        _normalSpeedButton?.onClick.AddListener(() => OnSpeedChanged(SpeedMode.Normal));
        _turboSpeedButton?.onClick.AddListener(() => OnSpeedChanged(SpeedMode.Turbo));
        _instantSpeedButton?.onClick.AddListener(() => OnSpeedChanged(SpeedMode.Instant));

        // Popup buttons
        _betPopupCloseButton?.onClick.AddListener(CloseBetPopup);
        _autoBetPopupCloseButton?.onClick.AddListener(CloseAutoBetPopup);
        _startAutoPlayButton?.onClick.AddListener(OnStartAutoPlayClicked);

        // NEW: Connection popup buttons
        _closeDisconnectButton?.onClick.AddListener(delegate {
            GameController.Instance?.CloseSocket();
        });

        // NEW: Quit game buttons
        _quitGameButton?.onClick.AddListener(OpenQuitGamePopup);
        _yesQuitButton?.onClick.AddListener(QuitGame);
        _noQuitButton?.onClick.AddListener(CloseQuitGamePopup);

        // Background panel click handlers
        if (_betPopupMainPanel != null)
        {
            Button mainPanelBtn = _betPopupMainPanel.GetComponent<Button>();
            if (mainPanelBtn == null) mainPanelBtn = _betPopupMainPanel.AddComponent<Button>();
            mainPanelBtn.onClick.AddListener(CloseBetPopup);
        }

        if (_autoBetPopupMainPanel != null)
        {
            Button mainPanelBtn = _autoBetPopupMainPanel.GetComponent<Button>();
            if (mainPanelBtn == null) mainPanelBtn = _autoBetPopupMainPanel.AddComponent<Button>();
            mainPanelBtn.onClick.AddListener(CloseAutoBetPopup);
        }
    }

    private void RemoveButtonListeners()
    {
        _playButton?.onClick.RemoveAllListeners();
        _pauseButton?.onClick.RemoveAllListeners();
        _clearButton?.onClick.RemoveAllListeners();
        _betButton?.onClick.RemoveAllListeners();
        _autoBetButton?.onClick.RemoveAllListeners();

        _normalSpeedButton?.onClick.RemoveAllListeners();
        _turboSpeedButton?.onClick.RemoveAllListeners();
        _instantSpeedButton?.onClick.RemoveAllListeners();

        _betPopupCloseButton?.onClick.RemoveAllListeners();
        _autoBetPopupCloseButton?.onClick.RemoveAllListeners();
        _startAutoPlayButton?.onClick.RemoveAllListeners();

        // NEW: Remove connection popup listeners
        _quitGameButton?.onClick.RemoveAllListeners();
        _yesQuitButton?.onClick.RemoveAllListeners();
        _noQuitButton?.onClick.RemoveAllListeners();
        _closeDisconnectButton?.onClick.RemoveAllListeners();
    }
    #endregion

    #region Event Subscriptions
    private void SubscribeToEvents()
    {
        GameEvents.OnBalanceUpdated += UpdateBalanceDisplay;
        GameEvents.OnBetChanged += UpdateBetDisplay;
        GameEvents.OnWinAmountUpdated += UpdateWinDisplay;
        GameEvents.OnGameStarted += HandleGameStarted;
        GameEvents.OnGameEnded += HandleGameEnded;
        GameEvents.OnAutoPlayToggled += HandleAutoPlayToggled;
        GameEvents.OnRoundCompleted += HandleRoundCompleted;

        // NEW: Connection event handlers (EXACT FUNCTION NAMES)
        GameEvents.OnConnectionUnstable += ReconnectionPopup;
        GameEvents.OnConnectionLost += HandleConnectionLost;
        GameEvents.OnConnectionRestored += CheckAndClosePopups;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnBalanceUpdated -= UpdateBalanceDisplay;
        GameEvents.OnBetChanged -= UpdateBetDisplay;
        GameEvents.OnWinAmountUpdated -= UpdateWinDisplay;
        GameEvents.OnGameStarted -= HandleGameStarted;
        GameEvents.OnGameEnded -= HandleGameEnded;
        GameEvents.OnAutoPlayToggled -= HandleAutoPlayToggled;
        GameEvents.OnRoundCompleted -= HandleRoundCompleted;

        // NEW: Unsubscribe connection handlers
        GameEvents.OnConnectionUnstable -= ReconnectionPopup;
        GameEvents.OnConnectionLost -= HandleConnectionLost;
        GameEvents.OnConnectionRestored -= CheckAndClosePopups;
    }
    #endregion

    #region NEW - Connection Popup Management (EXACT NAMES FROM REFERENCE GAME)

    /// <summary>
    /// Shows disconnection popup when connection is completely lost
    /// EXACT NAME: DisconnectionPopup (internal)
    /// </summary>
    internal void DisconnectionPopup()
    {
        OpenPopup(_disconnectPopupObject);
    }

    /// <summary>
    /// Shows reconnection popup when connection is unstable (2 missed pongs)
    /// EXACT NAME: ReconnectionPopup (internal)
    /// </summary>
    internal void ReconnectionPopup()
    {
        OpenPopup(_reconnectPopupObject);
    }

    /// <summary>
    /// Closes all connection-related popups (called when connection restored)
    /// EXACT NAME: CheckAndClosePopups (internal)
    /// </summary>
    internal void CheckAndClosePopups()
    {
        if (_reconnectPopupObject != null && _reconnectPopupObject.activeInHierarchy)
        {
            ClosePopup(_reconnectPopupObject);
        }

        if (_disconnectPopupObject != null && _disconnectPopupObject.activeInHierarchy)
        {
            ClosePopup(_disconnectPopupObject);
        }
    }

    private void HandleConnectionLost()
    {
        if (!IsQuitSelf)
        {
            DisconnectionPopup();
        }
    }

    private void ClosePopup(GameObject popup)
    {
        if (popup) popup.SetActive(false);

        // Only close main panel if no other popups are showing
        if (!_disconnectPopupObject.activeSelf && !_reconnectPopupObject.activeSelf)
        {
            if (_mainPopupObject) _mainPopupObject.SetActive(false);
        }
    }

    private void OpenPopup(GameObject popup)
    {
        if (popup) popup.SetActive(true);
        if (_mainPopupObject) _mainPopupObject.SetActive(true);
    }

    #endregion

    #region NEW - Quit Game Management (EXACT NAMES FROM REFERENCE GAME)

    /// <summary>
    /// Opens the quit game confirmation popup
    /// EXACT NAME: OpenQuitGamePopup (private)
    /// </summary>
    private void OpenQuitGamePopup()
    {
        AudioManager.Instance?.PlayButtonClick();
        OpenPopup(_quitGameObject);
    }

    /// <summary>
    /// Closes the quit game popup without quitting
    /// EXACT NAME: CloseQuitGamePopup (private)
    /// </summary>
    private void CloseQuitGamePopup()
    {
        AudioManager.Instance?.PlayButtonClick();
        ClosePopup(_quitGameObject);
    }

    /// <summary>
    /// User confirmed quit - close socket and exit game
    /// EXACT NAME: QuitGame (private)
    /// CRITICAL: Sets IsQuitSelf = true before calling CloseSocket
    /// </summary>
    private void QuitGame()
    {
        IsQuitSelf = true;  // CRITICAL: Mark as user-initiated quit
        AudioManager.Instance?.PlayButtonClick();

        ClosePopup(_quitGameObject);

        // Call GameController's CloseSocket method
        if (GameController.Instance != null)
        {
            GameController.Instance.CloseSocket();
        }
    }

    #endregion

    #region Display Updates
    private void UpdateBalanceDisplay(float balance)
    {
        if (_balanceText != null)
        {
            _stringBuilder.Clear();
            _balanceText.text = balance.ToString("n2");
        }
    }

    private void UpdateBetDisplay(float bet)
    {
        _selectedBet = bet;

        if (_betAmountText != null)
        {
            _stringBuilder.Clear();
            _betAmountText.text = bet.ToString("n2");
        }
    }

    private void UpdateWinDisplay(float winAmount)
    {
        _currentWinAmount = winAmount;

        if (_winAmountText != null)
        {
            _stringBuilder.Clear();
            _winAmountText.text = winAmount.ToString("n2");

            if (winAmount > 0)
                AnimateWinText();
        }
    }
    #endregion

    #region Button Click Handlers
    private void OnPlayClicked()
    {
        if (!_isInitialized) return;

        AudioManager.Instance?.PlayButtonClick();
        AnimateButtonPress(_playButton.gameObject);
        GameEvents.TriggerPlayButtonClicked();
    }

    private void OnPauseClicked()
    {
        if (!_isInitialized) return;

        AudioManager.Instance?.PlayButtonClick();
        AnimateButtonPress(_pauseButton.gameObject);
        GameEvents.TriggerAutoPlayToggled(false);
    }

    private void OnClearClicked()
    {
        if (!_isInitialized) return;

        AudioManager.Instance?.PlayButtonClick();
        AnimateButtonPress(_clearButton.gameObject);
        GameEvents.TriggerAllNumbersCleared();
    }

    private void OnBetClicked()
    {
        if (!_isInitialized) return;

        AudioManager.Instance?.PlayButtonClick();
        AnimateButtonPress(_betButton.gameObject);

        if (_betPopupMainPanel != null)
        {
            _betPopupMainPanel.SetActive(true);
            GameEvents.TriggerBetPopupToggled(true);
        }

        if (_betPopupArea != null)
        {
            _betPopupArea.SetActive(true);
            AnimatePopupShow(_betPopupArea);
        }

        if (_betButtonSelected != null)
            _betButtonSelected.SetActive(true);

        UpdateBetButtons();
    }

    private void OnAutoBetClicked()
    {
        if (!_isInitialized) return;

        AudioManager.Instance?.PlayButtonClick();
        AnimateButtonPress(_autoBetButton.gameObject);

        if (_autoBetPopupMainPanel != null)
        {
            _autoBetPopupMainPanel.SetActive(true);
            GameEvents.TriggerAutoBetPopupToggled(true);
        }

        if (_autoBetPopupArea != null)
        {
            _autoBetPopupArea.SetActive(true);
            AnimatePopupShow(_autoBetPopupArea);
        }

        if (_autoBetButtonSelected != null)
            _autoBetButtonSelected.SetActive(true);

        UpdateAutoRoundButtons();
    }

    private void OnSpeedChanged(SpeedMode mode)
    {
        if (!_isInitialized) return;

        _currentSpeedMode = mode;
        AudioManager.Instance?.PlayButtonClick();
        GameEvents.TriggerSpeedModeChanged(mode);
        UpdateSpeedButtonStates();
    }

    private void OnStartAutoPlayClicked()
    {
        if (!_isInitialized) return;

        AudioManager.Instance?.PlayButtonClick();

        if (_selectedAutoRounds <= 0)
        {
            ErrorPopupManager.ShowError(ErrorMessages.AUTOPLAY_NO_ROUNDS);
            return;
        }

        GameEvents.TriggerAutoBetRoundsChanged(_selectedAutoRounds);
        GameEvents.TriggerAutoPlayToggled(true);
        CloseAutoBetPopupAndStartGame();
    }
    #endregion

    #region Event Handlers
    private void HandleGameStarted()
    {
        if (_isAutoPlayMode)
        {
            if (_playButton != null)
                _playButton.gameObject.SetActive(false);

            if (_pauseButton != null)
                _pauseButton.gameObject.SetActive(true);

            _speedControlContainer?.SetActive(true);
            _mainControlContainer?.SetActive(false);
        }
        else
        {
            if (_playButton != null)
                _playButton.interactable = false;

            _speedControlContainer?.SetActive(true);
        }

        SetMainControlButtonsInteractable(false);
    }

    private void HandleGameEnded()
    {
        if (!_isAutoPlayMode)
        {
            if (_playButton != null)
                _playButton.interactable = true;

            _speedControlContainer?.SetActive(false);
        }

        if (!_isAutoPlayMode)
            SetMainControlButtonsInteractable(true);
    }

    private void HandleAutoPlayToggled(bool isActive)
    {
        _isAutoPlayMode = isActive;
        _remainingAutoRounds = _selectedAutoRounds;

        if (_autoPlayRoundsText != null)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(_remainingAutoRounds.ToString()).Append("/").Append(_selectedAutoRounds.ToString());
            _autoPlayRoundsText.text = _stringBuilder.ToString();
        }

        if (!isActive)
        {
            if (_playButton != null)
            {
                _playButton.gameObject.SetActive(true);
                _playButton.interactable = true;
            }

            if (_pauseButton != null)
                _pauseButton.gameObject.SetActive(false);

            _speedControlContainer?.SetActive(false);
            _mainControlContainer?.SetActive(true);
            SetMainControlButtonsInteractable(true);

            if (_autoBetButtonSelected != null)
                _autoBetButtonSelected.SetActive(false);
        }
    }

    private void HandleRoundCompleted()
    {
        if (_isAutoPlayMode)
        {
            _remainingAutoRounds--;

            if (_autoPlayRoundsText != null)
            {
                _stringBuilder.Clear();
                _stringBuilder.Append(_remainingAutoRounds.ToString()).Append("/").Append(_selectedAutoRounds.ToString());
                _autoPlayRoundsText.text = _stringBuilder.ToString();
            }
        }
    }

    private void SetMainControlButtonsInteractable(bool isActive)
    {
        if (_clearButton != null)
            _clearButton.interactable = isActive;

        if (_betButton != null)
            _betButton.interactable = isActive;

        if (_autoBetButton != null)
            _autoBetButton.interactable = isActive;
    }
    #endregion

    #region Popup Management
    private void CloseBetPopup()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (_betPopupArea != null)
        {
            AnimatePopupHide(_betPopupArea, () => {
                _betPopupMainPanel?.SetActive(false);
                GameEvents.TriggerBetPopupToggled(false);
            });
        }
        else
        {
            _betPopupMainPanel?.SetActive(false);
            GameEvents.TriggerBetPopupToggled(false);
        }

        if (_betButtonSelected != null)
            _betButtonSelected.SetActive(false);
    }

    private void CloseAutoBetPopup()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (_autoBetPopupArea != null)
        {
            AnimatePopupHide(_autoBetPopupArea, () => {
                _autoBetPopupMainPanel?.SetActive(false);
                GameEvents.TriggerAutoBetPopupToggled(false);
            });
        }
        else
        {
            _autoBetPopupMainPanel?.SetActive(false);
        }

        if (!_isAutoPlayMode && _autoBetButtonSelected != null)
            _autoBetButtonSelected.SetActive(false);
    }

    private void CloseAutoBetPopupAndStartGame()
    {
        if (_autoBetPopupArea != null)
        {
            AnimatePopupHide(_autoBetPopupArea, () => {
                _autoBetPopupMainPanel?.SetActive(false);
                GameEvents.TriggerPlayButtonClicked();
            });
        }
        else
        {
            _autoBetPopupMainPanel?.SetActive(false);
            GameEvents.TriggerPlayButtonClicked();
        }
    }

    private void UpdateBetButtons()
    {
        if (GameController.Instance == null || GameController.Instance.GetModel() == null)
            return;

        float[] bets = GameController.Instance.GetModel().InitData.bets;

        for (int i = 0; i < _betButtons.Length && i < bets.Length; i++)
        {
            if (_betButtons[i] != null)
            {
                TextMeshProUGUI btnText = _betButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    _stringBuilder.Clear();
                    _stringBuilder.Append(bets[i].ToString("F2"));
                    btnText.text = _stringBuilder.ToString();
                    btnText.color = Mathf.Approximately(bets[i], _selectedBet) ? _betSelectedColor : _betNormalColor;
                }

                _betButtons[i].onClick.RemoveAllListeners();
                float betValue = bets[i];
                _betButtons[i].onClick.AddListener(() => OnBetSelected(betValue));
                _betButtons[i].gameObject.SetActive(true);

                if (_betButtonsSelected[i] != null)
                    _betButtonsSelected[i].SetActive(Mathf.Approximately(betValue, _selectedBet));
            }
        }

        for (int i = bets.Length; i < _betButtons.Length; i++)
        {
            if (_betButtons[i] != null)
                _betButtons[i].gameObject.SetActive(false);
        }
    }

    private void UpdateAutoRoundButtons()
    {
        int[] rounds = { 3, 5, 10, 15, 25, 50, 75, 100 };

        for (int i = 0; i < _autoRoundButtons.Length && i < rounds.Length; i++)
        {
            if (_autoRoundButtons[i] != null)
            {
                TextMeshProUGUI btnText = _autoRoundButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.text = rounds[i].ToString();
                    btnText.color = (rounds[i] == _selectedAutoRounds) ? _autoSelectedColor : _autoNormalColor;
                }

                _autoRoundButtons[i].onClick.RemoveAllListeners();
                int roundValue = rounds[i];
                _autoRoundButtons[i].onClick.AddListener(() => OnAutoRoundSelected(roundValue));
                _autoRoundButtons[i].gameObject.SetActive(true);

                if (_autoRoundButtonsSelected[i] != null)
                    _autoRoundButtonsSelected[i].SetActive(roundValue == _selectedAutoRounds);
            }
        }

        for (int i = rounds.Length; i < _autoRoundButtons.Length; i++)
        {
            if (_autoRoundButtons[i] != null)
                _autoRoundButtons[i].gameObject.SetActive(false);
        }
    }

    private void OnBetSelected(float bet)
    {
        _selectedBet = bet;
        AudioManager.Instance.PlayButtonClick();
        GameEvents.TriggerBetChanged(bet);
        UpdateBetButtons();
        CloseBetPopup();
    }

    private void OnAutoRoundSelected(int rounds)
    {
        _selectedAutoRounds = rounds;
        AudioManager.Instance.PlayButtonClick();
        UpdateAutoRoundButtons();
    }
    #endregion

    #region Animations - OPTIMIZED
    private void AnimateButtonPress(GameObject button)
    {
        if (button == null) return;

        button.transform.DOKill(true);
        button.transform.localScale = Vector3.one;

        Tween punchTween = button.transform.DOPunchScale(Vector3.one * 0.1f, GameConfig.BUTTON_SCALE_DURATION, 1, 0.5f)
            .SetUpdate(true)
            .SetRecyclable(true);

        punchTween.OnComplete(() => punchTween.Kill());
    }

    private void AnimatePopupShow(GameObject popup)
    {
        if (popup == null) return;

        popup.transform.DOKill(true);
        popup.transform.localScale = Vector3.zero;

        Tween scaleTween = popup.transform.DOScale(Vector3.one, 0.3f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetRecyclable(true);

        scaleTween.OnComplete(() => scaleTween.Kill());
    }

    private void AnimatePopupHide(GameObject popup, System.Action onComplete = null)
    {
        if (popup == null)
        {
            onComplete?.Invoke();
            return;
        }

        popup.transform.DOKill(true);

        Tween scaleTween = popup.transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .SetRecyclable(true);

        scaleTween.OnComplete(() => {
            popup.SetActive(false);
            onComplete?.Invoke();
            scaleTween.Kill();
        });
    }

    private void AnimateWinText()
    {
        if (_winAmountText == null) return;

        _winAmountText.transform.DOKill(true);

        Tween punchTween = _winAmountText.transform.DOPunchScale(Vector3.one * 0.3f, 0.5f, 5, 0.5f)
            .SetUpdate(true)
            .SetRecyclable(true);

        punchTween.OnComplete(() => punchTween.Kill());
    }

    private void UpdateSpeedButtonStates()
    {
        // Normal
        if (_normalSpeedSelected != null)
            _normalSpeedSelected.SetActive(_currentSpeedMode == SpeedMode.Normal);
        UpdateSpeedButtonTextColor(_normalSpeedButton, _currentSpeedMode == SpeedMode.Normal);

        // Turbo
        if (_turboSpeedSelected != null)
            _turboSpeedSelected.SetActive(_currentSpeedMode == SpeedMode.Turbo);
        UpdateSpeedButtonTextColor(_turboSpeedButton, _currentSpeedMode == SpeedMode.Turbo);

        // Instant
        if (_instantSpeedSelected != null)
            _instantSpeedSelected.SetActive(_currentSpeedMode == SpeedMode.Instant);
        UpdateSpeedButtonTextColor(_instantSpeedButton, _currentSpeedMode == SpeedMode.Instant);
    }

    private void UpdateSpeedButtonTextColor(Button button, bool isSelected)
    {
        if (button == null) return;

        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
            buttonText.color = isSelected ? _speedSelectedColor : _speedNormalColor;
    }

    private void CleanupAnimations()
    {
        if (_playButton != null)
            _playButton.transform.DOKill(true);

        if (_pauseButton != null)
            _pauseButton.transform.DOKill(true);

        if (_betPopupArea != null)
            _betPopupArea.transform.DOKill(true);

        if (_autoBetPopupArea != null)
            _autoBetPopupArea.transform.DOKill(true);

        if (_winPopupArea != null)
            _winPopupArea.transform.DOKill(true);

        if (_winAmountText != null)
            _winAmountText.transform.DOKill(true);

        // NEW: Cleanup connection popup animations
        if (_reconnectPopupObject != null)
            _reconnectPopupObject.transform.DOKill(true);

        if (_disconnectPopupObject != null)
            _disconnectPopupObject.transform.DOKill(true);

        if (_quitGameObject != null)
            _quitGameObject.transform.DOKill(true);
    }
    #endregion
}
