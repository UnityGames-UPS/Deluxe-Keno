using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
public class QuickPickView : MonoBehaviour
{
    [Header("Quick Pick Buttons")]
    [SerializeField] private Button[] _quickPickButtons = new Button[15];
    [SerializeField] private GameObject[] _selectedImages = new GameObject[15];

    [Header("Special Buttons")]
    [SerializeField] private Button _shuffleButton;
    [SerializeField] private GameObject _shuffleButtonselected;
    [SerializeField] private GameObject _shuffleButtonicon;
    [SerializeField] private GameObject _shuffleButtonselectedicon;

    [Header("Scroll View")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Button _scrollLeftButton;
    [SerializeField] private Button _scrollRightButton;
    [SerializeField] private float _scrollSpeed = 0.3f;

    [Header("Text Colors")]
    [SerializeField] private Color _normalTextColor = Color.white;
    [SerializeField] private Color _selectedTextColor = Color.yellow;

    [Header("Scroll Sound Settings")]
    [SerializeField] private float _scrollSoundThreshold = 0.05f; // Threshold for detecting button boundary crossing

    private System.Random _random = new System.Random();
    private int _lastSelectedCount;
    private bool _isShuffleSelected = false;

    private HashSet<int> _numberSet = new HashSet<int>();

    private float _lastScrollPosition = 0f;
    private int _lastVisibleButtonCount = 0;

    private void Start()
    {
        InitializeSelectedImages();
        SetupButtonListeners();
        SubscribeToEvents();

        if (_scrollRect != null)
        {
            _scrollRect.horizontalNormalizedPosition = 0f;
            _lastScrollPosition = 0f;
        }

        StartCoroutine(InitializeScrollButtons());
    }

    private void Update()
    {
        CheckScrollBoundary();
    }

    private void InitializeSelectedImages()
    {
        for (int i = 0; i < _selectedImages.Length; i++)
        {
            if (_selectedImages[i] != null)
            {
                _selectedImages[i].SetActive(false);
            }
        }

        if (_shuffleButtonselected != null)
            _shuffleButtonselected.SetActive(false);

        if (_shuffleButtonselectedicon != null)
            _shuffleButtonselectedicon.SetActive(false);

        if (_shuffleButtonicon != null)
            _shuffleButtonicon.SetActive(true);

        _isShuffleSelected = false;
    }

    private System.Collections.IEnumerator InitializeScrollButtons()
    {
        yield return null;
        UpdateScrollButtonStates();
        _lastVisibleButtonCount = CountVisibleButtons();
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
        UnsubscribeFromEvents();
        CleanupAnimations();
    }

    private void CleanupAnimations()
    {
        foreach (var btn in _quickPickButtons)
        {
            if (btn != null)
                btn.transform.DOKill(true);
        }

        if (_shuffleButton != null)
            _shuffleButton.transform.DOKill(true);

        if (_scrollRect != null)
            DOTween.Kill(_scrollRect);
    }

    /// <summary>
    /// Animates button with punch effect without affecting scroll layout
    /// </summary>
    private void AnimateButton(Button button)
    {
        if (button == null) return;

        button.transform.DOKill(true);
        button.transform.localScale = Vector3.one;

        // Use local scale animation that doesn't trigger layout rebuild
        button.transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 5, 0.5f)
            .SetUpdate(true)
            .SetRecyclable(true)
            .OnComplete(() => {
                // Ensure scale returns to exactly 1
                button.transform.localScale = Vector3.one;
            });
    }

    private void SetupButtonListeners()
    {
        for (int i = 0; i < _quickPickButtons.Length; i++)
        {
            if (_quickPickButtons[i] != null)
            {
                int count = i + 1;
                _quickPickButtons[i].onClick.AddListener(() => OnQuickPickClicked(count));
            }
        }

        if (_shuffleButton != null)
            _shuffleButton.onClick.AddListener(OnShuffleClicked);

        if (_scrollLeftButton != null)
            _scrollLeftButton.onClick.AddListener(OnScrollLeft);

        if (_scrollRightButton != null)
            _scrollRightButton.onClick.AddListener(OnScrollRight);

        if (_scrollRect != null)
        {
            _scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }
    }

