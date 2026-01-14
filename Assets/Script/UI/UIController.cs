using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Text;

/// <summary>
/// FIXED:
/// - Play button stays visible but becomes non-interactable during play
/// - Only in autoplay mode does it get disabled and pause button shows
/// </summary>
public class UIController : MonoBehaviour
{
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

    [Header("Display Texts")]
    [SerializeField] private TextMeshProUGUI _balanceText;
    [SerializeField] private TextMeshProUGUI _betAmountText;
    [SerializeField] private TextMeshProUGUI _winAmountText;
    [SerializeField] private TextMeshProUGUI _autoPlayRoundsText;

    [Header("Bet Popup")]
    [SerializeField] private GameObject _betPopupMainPanel;
    [SerializeField] private GameObject _betPopupArea;
    [SerializeField] private Button[] _betButtons = new Button[8];
    [SerializeField] private GameObject[] _betButtonsSelected = new GameObject[8];
    [SerializeField] private Button _betPopupCloseButton;

    [Header("Bet Button Colors")]
    [SerializeField] private Color _betNormalColor = Color.white;
    [SerializeField] private Color _betSelectedColor = Color.yellow;

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

    [Header("Win Popup")]
    [SerializeField] private GameObject _winPopupMainPanel;
    [SerializeField] private GameObject _winPopupArea;
    [SerializeField] private TextMeshProUGUI _winPopupAmountText;
    [SerializeField] private float _winPopupDisplayDuration = 2f;

    [Header("Initialization")]
    [SerializeField] private string _loadingText = "Loading...";

    private SpeedMode _currentSpeedMode = SpeedMode.Normal;
    private float _selectedBet;
    private int _selectedAutoRounds;
    private int _remainingAutoRounds;
    private bool _isAutoPlayMode;
    private float _currentWinAmount;
    private Coroutine _winPopupCoroutine;
    private bool _isInitialized;

    // OPTIMIZATION: Cached string builder to reduce GC
    private StringBuilder _stringBuilder = new StringBuilder(32);

    // OPTIMIZATION: Cached format strings
    private const string BALANCE_FORMAT = "{0:F2}";
    private const string BET_FORMAT = "{0:F2}";
    private const string WIN_FORMAT = "{0:F2}";

    private void Start()
    {
        InitializeUI();
        SubscribeToEvents();
        SetupButtonListeners();
        StartCoroutine(WaitForGameControllerAndInitialize());

        // Start background music
        AudioManager.Instance.PlayBackgroundMusic();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        RemoveButtonListeners();
        CleanupAnimations();
    }

    private IEnumerator WaitForGameControllerAndInitialize()
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
                yield break;
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

        // FIXED: Play button always visible and interactable at start
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

    private void SetupButtonListeners()
    {
        _playButton?.onClick.AddListener(OnPlayClicked);
        _pauseButton?.onClick.AddListener(OnPauseClicked);
        _clearButton?.onClick.AddListener(OnClearClicked);
        _betButton?.onClick.AddListener(OnBetClicked);
        _autoBetButton?.onClick.AddListener(OnAutoBetClicked);

        _normalSpeedButton?.onClick.AddListener(() => OnSpeedChanged(SpeedMode.Normal));
        _turboSpeedButton?.onClick.AddListener(() => OnSpeedChanged(SpeedMode.Turbo));
        _instantSpeedButton?.onClick.AddListener(() => OnSpeedChanged(SpeedMode.Instant));
        _betPopupCloseButton?.onClick.AddListener(CloseBetPopup);
        _autoBetPopupCloseButton?.onClick.AddListener(CloseAutoBetPopup);
        _startAutoPlayButton?.onClick.AddListener(OnStartAutoPlayClicked);

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
    }

    private void SubscribeToEvents()
    {
        GameEvents.OnBalanceUpdated += UpdateBalanceDisplay;
        GameEvents.OnBetChanged += UpdateBetDisplay;
        GameEvents.OnWinAmountUpdated += UpdateWinDisplay;
        GameEvents.OnGameStarted += HandleGameStarted;
        GameEvents.OnGameEnded += HandleGameEnded;
        GameEvents.OnAutoPlayToggled += HandleAutoPlayToggled;
        GameEvents.OnShowWinPopup += ShowWinPopup;
        GameEvents.OnSpeedModeChanged += HandleSpeedModeChanged;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnBalanceUpdated -= UpdateBalanceDisplay;
        GameEvents.OnBetChanged -= UpdateBetDisplay;
        GameEvents.OnWinAmountUpdated -= UpdateWinDisplay;
        GameEvents.OnGameStarted -= HandleGameStarted;
        GameEvents.OnGameEnded -= HandleGameEnded;
        GameEvents.OnAutoPlayToggled -= HandleAutoPlayToggled;
        GameEvents.OnShowWinPopup -= ShowWinPopup;
        GameEvents.OnSpeedModeChanged -= HandleSpeedModeChanged;
    }

