using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

[RequireComponent(typeof(Button))]
public class NumberButton : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private int _number;

    [Header("UI References")]
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _numberText;

    [Header("Visual States")]
    [SerializeField] private GameObject _normalState;
    [SerializeField] private GameObject _selectedState;
    [SerializeField] private GameObject _drawnState;
    [SerializeField] private GameObject _winState;

    [Header("Text Colors")]
    [SerializeField] private Color _normalTextColor = Color.white;
    [SerializeField] private Color _selectedTextColor = Color.yellow;
    [SerializeField] private Color _drawnTextColor = Color.cyan;
    [SerializeField] private Color _winTextColor = Color.green;

    private bool _isSelected;
    private bool _isDrawn;
    private bool _isWinning;

    // OPTIMIZATION: Cache number string to avoid ToString() calls
    private string _numberString;

    public int Number => _number;

    private void Awake()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        if (_numberText == null)
            _numberText = GetComponentInChildren<TextMeshProUGUI>();

        _button.onClick.AddListener(OnButtonClicked);
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnButtonClicked);

        transform.DOKill(true);
    }

    public void Initialize(int number)
    {
        _number = number;
        _numberString = number.ToString(); // Cache string

        if (_numberText != null)
            _numberText.text = _numberString;

        ResetState();
    }

    private void OnButtonClicked()
    {
        // FIXED: If button has drawn/win state, clear it first and toggle selection
        if (_isDrawn || _isWinning)
        {
            // Clear the drawn/win visual states
            _isDrawn = false;
            _isWinning = false;

            // After clearing drawn/win, toggle selection based on current _isSelected state
            if (_isSelected)
            {
                // Was selected before drawing - deselect it
                GameEvents.TriggerNumberDeselected(_number);
            }
            else
            {
                // Was not selected - select it
                GameEvents.TriggerNumberSelected(_number);
            }
        }
        else
        {
            // Normal behavior - no drawn/win state
            if (_isSelected)
            {
                GameEvents.TriggerNumberDeselected(_number);
            }
            else
            {
                GameEvents.TriggerNumberSelected(_number);
            }
        }

        AnimateButtonPress();
        AudioManager.Instance.PlayButtonClick();
    }

    public void Select()
    {
        _isSelected = true;
        UpdateVisualState();
    }

    public void Deselect()
    {
        transform.DOKill(true);
        _isSelected = false;
        _isDrawn = false;
        _isWinning = false;
        UpdateVisualState();
    }

    public void ClearDrawnState()
    {
        _isDrawn = false;
        _isWinning = false;
        UpdateVisualState();
    }

    public void MarkAsDrawn()
    {
        _isDrawn = true;
        UpdateVisualState();
        AnimateDrawn();
    }

    public void MarkAsWinning()
    {
        _isWinning = true;
        _isDrawn = true;
        UpdateVisualState();
        AnimateWin();
    }

    public void ResetState()
    {
        transform.DOKill(true);
        _isSelected = false;
        _isDrawn = false;
        _isWinning = false;
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (_normalState != null)
            _normalState.SetActive(!_isSelected && !_isDrawn && !_isWinning);

        if (_selectedState != null)
            _selectedState.SetActive(_isSelected && !_isDrawn && !_isWinning);

        if (_drawnState != null)
            _drawnState.SetActive(_isDrawn && !_isWinning);

        if (_winState != null)
            _winState.SetActive(_isWinning);

        if (_numberText != null)
        {
            if (_isWinning)
                _numberText.color = _winTextColor;
            else if (_isDrawn)
                _numberText.color = _drawnTextColor;
            else if (_isSelected)
                _numberText.color = _selectedTextColor;
            else
                _numberText.color = _normalTextColor;
        }
    }

    private void AnimateButtonPress()
    {
        transform.DOKill(true);
        transform.localScale = Vector3.one;

        Tween punchTween = transform.DOPunchScale(Vector3.one * 0.1f, GameConfig.BUTTON_SCALE_DURATION, 1, 0.5f)
            .SetUpdate(true)
            .SetRecyclable(true);

        punchTween.OnComplete(() => punchTween.Kill());
    }

    private void AnimateDrawn()
    {
        transform.DOKill(true);

        Tween punchTween = transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 3, 0.5f)
            .SetUpdate(true)
            .SetRecyclable(true);

        punchTween.OnComplete(() => punchTween.Kill());
    }

    private void AnimateWin()
    {
        transform.DOKill(true);

        Sequence winSeq = DOTween.Sequence()
            .SetRecyclable(true)
            .SetUpdate(true);

        winSeq.Append(transform.DOScale(GameConfig.BUTTON_SCALE_AMOUNT * 1.2f, 0.25f).SetEase(Ease.OutBack));
        winSeq.Append(transform.DOScale(1f, 0.25f).SetEase(Ease.InBack));
        winSeq.Join(transform.DORotate(new Vector3(0, 0, 10), 0.1f).SetLoops(2, LoopType.Yoyo));

        winSeq.OnComplete(() => winSeq.Kill());
    }

    public void SetInteractable(bool interactable)
    {
        _button.interactable = interactable;
    }
}