    private void RemoveButtonListeners()
    {
        for (int i = 0; i < _quickPickButtons.Length; i++)
        {
            if (_quickPickButtons[i] != null)
                _quickPickButtons[i].onClick.RemoveAllListeners();
        }

        if (_shuffleButton != null)
            _shuffleButton.onClick.RemoveAllListeners();

        if (_scrollLeftButton != null)
            _scrollLeftButton.onClick.RemoveAllListeners();

        if (_scrollRightButton != null)
            _scrollRightButton.onClick.RemoveAllListeners();

        if (_scrollRect != null)
            _scrollRect.onValueChanged.RemoveAllListeners();
    }

    private void SubscribeToEvents()
    {
        GameEvents.OnGameStarted += HandleGameStarted;
        GameEvents.OnGameEnded += HandleGameEnded;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnGameStarted -= HandleGameStarted;
        GameEvents.OnGameEnded -= HandleGameEnded;
    }

    private void OnQuickPickClicked(int count)
    {
        _lastSelectedCount = count;
        _isShuffleSelected = false;

        List<int> randomNumbers = GenerateRandomNumbers(count);
        GameEvents.TriggerQuickPickSelected(randomNumbers);

        UpdateButtonColors(count);
        UpdateSelectedImages(count);
        UpdateShuffleButtonState(false);

        AnimateButton(_quickPickButtons[count - 1]);
        AudioManager.Instance.PlayButtonClick();
    }

    private void OnShuffleClicked()
    {
        int randomCount = _random.Next(1, 16);
        _lastSelectedCount = randomCount;
        _isShuffleSelected = true;

        List<int> randomNumbers = GenerateRandomNumbers(randomCount);
        GameEvents.TriggerQuickPickSelected(randomNumbers);

        UpdateButtonColors(randomCount);
        UpdateSelectedImages(randomCount); 
        UpdateShuffleButtonState(true); 

        AnimateButton(_shuffleButton);
        AudioManager.Instance.PlayButtonClick();
    }

    private void OnScrollLeft()
    {
        if (_scrollRect != null)
        {
            Tween scrollTween = DOTween.To(() => _scrollRect.horizontalNormalizedPosition,
                       x => _scrollRect.horizontalNormalizedPosition = x,
                       0f,
                       _scrollSpeed)
                   .SetEase(Ease.OutQuad)
                   .SetUpdate(true)
                   .SetRecyclable(true);

            scrollTween.OnComplete(() => {
                UpdateScrollButtonStates();
                scrollTween.Kill();
            });
        }
    }

    private void OnScrollRight()
    {
        if (_scrollRect != null)
        {
            Tween scrollTween = DOTween.To(() => _scrollRect.horizontalNormalizedPosition,
                       x => _scrollRect.horizontalNormalizedPosition = x,
                       1f,
                       _scrollSpeed)
                   .SetEase(Ease.OutQuad)
                   .SetUpdate(true)
                   .SetRecyclable(true);

            scrollTween.OnComplete(() => {
                UpdateScrollButtonStates();
                scrollTween.Kill();
            });
        }
    }

    private void OnScrollValueChanged(Vector2 value)
    {
        UpdateScrollButtonStates();
    }

    private void CheckScrollBoundary()
    {
        if (_scrollRect == null) return;

        float currentScrollPos = _scrollRect.horizontalNormalizedPosition;
        float scrollDelta = Mathf.Abs(currentScrollPos - _lastScrollPosition);

        // Increased threshold to ignore tiny movements from button animations
        if (scrollDelta > 0.02f) // Increased from 0.05f to better filter animation noise
        {
            int currentVisibleButtons = CountVisibleButtons();

            // Play sound when the number of visible buttons changes (button entered/exited viewport)
            if (currentVisibleButtons != _lastVisibleButtonCount)
            {
                AudioManager.Instance.PlayScrollDrag();
                _lastVisibleButtonCount = currentVisibleButtons;
            }

            _lastScrollPosition = currentScrollPos;
        }
    }

