using UnityEngine;
using DG.Tweening;
using System.Collections;


public class IntroAnimationController : MonoBehaviour
{
    [Header("Animation Elements")]
    [SerializeField] private RectTransform _introMainPanel;
    [SerializeField] private RectTransform _mainArea;
    [SerializeField] private Transform[] _balls = new Transform[3];
    [SerializeField] private GameObject _ballContainer;
    [SerializeField] private RectTransform _textTransform;

    [Header("Animation Settings")]
    [SerializeField] private float _mainAreaDropDuration = 0.8f;
    [SerializeField] private float _mainAreaPopScale = 1.05f;
    [SerializeField] private float _ballDropDuration = 0.6f;
    [SerializeField] private float _ballDropDelay = 0.15f;
    [SerializeField] private float _textFadeDuration = 0.4f;
    [SerializeField] private float _textPopScale = 1.1f;

    [Header("Position Settings")]
    [SerializeField] private float _mainAreaStartOffsetY = 1500f;
    [SerializeField] private float _ballStartOffsetY = 800f;

    [Header("Audio Sync")]
    [SerializeField] private float _audioDelayToMatchAnimation = 0.5f;

    private bool _isPlaying;
    private bool _hasPlayedOnce;
    private Sequence _currentSequence;

    private Vector2 _mainAreaOriginalPos;
    private Vector3[] _ballsOriginalPos;
    private Vector3 _textOriginalScale;

    private void Start()
    {
        SubscribeToEvents();
        StoreOriginalPositions();
        PrepareIntro();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        CleanupTweens();
    }

    private void CleanupTweens()
    {
        if (_currentSequence != null)
        {
            _currentSequence.Kill(true);
            _currentSequence = null;
        }

        if (_mainArea != null)
            _mainArea.DOKill(true);

        if (_textTransform != null)
            _textTransform.DOKill(true);

        foreach (var ball in _balls)
        {
            if (ball != null)
                ball.DOKill(true);
        }
    }

    private void SubscribeToEvents()
    {
        GameEvents.OnPlayButtonClicked += HandlePlayButtonClicked;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnPlayButtonClicked -= HandlePlayButtonClicked;
    }

    private void StoreOriginalPositions()
    {
        if (_mainArea != null)
            _mainAreaOriginalPos = _mainArea.anchoredPosition;

        _ballsOriginalPos = new Vector3[_balls.Length];
        for (int i = 0; i < _balls.Length; i++)
        {
            if (_balls[i] != null)
                _ballsOriginalPos[i] = _balls[i].localPosition;
        }

        if (_textTransform != null)
            _textOriginalScale = _textTransform.localScale;
    }

    private void PrepareIntro()
    {
        if (_mainArea != null)
        {
            Vector2 startPos = _mainAreaOriginalPos;
            startPos.y += _mainAreaStartOffsetY;
            _mainArea.anchoredPosition = startPos;
        }

        if (_ballContainer != null)
            _ballContainer.SetActive(false);

        for (int i = 0; i < _balls.Length; i++)
        {
            if (_balls[i] != null)
            {
                Vector3 pos = _ballsOriginalPos[i];
                pos.y += _ballStartOffsetY;
                _balls[i].localPosition = pos;
            }
        }

        if (_textTransform != null)
            _textTransform.localScale = Vector3.zero;

        if (_introMainPanel != null)
            _introMainPanel.gameObject.SetActive(true);

        StartCoroutine(PlayIntroAfterDelay(0.5f));
    }

    private IEnumerator PlayIntroAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayIntro();
    }

    public void PlayIntro()
    {
        if (_isPlaying) return;

        _isPlaying = true;
        _hasPlayedOnce = true;

        GameEvents.TriggerIntroAnimationStarted();

        // FIXED: Start animation immediately, then play audio with a slight delay to sync
        StartCoroutine(PlayAudioAfterDelay(_audioDelayToMatchAnimation));

        _currentSequence = DOTween.Sequence()
            .SetRecyclable(true)
            .SetUpdate(true);

        // Main area drop - STARTS IMMEDIATELY
        if (_mainArea != null)
        {
            _currentSequence.Append(_mainArea.DOAnchorPos(_mainAreaOriginalPos, _mainAreaDropDuration).SetEase(Ease.OutBounce));
            _currentSequence.Append(_mainArea.DOScale(_mainAreaPopScale, 0.15f).SetEase(Ease.OutQuad));
            _currentSequence.Append(_mainArea.DOScale(1f, 0.15f).SetEase(Ease.InQuad));
        }

        // Balls drop
        for (int i = 0; i < _balls.Length; i++)
        {
            if (_balls[i] != null)
            {
                Vector3 finalPos = _ballsOriginalPos[i];
                _currentSequence.Insert(
                    _mainAreaDropDuration + (i * _ballDropDelay),
                    _balls[i].DOLocalMove(finalPos, _ballDropDuration).SetEase(Ease.OutBounce)
                );
            }
        }

        // Text animation
        float textStartTime = _mainAreaDropDuration + (_balls.Length * _ballDropDelay) + _ballDropDuration + 0.1f;

        if (_textTransform != null)
        {
            _currentSequence.Insert(textStartTime, _textTransform.DOScale(_textOriginalScale, _textFadeDuration).SetEase(Ease.OutBack));
            _currentSequence.Append(_textTransform.DOScale(_textOriginalScale * _textPopScale, 0.2f).SetEase(Ease.OutQuad));
            _currentSequence.Append(_textTransform.DOScale(_textOriginalScale, 0.2f).SetEase(Ease.InQuad));
        }

        _currentSequence.OnComplete(() => {
            _isPlaying = false;
            GameEvents.TriggerIntroAnimationCompleted();
            _currentSequence.Kill();
            _currentSequence = null;
        });
    }

    // FIXED: Play audio with delay to match animation perfectly
    private IEnumerator PlayAudioAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        AudioManager.Instance.PlayGameStart();
    }

    private void HandlePlayButtonClicked()
    {
        if (!_hasPlayedOnce)
            _hasPlayedOnce = true;

        if (_introMainPanel != null && _introMainPanel.gameObject.activeSelf)
        {
            HideIntroPanel();

            if (_ballContainer != null)
                _ballContainer.SetActive(true);
        }
    }

    private void HideIntroPanel()
    {
        if (_currentSequence != null)
        {
            _currentSequence.Kill(true);
            _currentSequence = null;
        }

        _isPlaying = false;

        if (_introMainPanel != null)
        {
            Tween hideTween = _introMainPanel.DOScale(0f, 0.2f)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .SetRecyclable(true);

            hideTween.OnComplete(() => {
                _introMainPanel.gameObject.SetActive(false);
                GameEvents.TriggerIntroAnimationCompleted();
                hideTween.Kill();
            });
        }
    }

    public void ResetAndPlayIntro()
    {
        _hasPlayedOnce = false;

        if (_introMainPanel != null)
        {
            _introMainPanel.gameObject.SetActive(true);
            _introMainPanel.localScale = Vector3.one;
        }

        if (_ballContainer != null)
            _ballContainer.SetActive(false);

        PrepareIntro();
    }
}