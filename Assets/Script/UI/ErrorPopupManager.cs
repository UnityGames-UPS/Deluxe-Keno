using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class ErrorPopupManager : MonoBehaviour
{
    private static ErrorPopupManager _instance;

    public static ErrorPopupManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ErrorPopupManager>();
                if (_instance == null)
                    GameLogger.LogError("ErrorPopupManager not found in scene!");
            }
            return _instance;
        }
    }

    [Header("Error Popup")]
    [SerializeField] private GameObject _errorPopupMainPanel;
    [SerializeField] private GameObject _errorPopupArea;
    [SerializeField] private TextMeshProUGUI _errorMessageText;
    [SerializeField] private Button _errorOkButton;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (_errorPopupMainPanel != null)
            _errorPopupMainPanel.SetActive(false);

        if (_errorOkButton != null)
            _errorOkButton.onClick.AddListener(CloseError);

        if (_errorPopupMainPanel != null)
        {
            Button bgButton = _errorPopupMainPanel.GetComponent<Button>();
            if (bgButton == null) bgButton = _errorPopupMainPanel.AddComponent<Button>();
            bgButton.onClick.AddListener(CloseError);
        }
    }

    private void OnDestroy()
    {
        if (_errorOkButton != null)
            _errorOkButton.onClick.RemoveAllListeners();

        if (_errorPopupArea != null)
            _errorPopupArea.transform.DOKill(true);
    }

    public static void ShowError(string errorMessage)
    {
        if (Instance != null)
            Instance.DisplayError(errorMessage);
    }

    private void DisplayError(string errorMessage)
    {
        if (_errorMessageText != null)
            _errorMessageText.text = errorMessage;

        if (_errorPopupMainPanel != null)
            _errorPopupMainPanel.SetActive(true);

        if (_errorPopupArea != null)
        {
            _errorPopupArea.SetActive(true);
            AnimatePopupShow();
        }

        // Play error sound
        AudioManager.Instance.PlayError();
    }

    private void CloseError()
    {
        AudioManager.Instance.PlayButtonClick();

        if (_errorPopupArea != null)
        {
            AnimatePopupHide(() => {
                if (_errorPopupMainPanel != null)
                    _errorPopupMainPanel.SetActive(false);
            });
        }
        else
        {
            if (_errorPopupMainPanel != null)
                _errorPopupMainPanel.SetActive(false);
        }
    }

    private void AnimatePopupShow()
    {
        if (_errorPopupArea == null) return;

        _errorPopupArea.transform.DOKill(true);
        _errorPopupArea.transform.localScale = Vector3.zero;

        Tween scaleTween = _errorPopupArea.transform.DOScale(Vector3.one, 0.3f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetRecyclable(true);

        scaleTween.OnComplete(() => scaleTween.Kill());
    }

    private void AnimatePopupHide(System.Action onComplete = null)
    {
        if (_errorPopupArea == null)
        {
            onComplete?.Invoke();
            return;
        }

        _errorPopupArea.transform.DOKill(true);

        Tween scaleTween = _errorPopupArea.transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .SetRecyclable(true);

        scaleTween.OnComplete(() => {
            if (_errorPopupArea != null)
                _errorPopupArea.SetActive(false);
            onComplete?.Invoke();
            scaleTween.Kill();
        });
    }
}

public static class ErrorMessages
{
    public const string NO_NUMBERS_SELECTED = "Please select at least one number to play";
    public const string INSUFFICIENT_BALANCE = "Insufficient balance to place bet";
    public const string TOO_MANY_NUMBERS = "Maximum 15 numbers can be selected";
    public const string GAME_IN_PROGRESS = "Please wait for current game to finish";
    public const string CONNECTION_LOST = "Connection lost. Attempting to reconnect...";
    public const string SERVER_ERROR = "Server error occurred. Please try again";
    public const string INVALID_BET = "Please select a valid bet amount";
    public const string AUTOPLAY_NO_ROUNDS = "Please select number of rounds for auto-play";
}