    /// <summary>
    /// Counts how many buttons are currently visible in the viewport
    /// </summary>
    private int CountVisibleButtons()
    {
        if (_scrollRect == null || _scrollRect.content == null || _scrollRect.viewport == null)
            return 0;

        int visibleCount = 0;
        RectTransform viewportRect = _scrollRect.viewport;

        for (int i = 0; i < _quickPickButtons.Length; i++)
        {
            if (_quickPickButtons[i] != null)
            {
                RectTransform buttonRect = _quickPickButtons[i].GetComponent<RectTransform>();
                if (buttonRect != null && IsButtonVisible(buttonRect, viewportRect))
                {
                    visibleCount++;
                }
            }
        }

        return visibleCount;
    }

    /// <summary>
    /// Checks if a button is visible within the viewport
    /// </summary>
    private bool IsButtonVisible(RectTransform buttonRect, RectTransform viewportRect)
    {
        Vector3[] buttonCorners = new Vector3[4];
        Vector3[] viewportCorners = new Vector3[4];

        buttonRect.GetWorldCorners(buttonCorners);
        viewportRect.GetWorldCorners(viewportCorners);

        // Check if button's center is within viewport bounds
        float buttonCenterX = (buttonCorners[0].x + buttonCorners[2].x) / 2f;
        float viewportMinX = viewportCorners[0].x;
        float viewportMaxX = viewportCorners[2].x;

        return buttonCenterX >= viewportMinX && buttonCenterX <= viewportMaxX;
    }

    private void UpdateScrollButtonStates()
    {
        if (_scrollRect == null) return;

        float scrollPos = _scrollRect.horizontalNormalizedPosition;

        // Increased tolerance to prevent flickering from small animation movements
        if (_scrollLeftButton != null)
            _scrollLeftButton.interactable = scrollPos > 0.05f;

        if (_scrollRightButton != null)
            _scrollRightButton.interactable = scrollPos < 0.95f;
    }

    private List<int> GenerateRandomNumbers(int count)
    {
        _numberSet.Clear();

        while (_numberSet.Count < count)
        {
            int randomNumber = _random.Next(1, 81);
            _numberSet.Add(randomNumber);
        }

        List<int> result = ListPool<int>.Get();
        result.AddRange(_numberSet);

        return result;
    }

    private void UpdateButtonColors(int selectedCount)
    {
        for (int i = 0; i < _quickPickButtons.Length; i++)
        {
            if (_quickPickButtons[i] != null)
            {
                TextMeshProUGUI btnText = _quickPickButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.color = (i + 1 == selectedCount) ? _selectedTextColor : _normalTextColor;
                }
            }
        }
    }

    /// <summary>
    /// Updates selected images - shows the selected button's image
    /// </summary>
    private void UpdateSelectedImages(int selectedCount)
    {
        for (int i = 0; i < _selectedImages.Length; i++)
        {
            if (_selectedImages[i] != null)
            {
                _selectedImages[i].SetActive(i + 1 == selectedCount);
            }
        }
    }

    /// <summary>
    /// Updates shuffle button visual state (icons and selected image)
    /// </summary>
    private void UpdateShuffleButtonState(bool isSelected)
    {
        if (_shuffleButtonselected != null)
            _shuffleButtonselected.SetActive(isSelected);

        if (_shuffleButtonselectedicon != null)
            _shuffleButtonselectedicon.SetActive(isSelected);

        if (_shuffleButtonicon != null)
            _shuffleButtonicon.SetActive(!isSelected);
    }

    private void HandleGameStarted()
    {
        SetButtonsInteractable(false);
    }

    private void HandleGameEnded()
    {
        SetButtonsInteractable(true);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        foreach (Button btn in _quickPickButtons)
        {
            if (btn != null)
                btn.interactable = interactable;
        }

        if (_shuffleButton != null)
            _shuffleButton.interactable = interactable;

        if (_scrollLeftButton != null)
            _scrollLeftButton.interactable = interactable && _scrollRect.horizontalNormalizedPosition > 0.05f;

        if (_scrollRightButton != null)
            _scrollRightButton.interactable = interactable && _scrollRect.horizontalNormalizedPosition < 0.95f;
    }
}