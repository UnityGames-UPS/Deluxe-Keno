using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// FIXED: Scroll buttons now show correct state on start
/// Left button should be disabled at start, right button should be enabled
/// NEW: Added scroll sound effects - single sound for buttons, continuous for dragging
/// </summary>
public class QuickPickView : MonoBehaviour
{
    [Header("Quick Pick Buttons")]
    [SerializeField] private Button[] _quickPickButtons = new Button[15];

    [Header("Special Buttons")]
    [SerializeField] private Button _shuffleButton;

    [Header("Scroll View")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Button _scrollLeftButton;
    [SerializeField] private Button _scrollRightButton;
    [SerializeField] private float _scrollSpeed = 0.3f;

    [Header("Text Colors")]
    [SerializeField] private Color _normalTextColor = Color.white;
    [SerializeField] private Color _selectedTextColor = Color.yellow;

    [Header("Scroll Sound Settings")]
    [SerializeField] private float _scrollSoundThreshold = 0.01f; // Minimum movement to trigger sound

    private System.Random _random = new System.Random();
    private int _lastSelectedCount;

    // OPTIMIZATION: Reuse HashSet to avoid allocations
    private HashSet<int> _numberSet = new HashSet<int>();

    // NEW: Scroll drag tracking
    private bool _isDragging = false;
    private float _lastScrollPosition = 0f;
    private float _scrollSoundTimer = 0f;
    private const float SCROLL_SOUND_INTERVAL = 0.1f; // Play sound every 0.1 seconds while dragging

    private void Start()
    {
        SetupButtonListeners();
        SubscribeToEvents();

        // FIXED: Initialize scroll state properly on start
        // Force scroll to leftmost position
        if (_scrollRect != null)
        {
            _scrollRect.horizontalNormalizedPosition = 0f;
            _lastScrollPosition = 0f;
        }

        // Wait one frame for layout to settle, then update button states
        StartCoroutine(InitializeScrollButtons());
    }

    private void Update()
    {
        // NEW: Handle continuous scroll sound while dragging
        if (_isDragging && _scrollRect != null)
        {
            float currentPos = _scrollRect.horizontalNormalizedPosition;
            float movement = Mathf.Abs(currentPos - _lastScrollPosition);

            if (movement > _scrollSoundThreshold)
            {
                _scrollSoundTimer += Time.deltaTime;

                if (_scrollSoundTimer >= SCROLL_SOUND_INTERVAL)
                {
                    AudioManager.Instance.PlayScrollDrag();
                    _scrollSoundTimer = 0f;
                }
            }

            _lastScrollPosition = currentPos;
        }
    }

    // FIXED: Initialize scroll buttons after layout is ready
    private System.Collections.IEnumerator InitializeScrollButtons()
    {
        yield return null; // Wait one frame for layout
        UpdateScrollButtonStates();
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
    }

    private void AnimateButton(Button button)
    {
        if (button == null) return;

        button.transform.DOKill(true);
        button.transform.localScale = Vector3.one;

        Tween punchTween = button.transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 5, 0.5f)
            .SetUpdate(true)
            .SetRecyclable(true);

        punchTween.OnComplete(() => punchTween.Kill());
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

            // NEW: Add event triggers for drag detection
            UnityEngine.EventSystems.EventTrigger trigger = _scrollRect.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null)
                trigger = _scrollRect.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            // Begin drag
            UnityEngine.EventSystems.EventTrigger.Entry beginDragEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            beginDragEntry.eventID = UnityEngine.EventSystems.EventTriggerType.BeginDrag;
            beginDragEntry.callback.AddListener((data) => OnBeginDrag());
            trigger.triggers.Add(beginDragEntry);

            // End drag
            UnityEngine.EventSystems.EventTrigger.Entry endDragEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            endDragEntry.eventID = UnityEngine.EventSystems.EventTriggerType.EndDrag;
            endDragEntry.callback.AddListener((data) => OnEndDrag());
            trigger.triggers.Add(endDragEntry);
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

    // NEW: Drag detection handlers
    private void OnBeginDrag()
    {
        _isDragging = true;
        _scrollSoundTimer = 0f;
        if (_scrollRect != null)
            _lastScrollPosition = _scrollRect.horizontalNormalizedPosition;
    }

    private void OnEndDrag()
    {
        _isDragging = false;
        _scrollSoundTimer = 0f;
    }

    private void OnQuickPickClicked(int count)
    {
        _lastSelectedCount = count;
        List<int> randomNumbers = GenerateRandomNumbers(count);
        GameEvents.TriggerQuickPickSelected(randomNumbers);
        UpdateButtonColors(count);
        AnimateButton(_quickPickButtons[count - 1]);
        AudioManager.Instance.PlayButtonClick();
    }

    private void OnShuffleClicked()
    {
        int randomCount = _random.Next(1, 16);
        _lastSelectedCount = randomCount;

        List<int> randomNumbers = GenerateRandomNumbers(randomCount);
        GameEvents.TriggerQuickPickSelected(randomNumbers);
        // UpdateButtonColors(randomCount);
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

            // NEW: Play single scroll button sound
            AudioManager.Instance.PlayScrollButton();
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

            // NEW: Play single scroll button sound
            AudioManager.Instance.PlayScrollButton();
        }
    }

    private void OnScrollValueChanged(Vector2 value)
    {
        UpdateScrollButtonStates();
    }

    // FIXED: Proper scroll button state logic
    // At position 0 (leftmost): Left button disabled, Right button enabled
    // At position 1 (rightmost): Left button enabled, Right button disabled
    private void UpdateScrollButtonStates()
    {
        if (_scrollRect == null) return;

        float scrollPos = _scrollRect.horizontalNormalizedPosition;

        // FIXED: Left button disabled when at leftmost position (0)
        if (_scrollLeftButton != null)
            _scrollLeftButton.interactable = scrollPos > 0.01f;

        // FIXED: Right button enabled when at leftmost position (0)
        if (_scrollRightButton != null)
            _scrollRightButton.interactable = scrollPos < 0.99f;
    }

    // OPTIMIZED: Reuse HashSet instead of creating new ones
    private List<int> GenerateRandomNumbers(int count)
    {
        _numberSet.Clear();

        while (_numberSet.Count < count)
        {
            int randomNumber = _random.Next(1, 81);
            _numberSet.Add(randomNumber);
        }

        // Use pooled list
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
                    btnText.color = (i + 1 == selectedCount) ? _selectedTextColor : _normalTextColor;
            }
        }
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
            _scrollLeftButton.interactable = interactable && _scrollRect.horizontalNormalizedPosition > 0.01f;

        if (_scrollRightButton != null)
            _scrollRightButton.interactable = interactable && _scrollRect.horizontalNormalizedPosition < 0.99f;
    }
}