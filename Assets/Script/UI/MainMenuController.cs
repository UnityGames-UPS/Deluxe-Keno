using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main Menu Controller - Manages menu panel, sub-panels, and navigation
/// Integrates with existing game systems and audio manager
/// </summary>
public class MainMenuController : MonoBehaviour
{
    #region Singleton
    private static MainMenuController _instance;
    public static MainMenuController Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<MainMenuController>();
            return _instance;
        }
    }
    #endregion

    #region Menu Panel References
    [Header("Main Menu")]
    [SerializeField] private GameObject _mainMenuPanel;
    [SerializeField] private GameObject _mainMenuArea;
    [SerializeField] private Button _menuOpenButton;
    [SerializeField] private Button _menuCloseButton;
    #endregion

    #region Sub-Panel References
    [Header("Sub-Panels")]
    [SerializeField] private GameObject _howToPlayPanel;
    [SerializeField] private GameObject _payTablePanel;
    [SerializeField] private GameObject _settingsPanel;
    #endregion

    #region Sub-Panel Menu Buttons
    [Header("Sub-Panel Navigation Buttons")]
    [SerializeField] private Button _howToPlayButton;
    [SerializeField] private GameObject _howToPlaySelectedImage;
    [SerializeField] private Button _payTableButton;
    [SerializeField] private GameObject _payTableSelectedImage;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private GameObject _settingsSelectedImage;
    #endregion

    #region How To Play Navigation
    [Header("How To Play Panel")]
    [SerializeField] private GameObject[] _howToPlayPages = new GameObject[6];
    [SerializeField] private Button _howToPlayLeftButton;
    [SerializeField] private Button _howToPlayRightButton;
    private int _currentHowToPlayPage = 0;
    #endregion

    #region Settings Panel - Audio
    [Header("Settings - Audio Toggles")]
    [SerializeField] private Toggle _bgmToggle;
    [SerializeField] private Toggle _sfxToggle;
    #endregion

    #region Settings Panel - Speed
    [Header("Settings - Speed Buttons")]
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

    #region State
    private enum MenuPanel { None, HowToPlay, PayTable, Settings }
    private MenuPanel _currentPanel = MenuPanel.None;
    private SpeedMode _currentSpeedMode = SpeedMode.Normal;
    private bool _isMenuOpen = false;
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
    }

    private void Start()
    {
        InitializeMenu();
        SetupButtonListeners();
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        RemoveButtonListeners();
        CleanupAnimations();
    }
    #endregion

    #region Initialization
    private void InitializeMenu()
    {
        // Hide main menu
        if (_mainMenuPanel != null)
            _mainMenuPanel.SetActive(false);

        // Hide all sub-panels
        if (_howToPlayPanel != null)
            _howToPlayPanel.SetActive(false);
        if (_payTablePanel != null)
            _payTablePanel.SetActive(false);
        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);

        // Initialize How To Play pages
        for (int i = 0; i < _howToPlayPages.Length; i++)
        {
            if (_howToPlayPages[i] != null)
                _howToPlayPages[i].SetActive(i == 0);
        }
        _currentHowToPlayPage = 0;
        UpdateHowToPlayNavigationButtons();

        // Initialize audio toggles
        if (_bgmToggle != null)
            _bgmToggle.isOn = AudioManager.Instance.MusicEnabled;
        if (_sfxToggle != null)
            _sfxToggle.isOn = AudioManager.Instance.SFXEnabled;

        // Initialize speed mode
        _currentSpeedMode = SpeedMode.Normal;
        UpdateSpeedButtonStates();
    }

    private void SetupButtonListeners()
    {
        // Main menu open/close
        if (_menuOpenButton != null)
            _menuOpenButton.onClick.AddListener(OpenMenu);
        if (_menuCloseButton != null)
            _menuCloseButton.onClick.AddListener(CloseMenu);

        // Background click to close
        if (_mainMenuPanel != null)
        {
            Button bgButton = _mainMenuPanel.GetComponent<Button>();
            if (bgButton == null)
                bgButton = _mainMenuPanel.AddComponent<Button>();
            bgButton.onClick.AddListener(CloseMenu);
        }

        // Sub-panel navigation
        if (_howToPlayButton != null)
            _howToPlayButton.onClick.AddListener(() => SwitchToPanel(MenuPanel.HowToPlay));
        if (_payTableButton != null)
            _payTableButton.onClick.AddListener(() => SwitchToPanel(MenuPanel.PayTable));
        if (_settingsButton != null)
            _settingsButton.onClick.AddListener(() => SwitchToPanel(MenuPanel.Settings));

        // How To Play navigation
        if (_howToPlayLeftButton != null)
            _howToPlayLeftButton.onClick.AddListener(OnHowToPlayLeft);
        if (_howToPlayRightButton != null)
            _howToPlayRightButton.onClick.AddListener(OnHowToPlayRight);

        // Audio toggles
        if (_bgmToggle != null)
            _bgmToggle.onValueChanged.AddListener(OnBGMToggleChanged);
        if (_sfxToggle != null)
            _sfxToggle.onValueChanged.AddListener(OnSFXToggleChanged);

        // Speed buttons
        if (_normalSpeedButton != null)
            _normalSpeedButton.onClick.AddListener(() => OnSpeedButtonClicked(SpeedMode.Normal));
        if (_turboSpeedButton != null)
            _turboSpeedButton.onClick.AddListener(() => OnSpeedButtonClicked(SpeedMode.Turbo));
        if (_instantSpeedButton != null)
            _instantSpeedButton.onClick.AddListener(() => OnSpeedButtonClicked(SpeedMode.Instant));
    }

    private void RemoveButtonListeners()
    {
        if (_menuOpenButton != null)
            _menuOpenButton.onClick.RemoveAllListeners();
        if (_menuCloseButton != null)
            _menuCloseButton.onClick.RemoveAllListeners();
        if (_howToPlayButton != null)
            _howToPlayButton.onClick.RemoveAllListeners();
        if (_payTableButton != null)
            _payTableButton.onClick.RemoveAllListeners();
        if (_settingsButton != null)
            _settingsButton.onClick.RemoveAllListeners();
        if (_howToPlayLeftButton != null)
            _howToPlayLeftButton.onClick.RemoveAllListeners();
        if (_howToPlayRightButton != null)
            _howToPlayRightButton.onClick.RemoveAllListeners();
        if (_bgmToggle != null)
            _bgmToggle.onValueChanged.RemoveAllListeners();
        if (_sfxToggle != null)
            _sfxToggle.onValueChanged.RemoveAllListeners();
        if (_normalSpeedButton != null)
            _normalSpeedButton.onClick.RemoveAllListeners();
        if (_turboSpeedButton != null)
            _turboSpeedButton.onClick.RemoveAllListeners();
        if (_instantSpeedButton != null)
            _instantSpeedButton.onClick.RemoveAllListeners();
    }

    private void SubscribeToEvents()
    {
        GameEvents.OnSpeedModeChanged += HandleSpeedModeChanged;
        GameEvents.OnAudioSettingsChanged += HandleAudioSettingsChanged;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnSpeedModeChanged -= HandleSpeedModeChanged;
        GameEvents.OnAudioSettingsChanged -= HandleAudioSettingsChanged;
    }

    private void OnEnable()
    {
        // Sync with current speed mode when menu opens
        if (GameController.Instance != null && GameController.Instance.GetModel() != null)
        {
            _currentSpeedMode = GameController.Instance.GetModel().GameState.currentSpeedMode;
            UpdateSpeedButtonStates();
        }
    }
    #endregion

    #region Menu Open/Close
    private void OpenMenu()
    {
        AudioManager.Instance.PlayButtonClick();

        _isMenuOpen = true;
        _mainMenuPanel?.SetActive(true);

        // Open How To Play panel by default
        SwitchToPanel(MenuPanel.HowToPlay);

        // Animate menu
        if (_mainMenuArea != null)
        {
            _mainMenuArea.SetActive(true);
            AnimateMenuShow();
        }

        GameLogger.Log("Main Menu opened");
    }

    private void CloseMenu()
    {
        AudioManager.Instance.PlayButtonClick();

        if (_mainMenuArea != null)
        {
            AnimateMenuHide(() => {
                _isMenuOpen = false;
                _currentPanel = MenuPanel.None;
                _mainMenuPanel?.SetActive(false);
            });
        }
        else
        {
            _isMenuOpen = false;
            _currentPanel = MenuPanel.None;
            _mainMenuPanel?.SetActive(false);
        }

        GameLogger.Log("Main Menu closed");
    }
    #endregion

    #region Sub-Panel Switching
    private void SwitchToPanel(MenuPanel panel)
    {
        AudioManager.Instance.PlayButtonClick();

        _currentPanel = panel;

        // Deactivate all panels
        if (_howToPlayPanel != null)
            _howToPlayPanel.SetActive(false);
        if (_payTablePanel != null)
            _payTablePanel.SetActive(false);
        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);

        // Activate selected panel
        switch (panel)
        {
            case MenuPanel.HowToPlay:
                if (_howToPlayPanel != null)
                    _howToPlayPanel.SetActive(true);
                break;
            case MenuPanel.PayTable:
                if (_payTablePanel != null)
                    _payTablePanel.SetActive(true);
                break;
            case MenuPanel.Settings:
                if (_settingsPanel != null)
                    _settingsPanel.SetActive(true);
                break;
        }

        // Update button selection visuals
        UpdatePanelButtonStates();

        GameLogger.Log($"Switched to {panel} panel");
    }

    private void UpdatePanelButtonStates()
    {
        // How To Play
        if (_howToPlaySelectedImage != null)
            _howToPlaySelectedImage.SetActive(_currentPanel == MenuPanel.HowToPlay);

        // Pay Table
        if (_payTableSelectedImage != null)
            _payTableSelectedImage.SetActive(_currentPanel == MenuPanel.PayTable);

        // Settings
        if (_settingsSelectedImage != null)
            _settingsSelectedImage.SetActive(_currentPanel == MenuPanel.Settings);
    }
    #endregion

    #region How To Play Navigation
    private void OnHowToPlayLeft()
    {
        AudioManager.Instance.PlayButtonClick();

        if (_currentHowToPlayPage > 0)
        {
            _currentHowToPlayPage--;
            UpdateHowToPlayPage();
        }
    }

    private void OnHowToPlayRight()
    {
        AudioManager.Instance.PlayButtonClick();

        if (_currentHowToPlayPage < _howToPlayPages.Length - 1)
        {
            _currentHowToPlayPage++;
            UpdateHowToPlayPage();
        }
    }

    private void UpdateHowToPlayPage()
    {
        // Show only current page
        for (int i = 0; i < _howToPlayPages.Length; i++)
        {
            if (_howToPlayPages[i] != null)
                _howToPlayPages[i].SetActive(i == _currentHowToPlayPage);
        }

        UpdateHowToPlayNavigationButtons();
        GameLogger.Log($"How To Play page: {_currentHowToPlayPage + 1}/{_howToPlayPages.Length}");
    }

    private void UpdateHowToPlayNavigationButtons()
    {
        // Left button disabled on first page
        if (_howToPlayLeftButton != null)
            _howToPlayLeftButton.interactable = _currentHowToPlayPage > 0;

        // Right button disabled on last page
        if (_howToPlayRightButton != null)
            _howToPlayRightButton.interactable = _currentHowToPlayPage < _howToPlayPages.Length - 1;
    }
    #endregion

    #region Settings - Audio
    private void OnBGMToggleChanged(bool isOn)
    {
        AudioManager.Instance.PlayButtonClick();

        if (AudioManager.Instance.MusicEnabled != isOn)
            AudioManager.Instance.ToggleMusic();

        GameLogger.Log($"BGM toggled: {isOn}");
    }

    private void OnSFXToggleChanged(bool isOn)
    {
        // Play click sound before changing SFX state
        if (!isOn && AudioManager.Instance.SFXEnabled)
            AudioManager.Instance.PlayButtonClick();

        if (AudioManager.Instance.SFXEnabled != isOn)
            AudioManager.Instance.ToggleSFX();

        // Play click sound after enabling SFX
        if (isOn && AudioManager.Instance.SFXEnabled)
            AudioManager.Instance.PlayButtonClick();

        GameLogger.Log($"SFX toggled: {isOn}");
    }

    private void HandleAudioSettingsChanged()
    {
        // Sync toggles with audio manager state
        if (_bgmToggle != null)
            _bgmToggle.isOn = AudioManager.Instance.MusicEnabled;
        if (_sfxToggle != null)
            _sfxToggle.isOn = AudioManager.Instance.SFXEnabled;
    }
    #endregion

    #region Settings - Speed Mode
    private void OnSpeedButtonClicked(SpeedMode mode)
    {
        // Play appropriate speed sound
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

        _currentSpeedMode = mode;
        UpdateSpeedButtonStates();

        // Trigger global speed mode change event
        GameEvents.TriggerSpeedModeChanged(mode);

        GameLogger.Log($"Speed mode changed to: {mode}");
    }

    private void HandleSpeedModeChanged(SpeedMode mode)
    {
        // Sync with external speed changes (from main speed control)
        _currentSpeedMode = mode;
        UpdateSpeedButtonStates();
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
    #endregion

    #region Animations
    private void AnimateMenuShow()
    {
        if (_mainMenuArea == null) return;

        _mainMenuArea.transform.DOKill(true);
        _mainMenuArea.transform.localScale = Vector3.zero;

        Tween scaleTween = _mainMenuArea.transform.DOScale(Vector3.one, 0.3f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetRecyclable(true);

        scaleTween.OnComplete(() => scaleTween.Kill());
    }

    private void AnimateMenuHide(System.Action onComplete = null)
    {
        if (_mainMenuArea == null)
        {
            onComplete?.Invoke();
            return;
        }

        _mainMenuArea.transform.DOKill(true);

        Tween scaleTween = _mainMenuArea.transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .SetRecyclable(true);

        scaleTween.OnComplete(() => {
            if (_mainMenuArea != null)
                _mainMenuArea.SetActive(false);
            onComplete?.Invoke();
            scaleTween.Kill();
        });
    }

    private void CleanupAnimations()
    {
        if (_mainMenuArea != null)
            _mainMenuArea.transform.DOKill(true);
    }
    #endregion

    #region Public API
    /// <summary>
    /// Check if menu is currently open
    /// </summary>
    public bool IsMenuOpen => _isMenuOpen;

    /// <summary>
    /// Programmatically close menu (useful for external systems)
    /// </summary>
    public void ForceCloseMenu()
    {
        if (_isMenuOpen)
            CloseMenu();
    }
    #endregion
}