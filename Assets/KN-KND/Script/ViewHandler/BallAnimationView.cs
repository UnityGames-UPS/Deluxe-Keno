using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class BallAnimationView : MonoBehaviour
{
    [System.Serializable]
    public class BallData
    {
        public GameObject ballObject;
        public GameObject winImage;
        public TextMeshProUGUI numberText;
    }

    [Header("Ball References")]
    [SerializeField] private List<BallData> _balls = new List<BallData>();

    [Header("L-Shape Tube Points")]
    [SerializeField] private Transform _startPoint;
    [SerializeField] private Transform _centerPoint;
    [SerializeField] private Transform _endPoint;

    [Header("Animation Timing")]
    [SerializeField] private float _verticalFallDuration = 0.4f;
    [SerializeField] private float _horizontalRollDuration = 0.5f;
    [SerializeField] private float _bounceDuration = 0.15f;

    [Header("Ball Properties")]
    [SerializeField] private float _ballRadius = 0.5f;
    [SerializeField] private float _ballSpacing = 0.1f;
    [SerializeField] private float _bounceAmount = 0.1f;

    [Header("Speed Settings")]
    [SerializeField] private float _normalDelay = 0.35f;
    [SerializeField] private float _turboDelay = 0.15f;
    [SerializeField] private float _turboMultiplier = 1.2f;
    [SerializeField] private float _instantMultiplier = 1.3f;

    private SpeedMode _currentSpeedMode = SpeedMode.Normal;
    private Coroutine _mainAnimationCoroutine;
    private GameResultData _currentResult;
    private int _currentBallIndex;
    private bool _isAnimating;
    private bool _speedModeChangeRequested;
    private bool _isIntroActive;
    private bool _hasCompletedThisRound; // NEW: Track if we've already triggered completion

    private HashSet<int> _winningNumbersSet = new HashSet<int>();

    private Vector3 _startPointLocal;
    private Vector3 _centerPointLocal;
    private Vector3 _endPointLocal;
    private Transform _animationParent;

    private void Start()
    {
        SubscribeToEvents();
        CacheLocalReferencePoints();
        HideAllBalls();
        StartCoroutine(MonitorOrientationChanges());
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private IEnumerator MonitorOrientationChanges()
    {
        ScreenOrientation lastOrientation = Screen.orientation;

        while (true)
        {
            yield return new WaitForSeconds(0.5f);

            if (Screen.orientation != lastOrientation)
            {
                lastOrientation = Screen.orientation;
                CacheLocalReferencePoints();
            }
        }
    }

    private void CacheLocalReferencePoints()
    {
        if (_startPoint == null || _centerPoint == null || _endPoint == null) return;

        if (_balls.Count > 0 && _balls[0].ballObject != null)
            _animationParent = _balls[0].ballObject.transform.parent;
        else
            _animationParent = _startPoint.parent;

        if (_animationParent == null)
        {
            _startPointLocal = _startPoint.position;
            _centerPointLocal = _centerPoint.position;
            _endPointLocal = _endPoint.position;
        }
        else
        {
            _startPointLocal = _animationParent.InverseTransformPoint(_startPoint.position);
            _centerPointLocal = _animationParent.InverseTransformPoint(_centerPoint.position);
            _endPointLocal = _animationParent.InverseTransformPoint(_endPoint.position);
        }
    }

    private void SubscribeToEvents()
    {
        GameEvents.OnGameResultReceived += HandleGameResult;
        GameEvents.OnGameStarted += HandleGameStarted;
        GameEvents.OnSpeedModeChanged += HandleSpeedModeChanged;
        GameEvents.OnIntroAnimationStarted += () => { _isIntroActive = true; HideAllBalls(); };
        GameEvents.OnIntroAnimationCompleted += () => _isIntroActive = false;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnGameResultReceived -= HandleGameResult;
        GameEvents.OnGameStarted -= HandleGameStarted;
        GameEvents.OnSpeedModeChanged -= HandleSpeedModeChanged;
    }

    private void HandleGameStarted()
    {
        if (_isIntroActive) return;
        StopCurrentAnimation();
        HideAllBalls();
        ResetState();
    }

    private void HandleGameResult(GameResultData result)
    {
        if (_isIntroActive) return;

        _currentResult = result;
        _currentBallIndex = 0;
        _isAnimating = true;
        _speedModeChangeRequested = false;
        _hasCompletedThisRound = false; // NEW: Reset completion flag for new round

        _winningNumbersSet.Clear();
        foreach (int hit in result.hits)
            _winningNumbersSet.Add(hit);

        StopCurrentAnimation();
        _mainAnimationCoroutine = StartCoroutine(AnimateDrawSequence(result));
    }

    private void HandleSpeedModeChanged(SpeedMode mode)
    {
        _currentSpeedMode = mode;
        DOTween.timeScale = GetSpeedMultiplier();

        if (_isAnimating && _currentResult != null && !_speedModeChangeRequested)
            _speedModeChangeRequested = true;
    }

    private IEnumerator AnimateDrawSequence(GameResultData result, int startIndex = 0)
    {
        _speedModeChangeRequested = false;

        if (_currentSpeedMode == SpeedMode.Instant)
            yield return AnimateInstantMode(result, startIndex);
        else
            yield return AnimateNormalMode(result, startIndex);

        // NEW: Only trigger completion once per round
        if (!_hasCompletedThisRound)
        {
            _hasCompletedThisRound = true;
            CompleteAnimation(result);
        }
    }

    private IEnumerator AnimateInstantMode(GameResultData result, int startIndex)
    {
        for (int i = startIndex; i < result.drawn.Count && i < _balls.Count; i++)
        {
            _currentBallIndex = i;
            int number = result.drawn[i];
            bool isWin = _winningNumbersSet.Contains(number);

            AudioManager.Instance.PlayBallFalling();
            StartCoroutine(AnimateSingleBallAsync(i, number, isWin));
            GameEvents.TriggerBallDrawn(number, isWin);

            if (isWin)
                AudioManager.Instance.PlayWinBall();

            yield return new WaitForSeconds(0.02f);
        }

        float totalDuration = (_verticalFallDuration + _horizontalRollDuration) / GetSpeedMultiplier() + 0.5f;
        yield return new WaitForSeconds(totalDuration);
    }

    private IEnumerator AnimateNormalMode(GameResultData result, int startIndex)
    {
        for (int i = startIndex; i < result.drawn.Count && i < _balls.Count; i++)
        {
            if (_speedModeChangeRequested)
            {
                _speedModeChangeRequested = false;
                // NEW: Don't trigger completion when restarting animation
                yield return AnimateDrawSequenceInternal(result, i);
                yield break;
            }

            _currentBallIndex = i;
            int number = result.drawn[i];
            bool isWin = _winningNumbersSet.Contains(number);

            BallData ball = _balls[i];
            PrepareBall(ball, number);
            Vector3 finalPosLocal = CalculateFinalPositionLocal(i);

            AudioManager.Instance.PlayBallFalling();

            yield return AnimateVerticalFallLocal(ball, finalPosLocal);
            yield return AnimateHorizontalRollLocal(ball, finalPosLocal);
            yield return AnimateBounceLocal(ball, finalPosLocal);
            yield return AnimateSettle(ball);

            if (isWin)
            {
                AnimateWinEffect(ball);
                AudioManager.Instance.PlayWinBall();
            }

            GameEvents.TriggerBallDrawn(number, isWin);
            yield return new WaitForSeconds(GetCurrentDelay());
        }
    }

    // NEW: Internal method that doesn't trigger completion
    private IEnumerator AnimateDrawSequenceInternal(GameResultData result, int startIndex)
    {
        _speedModeChangeRequested = false;

        if (_currentSpeedMode == SpeedMode.Instant)
            yield return AnimateInstantMode(result, startIndex);
        else
            yield return AnimateNormalMode(result, startIndex);

        // Don't call CompleteAnimation here - already handled by parent
    }

    private IEnumerator AnimateSingleBallAsync(int ballIndex, int number, bool isWinning)
    {
        BallData ball = _balls[ballIndex];
        PrepareBall(ball, number);
        Vector3 finalPosLocal = CalculateFinalPositionLocal(ballIndex);

        yield return AnimateVerticalFallLocal(ball, finalPosLocal);
        yield return AnimateHorizontalRollLocal(ball, finalPosLocal);
        yield return AnimateBounceLocal(ball, finalPosLocal);
        yield return AnimateSettle(ball);

        if (isWinning)
            AnimateWinEffect(ball);
    }

    private void CompleteAnimation(GameResultData result)
    {
        _isAnimating = false;
        StartCoroutine(ShowWinPopupAfterDelay(result));
    }

    private IEnumerator ShowWinPopupAfterDelay(GameResultData result)
    {
        yield return new WaitForSeconds(0.5f);

        if (result.currentWinning > 0)
        {
            GameEvents.TriggerShowWinPopup(result.currentWinning);
            AudioManager.Instance.PlayWinPopup();
        }

        GameEvents.TriggerAnimationCompleted();
    }

    private void PrepareBall(BallData ball, int number)
    {
        ball.ballObject.SetActive(true);
        ball.ballObject.transform.localPosition = _startPointLocal;
        ball.ballObject.transform.localRotation = Quaternion.identity;

        if (ball.numberText != null)
        {
            ball.numberText.text = number.ToString();
            ball.numberText.transform.localRotation = Quaternion.identity;
        }

        if (ball.winImage != null)
            ball.winImage.SetActive(false);
    }

    private IEnumerator AnimateVerticalFallLocal(BallData ball, Vector3 finalPosLocal)
    {
        float vertDist = Vector3.Distance(_startPointLocal, _centerPointLocal);
        float vertRotations = vertDist / (_ballRadius * 2f * Mathf.PI);
        float vertRotation = -(vertRotations * 360f);

        Sequence vertSeq = DOTween.Sequence().SetRecyclable(true).SetUpdate(true);
        vertSeq.Append(ball.ballObject.transform.DOLocalMove(_centerPointLocal, _verticalFallDuration).SetEase(Ease.InQuad));
        vertSeq.Join(ball.ballObject.transform.DORotate(new Vector3(0, 0, vertRotation), _verticalFallDuration, RotateMode.FastBeyond360).SetEase(Ease.Linear));

        yield return vertSeq.WaitForCompletion();
        vertSeq.Kill();
    }

    private IEnumerator AnimateHorizontalRollLocal(BallData ball, Vector3 finalPosLocal)
    {
        float horzDist = Vector3.Distance(_centerPointLocal, finalPosLocal);
        float horzRotations = horzDist / (_ballRadius * 2f * Mathf.PI);
        float naturalRoll = -(horzRotations * 360f);

        float currentRotation = ball.ballObject.transform.rotation.eulerAngles.z;
        float afterRoll = (currentRotation + naturalRoll) % 360f;
        if (afterRoll < 0) afterRoll += 360f;

        float adjustment = afterRoll <= 180f ? -afterRoll : 360f - afterRoll;
        float finalRotation = currentRotation + naturalRoll + adjustment;

        Sequence horzSeq = DOTween.Sequence().SetRecyclable(true).SetUpdate(true);
        horzSeq.Append(ball.ballObject.transform.DOLocalMove(finalPosLocal, _horizontalRollDuration).SetEase(Ease.OutQuad));
        horzSeq.Join(ball.ballObject.transform.DORotate(new Vector3(0, 0, finalRotation), _horizontalRollDuration, RotateMode.FastBeyond360).SetEase(Ease.Linear));

        yield return horzSeq.WaitForCompletion();
        horzSeq.Kill();
    }

    private IEnumerator AnimateBounceLocal(BallData ball, Vector3 finalPosLocal)
    {
        Vector3 bounceDir = (_centerPointLocal - _endPointLocal).normalized;
        bounceDir.y = 0;
        Vector3 bouncePosLocal = finalPosLocal + bounceDir * _bounceAmount;
        bouncePosLocal.y = finalPosLocal.y;

        Sequence bounceSeq = DOTween.Sequence().SetRecyclable(true).SetUpdate(true);
        bounceSeq.Append(ball.ballObject.transform.DOLocalMove(bouncePosLocal, _bounceDuration * 0.5f).SetEase(Ease.OutQuad));
        bounceSeq.Append(ball.ballObject.transform.DOLocalMove(finalPosLocal, _bounceDuration * 0.5f).SetEase(Ease.InOutQuad));

        yield return bounceSeq.WaitForCompletion();
        bounceSeq.Kill();
    }

    private IEnumerator AnimateSettle(BallData ball)
    {
        float tilt = Random.Range(-3f, 3f);
        Tween settleTween = ball.ballObject.transform.DOLocalRotate(new Vector3(0, 0, tilt), 0.2f).SetEase(Ease.OutQuad).SetUpdate(true).SetRecyclable(true);
        yield return new WaitForSeconds(0.2f);
        settleTween.Kill();
    }

    private void AnimateWinEffect(BallData ball)
    {
        if (ball.winImage == null) return;

        ball.winImage.SetActive(true);

        Sequence winSeq = DOTween.Sequence().SetRecyclable(true).SetUpdate(true);
        winSeq.Append(ball.winImage.transform.DOScale(1.2f, 0.12f));
        winSeq.Append(ball.winImage.transform.DOScale(1f, 0.12f));
        winSeq.SetLoops(2);
        winSeq.OnComplete(() => winSeq.Kill());
    }

    private Vector3 CalculateFinalPositionLocal(int ballIndex)
    {
        Vector3 directionLocal = (_endPointLocal - _centerPointLocal).normalized;
        float offset = ballIndex * ((_ballRadius * 2f) + _ballSpacing);
        Vector3 posLocal = _endPointLocal - directionLocal * offset;
        posLocal.y = _endPointLocal.y;
        return posLocal;
    }

    private float GetSpeedMultiplier() => _currentSpeedMode switch
    {
        SpeedMode.Turbo => _turboMultiplier,
        SpeedMode.Instant => _instantMultiplier,
        _ => 1f
    };

    private float GetCurrentDelay() => _currentSpeedMode switch
    {
        SpeedMode.Turbo => _turboDelay,
        SpeedMode.Instant => 0f,
        _ => _normalDelay
    };

    private void HideAllBalls()
    {
        foreach (var ball in _balls)
        {
            if (ball.ballObject != null)
                ball.ballObject.SetActive(false);
        }
    }

    private void ResetState()
    {
        _isAnimating = false;
        _currentBallIndex = 0;
        _currentResult = null;
        _speedModeChangeRequested = false;
        _hasCompletedThisRound = false; // NEW: Reset completion flag
        _winningNumbersSet.Clear();
    }

    private void StopCurrentAnimation()
    {
        if (_mainAnimationCoroutine != null)
        {
            StopCoroutine(_mainAnimationCoroutine);
            _mainAnimationCoroutine = null;
        }

        foreach (var ball in _balls)
        {
            if (ball.ballObject != null)
                ball.ballObject.transform.DOKill(true);
            if (ball.winImage != null)
                ball.winImage.transform.DOKill(true);
        }
    }

    private void Cleanup()
    {
        UnsubscribeFromEvents();
        StopCurrentAnimation();
        StopAllCoroutines();

        DOTween.Kill(transform);
        foreach (var ball in _balls)
        {
            if (ball.ballObject != null)
                DOTween.Kill(ball.ballObject.transform);
            if (ball.winImage != null)
                DOTween.Kill(ball.winImage.transform);
        }

        _winningNumbersSet.Clear();
    }
}