    #region Display Updates - OPTIMIZED
    private void UpdateBalanceDisplay(float balance)
    {
        if (_balanceText != null)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(balance.ToString("F2"));
            _balanceText.text = _stringBuilder.ToString();
        }
    }

    private void UpdateBetDisplay(float bet)
    {
        if (_betAmountText != null)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(bet.ToString("F2"));
            _betAmountText.text = _stringBuilder.ToString();
        }
    }

    private void UpdateWinDisplay(float winAmount)
    {
        _currentWinAmount = winAmount;
        if (_winAmountText != null)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(winAmount.ToString("F2"));
            _winAmountText.text = _stringBuilder.ToString();

            if (winAmount > 0)
                AnimateWinText();
        }
    }
    #endregion

    #region Button Handlers
    private void OnPlayClicked()
    {
        if (!_isInitialized) return;
        AnimateButtonPress(_playButton?.gameObject);
        AudioManager.Instance.PlayPlayButton();
        GameEvents.TriggerPlayButtonClicked();
    }

    private void OnPauseClicked()
    {
        AnimateButtonPress(_pauseButton?.gameObject);
        AudioManager.Instance.PlayButtonClick();
        _isAutoPlayMode = false;
        GameEvents.TriggerAutoPlayToggled(false);
    }

    private void OnClearClicked()
    {
        AnimateButtonPress(_clearButton?.gameObject);
        AudioManager.Instance.PlayButtonClick();
        GameEvents.TriggerAllNumbersCleared();
    }

    private void OnBetClicked()
    {
        AnimateButtonPress(_betButton?.gameObject);
        AudioManager.Instance.PlayButtonClick();
        ShowBetPopup();
    }

    private void OnAutoBetClicked()
    {
        AnimateButtonPress(_autoBetButton?.gameObject);
        AudioManager.Instance.PlayButtonClick();
        ShowAutoBetPopup();
    }

    private void OnSpeedChanged(SpeedMode mode)
    {
        _currentSpeedMode = mode;
        UpdateSpeedButtonStates();

        // Play appropriate speed button sound
        switch (mode)
        {
            case SpeedMode.Normal:
                AudioManager.Instance.PlayNormalSpeedClick();
                break;
            case SpeedMode.Turbo:
                AudioManager.Instance.PlayTurboSpeedClick();
                break;
            case SpeedMode.Instant:
                AudioManager.Instance.PlayInstantSpeedClick();
                break;
        }

        GameEvents.TriggerSpeedModeChanged(mode);
    }

    private void HandleSpeedModeChanged(SpeedMode mode)
    {
        _currentSpeedMode = mode;
        UpdateSpeedButtonStates();
    }

    private void OnStartAutoPlayClicked()
    {
        if (_selectedAutoRounds <= 0)
        {
            ErrorPopupManager.ShowError(ErrorMessages.AUTOPLAY_NO_ROUNDS);
            return;
        }

        _remainingAutoRounds = _selectedAutoRounds;
        _isAutoPlayMode = true;
        AudioManager.Instance.PlayButtonClick();
        GameEvents.TriggerAutoBetRoundsChanged(_selectedAutoRounds);
        GameEvents.TriggerAutoPlayToggled(true);

        CloseAutoBetPopupAndStartGame();
    }
    #endregion

    #region Event Handlers
    private void HandleGameStarted()
    {
        // FIXED: Different behavior for autoplay vs normal play
        if (_isAutoPlayMode)
        {
            // AUTOPLAY MODE: Hide play button, show pause button
            if (_playButton != null)
                _playButton.gameObject.SetActive(false);

            if (_pauseButton != null)
                _pauseButton.gameObject.SetActive(true);
        }
        else
        {
            // NORMAL PLAY MODE: Keep play button visible but make it non-interactable
            if (_playButton != null)
            {
                _playButton.gameObject.SetActive(true);
                _playButton.interactable = false;
            }

            if (_pauseButton != null)
                _pauseButton.gameObject.SetActive(false);
        }

        _mainControlContainer?.SetActive(false);
        _speedControlContainer?.SetActive(true);

        SetMainControlButtonsInteractable(false);
        UpdateAutoPlayRoundsDisplay();
    }

    private void HandleGameEnded()
    {
        if (!_isAutoPlayMode)
        {
            // NORMAL PLAY MODE: Re-enable play button
            if (_playButton != null)
            {
                _playButton.gameObject.SetActive(true);
                _playButton.interactable = true;
            }

            if (_pauseButton != null)
                _pauseButton.gameObject.SetActive(false);

            _mainControlContainer?.SetActive(true);
            _speedControlContainer?.SetActive(false);
        }

        SetMainControlButtonsInteractable(!_isAutoPlayMode);

        if (_isAutoPlayMode)
        {
            _remainingAutoRounds--;
            UpdateAutoPlayRoundsDisplay();

            if (_remainingAutoRounds <= 0)
            {
                _isAutoPlayMode = false;
                GameEvents.TriggerAutoPlayToggled(false);
            }
        }
    }

    private void HandleAutoPlayToggled(bool isActive)
    {
        _isAutoPlayMode = isActive;

        if (!isActive)
        {
            // When autoplay stops, restore normal state
            if (_playButton != null)
            {
                _playButton.gameObject.SetActive(true);
                _playButton.interactable = true;
            }

            if (_pauseButton != null)
                _pauseButton.gameObject.SetActive(false);

            _mainControlContainer?.SetActive(true);
            _speedControlContainer?.SetActive(false);
            SetMainControlButtonsInteractable(true);

            if (_autoBetButtonSelected != null)
                _autoBetButtonSelected.SetActive(false);

            _remainingAutoRounds = 0;
            UpdateAutoPlayRoundsDisplay();
        }
    }
    #endregion

    #region Helper Methods
    private void SetMainControlButtonsInteractable(bool interactable)
    {
        if (_clearButton != null)
            _clearButton.interactable = interactable;

        if (_betButton != null)
            _betButton.interactable = interactable;

        if (_autoBetButton != null)
            _autoBetButton.interactable = interactable;
    }

    private void UpdateAutoPlayRoundsDisplay()
    {
        if (_autoPlayRoundsText != null)
        {
            if (_isAutoPlayMode && _remainingAutoRounds > 0)
            {
                _stringBuilder.Clear();
                _stringBuilder.Append(_remainingAutoRounds);
                _autoPlayRoundsText.text = _stringBuilder.ToString();
            }
            else
            {
                _autoPlayRoundsText.text = "";
            }
        }
    }
    #endregion

    #region Win Popup
    private void ShowWinPopup(float winAmount)
    {
        if (_winPopupCoroutine != null)
            StopCoroutine(_winPopupCoroutine);

        _winPopupCoroutine = StartCoroutine(DisplayWinPopup(winAmount));
    }

    private IEnumerator DisplayWinPopup(float winAmount)
    {
        if (_winPopupAmountText != null)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(winAmount.ToString("F2"));
            _winPopupAmountText.text = _stringBuilder.ToString();
        }

        _winPopupMainPanel?.SetActive(true);

        if (_winPopupArea != null)
        {
            _winPopupArea.SetActive(true);
            AnimatePopupShow(_winPopupArea);
        }

        yield return new WaitForSeconds(_winPopupDisplayDuration);

        if (_winPopupArea != null)
        {
            AnimatePopupHide(_winPopupArea, () => {
                _winPopupMainPanel?.SetActive(false);
            });
        }
        else
        {
            _winPopupMainPanel?.SetActive(false);
        }

        _winPopupCoroutine = null;
    }
    #endregion

    #region Popups
    private void ShowBetPopup()
    {
        _betPopupMainPanel?.SetActive(true);

        if (_betPopupArea != null)
        {
            _betPopupArea.SetActive(true);
            UpdateBetButtons();
            AnimatePopupShow(_betPopupArea);
        }

        if (_betButtonSelected != null)
            _betButtonSelected.SetActive(true);
    }

    private void CloseBetPopup()
    {
        AudioManager.Instance.PlayButtonClick();

        if (_betPopupArea != null)
        {
            AnimatePopupHide(_betPopupArea, () => {
                _betPopupMainPanel?.SetActive(false);
            });
        }
        else
        {
            _betPopupMainPanel?.SetActive(false);
        }

        if (_betButtonSelected != null)
            _betButtonSelected.SetActive(false);
    }

    private void ShowAutoBetPopup()
    {
        _autoBetPopupMainPanel?.SetActive(true);

        if (_autoBetPopupArea != null)
        {
            _autoBetPopupArea.SetActive(true);
            UpdateAutoRoundButtons();
            AnimatePopupShow(_autoBetPopupArea);
        }

        if (_autoBetButtonSelected != null)
            _autoBetButtonSelected.SetActive(true);
    }

    private void CloseAutoBetPopup()
    {
        AudioManager.Instance.PlayButtonClick();

        if (_autoBetPopupArea != null)
        {
            AnimatePopupHide(_autoBetPopupArea, () => {
                _autoBetPopupMainPanel?.SetActive(false);
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
    }
    #endregion
}