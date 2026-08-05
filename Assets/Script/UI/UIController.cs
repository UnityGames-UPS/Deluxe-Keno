using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private Color _speedNormalColor = Color.white;
    [SerializeField] private Color _speedSelectedColor = Color.yellow;

    [Header("Display Texts")]
    [SerializeField] private TextMeshProUGUI _balanceText;
    [SerializeField] private TextMeshProUGUI _betAmountText;
    [SerializeField] private TextMeshProUGUI _winAmountText;
    [SerializeField] private TextMeshProUGUI _autoPlayRoundsText;

    [Header("Connection Popups")]
    [SerializeField] private GameObject _reconnectPopupMainPanel;
    [SerializeField] private GameObject _reconnectPopupArea;
    [SerializeField] private GameObject _disconnectPopupMainPanel;
    [SerializeField] private GameObject _disconnectPopupArea;
    [SerializeField] private Button _closeDisconnectButton;

    [Header("Quit Game Popup")]
    [SerializeField] private GameObject _quitGamePopupMainPanel;
    [SerializeField] private GameObject _quitGamePopupArea;
    [SerializeField] private Button _quitGameButton;
    [SerializeField] private Button _yesQuitButton;
    [SerializeField] private Button _noQuitButton;

    [Header("Bet Popup - DYNAMIC")]
    [SerializeField] private GameObject _betPopupMainPanel;
    [SerializeField] private GameObject _betPopupArea;
    [SerializeField] private Transform _betButtonsContainer; 
    [SerializeField] private GameObject _betButtonPrefab; 
    [SerializeField] private Button _betPopupCloseButton;
    [SerializeField] private Color _betNormalColor = Color.white;
    [SerializeField] private Color _betSelectedColor = Color.yellow;

    [Header("Auto Play Popup")]
    [SerializeField] private GameObject _autoBetPopupMainPanel;
    [SerializeField] private GameObject _autoBetPopupArea;
    [SerializeField] private Button[] _autoRoundButtons = new Button[8];
    [SerializeField] private GameObject[] _autoRoundButtonsSelected = new GameObject[8];
    [SerializeField] private Button _startAutoPlayButton;
    [SerializeField] private Button _autoBetPopupCloseButton;
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

    internal bool IsQuitSelf = false;

    private StringBuilder _stringBuilder = new StringBuilder(32);

    // NEW: Dynamic bet buttons tracking
    private List<Button> _dynamicBetButtons = new List<Button>();
    private List<GameObject> _dynamicBetSelectedImages = new List<GameObject>();
    private float[] _availableBets;

    private void Awake()
    {
        if (JSBridge.Instance != null)
        {
            JSBridge.Instance.RegisterVisibilityListener(gameObject.name);
        }
    }

    private void Start()
    {
        IsQuitSelf = false;
        InitializeUI();
        SubscribeToEvents();
        SetupButtonListeners();
        AudioManager.Instance.PlayBackgroundMusic();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        RemoveButtonListeners();
        CleanupAnimations();
        ClearDynamicBetButtons();
    }

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
                ShowDisconnectPopup();
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
        if (model == null || model.InitData == null) return;

        _selectedBet = model.PlayerData.currentBet;
        _availableBets = model.InitData.bets;

        // NEW: Create dynamic bet buttons
        CreateDynamicBetButtons();

        UpdateBetDisplay(_selectedBet);
        UpdateBalanceDisplay(model.PlayerData.balance);
        UpdateWinDisplay(model.LastWinAmount);
    }

    private void SetLoadingState()
    {
        if (_balanceText != null) _balanceText.text = _loadingText;
        if (_betAmountText != null) _betAmountText.text = _loadingText;
        if (_winAmountText != null) _winAmountText.text = "0.00";
    }

    private void InitializeUI()
    {
        _mainControlContainer?.SetActive(true);
        _speedControlContainer?.SetActive(false);
        _betPopupMainPanel?.SetActive(false);
        _autoBetPopupMainPanel?.SetActive(false);
        _winPopupMainPanel?.SetActive(false);
        _reconnectPopupMainPanel?.SetActive(false);
        _disconnectPopupMainPanel?.SetActive(false);
        _quitGamePopupMainPanel?.SetActive(false);

        if (_playButton != null)
        {
            _playButton.gameObject.SetActive(true);
            _playButton.interactable = true;
        }

        if (_pauseButton != null)
            _pauseButton.gameObject.SetActive(false);

        UpdateSpeedButtonStates();
        _betButtonSelected?.SetActive(false);
        _autoBetButtonSelected?.SetActive(false);
        SetMainControlButtonsInteractable(true);
    }

    #region Dynamic Bet Buttons

    /// <summary>
    /// Creates bet buttons dynamically based on available bets from server
    /// </summary>
    private void CreateDynamicBetButtons()
    {
        ClearDynamicBetButtons();

        if (_availableBets == null || _availableBets.Length == 0)
        {
            Debug.LogError("[UIController] No bets available to create buttons");
            return;
        }

        if (_betButtonPrefab == null || _betButtonsContainer == null)
        {
            Debug.LogError("[UIController] Bet button prefab or container not assigned!");
            return;
        }

        for (int i = 0; i < _availableBets.Length; i++)
        {
            float betValue = _availableBets[i];
            GameObject buttonObj = Instantiate(_betButtonPrefab, _betButtonsContainer);

            // Get button component
            Button button = buttonObj.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError($"[UIController] Bet button prefab missing Button component!");
                Destroy(buttonObj);
                continue;
            }

            // Find text component
            TextMeshProUGUI btnText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                _stringBuilder.Clear();
                _stringBuilder.Append(betValue.ToString("F2"));
                btnText.text = _stringBuilder.ToString();
                btnText.color = _betNormalColor;
            }

            // Find selected image (should be child named "Selected" or similar)
            Transform selectedTransform = buttonObj.transform.Find("Selected");
            GameObject selectedImage = selectedTransform != null ? selectedTransform.gameObject : null;

            if (selectedImage != null)
            {
                selectedImage.SetActive(false);
                _dynamicBetSelectedImages.Add(selectedImage);
            }
            else
            {
                _dynamicBetSelectedImages.Add(null);
            }

            // Add click listener
            button.onClick.AddListener(() => OnBetSelected(betValue));

            _dynamicBetButtons.Add(button);
            buttonObj.SetActive(true);
        }

        Debug.Log($"[UIController] Created {_dynamicBetButtons.Count} dynamic bet buttons");
    }

    /// <summary>
    /// Clears all dynamically created bet buttons
    /// </summary>
    private void ClearDynamicBetButtons()
    {
        foreach (Button btn in _dynamicBetButtons)
        {
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                Destroy(btn.gameObject);
            }
        }

        _dynamicBetButtons.Clear();
        _dynamicBetSelectedImages.Clear();
    }

    /// <summary>
    /// Updates visual state of all bet buttons
    /// </summary>
    private void UpdateBetButtons()
    {
        if (_availableBets == null) return;

        for (int i = 0; i < _dynamicBetButtons.Count && i < _availableBets.Length; i++)
        {
            Button button = _dynamicBetButtons[i];
            if (button == null) continue;

            float betValue = _availableBets[i];
            bool isSelected = Mathf.Approximately(betValue, _selectedBet);

            // Update text color
            TextMeshProUGUI btnText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.color = isSelected ? _betSelectedColor : _betNormalColor;
            }

            // Update selected image
            if (i < _dynamicBetSelectedImages.Count && _dynamicBetSelectedImages[i] != null)
            {
                _dynamicBetSelectedImages[i].SetActive(isSelected);
            }
        }
    }

    #endregion

    #region Button Setup
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
        _closeDisconnectButton?.onClick.AddListener(() => GameController.Instance?.CloseSocket());
        _quitGameButton?.onClick.AddListener(OpenQuitGamePopup);
        _yesQuitButton?.onClick.AddListener(QuitGame);
        _noQuitButton?.onClick.AddListener(CloseQuitGamePopup);

        //AddBackgroundClickListener(_betPopupMainPanel, CloseBetPopup);
        //AddBackgroundClickListener(_autoBetPopupMainPanel, CloseAutoBetPopup);
    }

    private void AddBackgroundClickListener(GameObject panel, UnityEngine.Events.UnityAction action)
    {
        if (panel == null) return;
        Button btn = panel.GetComponent<Button>();
        if (btn == null) btn = panel.AddComponent<Button>();
        btn.onClick.AddListener(action);
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
        GameEvents.OnShowWinPopup += ShowWinPopup;
        GameEvents.OnSpeedModeChanged += HandleSpeedModeChanged;
        GameEvents.OnConnectionUnstable += ShowReconnectPopup;
        GameEvents.OnConnectionLost += HandleConnectionLost;
        GameEvents.OnConnectionRestored += HandleConnectionRestored;
       
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
    #endregion

    #region Display Updates
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
        AnimateButton(_playButton?.gameObject);
        AudioManager.Instance.PlayPlayButton();
        GameEvents.TriggerPlayButtonClicked();
    }

    private void OnPauseClicked()
    {
        AnimateButton(_pauseButton?.gameObject);
        AudioManager.Instance.PlayButtonClick();
        _isAutoPlayMode = false;
        GameEvents.TriggerAutoPlayToggled(false);
    }

    private void OnClearClicked()
    {
        AnimateButton(_clearButton?.gameObject);
        AudioManager.Instance.PlayButtonClick();
        GameEvents.TriggerAllNumbersCleared();
    }

    private void OnBetClicked()
    {
        AnimateButton(_betButton?.gameObject);
        AudioManager.Instance.PlayButtonClick();
        ShowBetPopup();
    }

    private void OnAutoBetClicked()
    {
        AnimateButton(_autoBetButton?.gameObject);
        AudioManager.Instance.PlayButtonClick();
        ShowAutoBetPopup();
    }

    private void OnSpeedChanged(SpeedMode mode)
    {
        _currentSpeedMode = mode;
        UpdateSpeedButtonStates();

        switch (mode)
        {
            case SpeedMode.Normal: AudioManager.Instance.PlayNormalSpeedClick(); break;
            case SpeedMode.Turbo: AudioManager.Instance.PlayTurboSpeedClick(); break;
            case SpeedMode.Instant: AudioManager.Instance.PlayInstantSpeedClick(); break;
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
        _autoBetButtonSelected?.SetActive(false);
        _remainingAutoRounds = _selectedAutoRounds;
        _isAutoPlayMode = true;
        AudioManager.Instance.PlayButtonClick();
        GameEvents.TriggerAutoBetRoundsChanged(_selectedAutoRounds);
        GameEvents.TriggerAutoPlayToggled(true);
        CloseAutoBetPopupAndStartGame();
    }
    #endregion

    #region Game State Handlers
    private void HandleGameStarted()
    {
        if (_isAutoPlayMode)
        {
            _playButton?.gameObject.SetActive(false);
            _pauseButton?.gameObject.SetActive(true);
        }
        else
        {
            if (_playButton != null)
            {
                _playButton.gameObject.SetActive(true);
                _playButton.interactable = false;
            }
            _pauseButton?.gameObject.SetActive(false);
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
            if (_playButton != null)
            {
                _playButton.gameObject.SetActive(true);
                _playButton.interactable = true;
            }
            _pauseButton?.gameObject.SetActive(false);
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
            if (_playButton != null)
            {
                _playButton.gameObject.SetActive(true);
                _playButton.interactable = true;
            }
            _pauseButton?.gameObject.SetActive(false);
            _mainControlContainer?.SetActive(true);
            _speedControlContainer?.SetActive(false);
            SetMainControlButtonsInteractable(true);




            _remainingAutoRounds = 0;
            UpdateAutoPlayRoundsDisplay();
        }
       
    }

    #endregion

    #region Helper Methods
    private void SetMainControlButtonsInteractable(bool interactable)
    {
        if (_clearButton != null) _clearButton.interactable = interactable;
        if (_betButton != null) _betButton.interactable = interactable;
        if (_autoBetButton != null) _autoBetButton.interactable = interactable;
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

    #region Connection Popup Management
    private void ShowReconnectPopup()
    {
        _reconnectPopupMainPanel?.SetActive(true);
        if (_reconnectPopupArea != null)
        {
            _reconnectPopupArea.SetActive(true);
            AnimatePopupShow(_reconnectPopupArea);
        }
    }

    private void CloseReconnectPopup()
    {
        if (_reconnectPopupArea != null)
        {
            AnimatePopupHide(_reconnectPopupArea, () => _reconnectPopupMainPanel?.SetActive(false));
        }
        else
        {
            _reconnectPopupMainPanel?.SetActive(false);
        }
    }

    private void ShowDisconnectPopup()
    {
        _disconnectPopupMainPanel?.SetActive(true);
        if (_disconnectPopupArea != null)
        {
            _disconnectPopupArea.SetActive(true);
            AnimatePopupShow(_disconnectPopupArea);
        }
    }

    private void CloseDisconnectPopup()
    {
        if (_disconnectPopupArea != null)
        {
            AnimatePopupHide(_disconnectPopupArea, () => _disconnectPopupMainPanel?.SetActive(false));
        }
        else
        {
            _disconnectPopupMainPanel?.SetActive(false);
        }
    }

    internal void CheckAndClosePopups()
    {
        CloseReconnectPopup();
        CloseDisconnectPopup();
    }

    private void HandleConnectionLost()
    {
        if (!IsQuitSelf)
            ShowDisconnectPopup();
    }

    private void HandleConnectionRestored()
    {
        CloseReconnectPopup();
        CloseDisconnectPopup();
    }
    #endregion

    #region Quit Game Management
    private void OpenQuitGamePopup()
    {
        AudioManager.Instance?.PlayButtonClick();
        _quitGamePopupMainPanel?.SetActive(true);
        if (_quitGamePopupArea != null)
        {
            _quitGamePopupArea.SetActive(true);
            AnimatePopupShow(_quitGamePopupArea);
        }
    }

    private void CloseQuitGamePopup()
    {
        AudioManager.Instance?.PlayButtonClick();
        if (_quitGamePopupArea != null)
        {
            AnimatePopupHide(_quitGamePopupArea, () => _quitGamePopupMainPanel?.SetActive(false));
        }
        else
        {
            _quitGamePopupMainPanel?.SetActive(false);
        }
    }

    private void QuitGame()
    {
        IsQuitSelf = true;
        AudioManager.Instance?.PlayButtonClick();
        CloseQuitGamePopup();
        GameController.Instance?.CloseSocket();
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
            AnimatePopupHide(_winPopupArea, () => _winPopupMainPanel?.SetActive(false));
        }
        else
        {
            _winPopupMainPanel?.SetActive(false);
        }

        _winPopupCoroutine = null;
    }
    #endregion

    #region Bet/Auto Popups
    private void ShowBetPopup()
    {
        _betPopupMainPanel?.SetActive(true);
        if (_betPopupArea != null)
        {
            _betPopupArea.SetActive(true);
            UpdateBetButtons();
            AnimatePopupShow(_betPopupArea);
        }
        _betButtonSelected?.SetActive(true);
    }

    private void CloseBetPopup()
    {
        AudioManager.Instance.PlayButtonClick();
        if (_betPopupArea != null)
        {
            AnimatePopupHide(_betPopupArea, () => _betPopupMainPanel?.SetActive(false));
        }
        else
        {
            _betPopupMainPanel?.SetActive(false);
        }
        _betButtonSelected?.SetActive(false);
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
        _autoBetButtonSelected?.SetActive(true);
    }

    private void CloseAutoBetPopup()
    {
        AudioManager.Instance.PlayButtonClick();
        if (_autoBetPopupArea != null)
        {
            AnimatePopupHide(_autoBetPopupArea, () => _autoBetPopupMainPanel?.SetActive(false));
        }
        else
        {
            _autoBetPopupMainPanel?.SetActive(false);
        }
            _autoBetButtonSelected?.SetActive(false);
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


    #region Animations
    private void AnimateButton(GameObject button)
    {
        if (button == null) return;
        button.transform.DOKill(true);
        button.transform.localScale = Vector3.one;
        Tween punchTween = button.transform.DOPunchScale(Vector3.one * 0.1f, GameConfig.BUTTON_SCALE_DURATION, 1, 0.5f)
            .SetUpdate(true).SetRecyclable(true);
        punchTween.OnComplete(() => punchTween.Kill());
    }

    private void AnimatePopupShow(GameObject popup)
    {
        if (popup == null) return;
        popup.transform.DOKill(true);
        popup.transform.localScale = Vector3.zero;
        Tween scaleTween = popup.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetUpdate(true).SetRecyclable(true);
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
        Tween scaleTween = popup.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).SetUpdate(true).SetRecyclable(true);
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
        Tween punchTween = _winAmountText.transform.DOPunchScale(Vector3.one * 0.3f, 0.5f, 5, 0.5f).SetUpdate(true).SetRecyclable(true);
        punchTween.OnComplete(() => punchTween.Kill());
    }

    private void UpdateSpeedButtonStates()
    {
        _normalSpeedSelected?.SetActive(_currentSpeedMode == SpeedMode.Normal);
        UpdateSpeedButtonTextColor(_normalSpeedButton, _currentSpeedMode == SpeedMode.Normal);
        _turboSpeedSelected?.SetActive(_currentSpeedMode == SpeedMode.Turbo);
        UpdateSpeedButtonTextColor(_turboSpeedButton, _currentSpeedMode == SpeedMode.Turbo);
        _instantSpeedSelected?.SetActive(_currentSpeedMode == SpeedMode.Instant);
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
        if (_playButton != null && _playButton.transform != null)
            _playButton.transform.DOKill(true);
        if (_pauseButton != null && _pauseButton.transform != null)
            _pauseButton.transform.DOKill(true);
        if (_betPopupArea != null && _betPopupArea.transform != null)
            _betPopupArea.transform.DOKill(true);
        if (_autoBetPopupArea != null && _autoBetPopupArea.transform != null)
            _autoBetPopupArea.transform.DOKill(true);
        if (_winPopupArea != null && _winPopupArea.transform != null)
            _winPopupArea.transform.DOKill(true);
        if (_winAmountText != null && _winAmountText.transform != null)
            _winAmountText.transform.DOKill(true);
        if (_reconnectPopupArea != null && _reconnectPopupArea.transform != null)
            _reconnectPopupArea.transform.DOKill(true);
        if (_disconnectPopupArea != null && _disconnectPopupArea.transform != null)
            _disconnectPopupArea.transform.DOKill(true);
        if (_quitGamePopupArea != null && _quitGamePopupArea.transform != null)
            _quitGamePopupArea.transform.DOKill(true);
    }
    #endregion

    #region WebGL Focus Handler (Check 2)
    public void OnFocusChanged(string value)
    {
        bool focused = value == "1";
        Debug.Log($"[UIController] UNITY FOCUS CHANGED: {value} (focused: {focused})");
        AudioManager.Instance?.SetMuteAll(!focused);
        GameController.Instance?.HandleFocusChange(focused);
    }
    #endregion
}