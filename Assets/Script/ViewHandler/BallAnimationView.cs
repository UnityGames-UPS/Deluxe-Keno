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

    // Optimization: Cached HashSet for faster lookup
    private HashSet<int> _winningNumbersSet = new HashSet<int>();

    private void Start()
    {
        SubscribeToEvents();
        HideAllBalls();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void SubscribeToEvents()
    {
        GameEvents.OnGameResultReceived += HandleGameResult;
        GameEvents.OnGameStarted += HandleGameStarted;
        GameEvents.OnSpeedModeChanged += HandleSpeedModeChanged;
        GameEvents.OnIntroAnimationStarted += HandleIntroStarted;
        GameEvents.OnIntroAnimationCompleted += HandleIntroCompleted;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnGameResultReceived -= HandleGameResult;
        GameEvents.OnGameStarted -= HandleGameStarted;
        GameEvents.OnSpeedModeChanged -= HandleSpeedModeChanged;
        GameEvents.OnIntroAnimationStarted -= HandleIntroStarted;
        GameEvents.OnIntroAnimationCompleted -= HandleIntroCompleted;
    }

    private void HandleIntroStarted()
    {
        _isIntroActive = true;
        HideAllBalls();
    }

    private void HandleIntroCompleted()
    {
        _isIntroActive = false;
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

        // Optimization: Build HashSet once for O(1) lookups
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
        {
            _speedModeChangeRequested = true;
        }
    }

    // OPTIMIZED: Simplified coroutine - no nested coroutines
    private IEnumerator AnimateDrawSequence(GameResultData result, int startIndex = 0)
    {
        _speedModeChangeRequested = false;

        if (_currentSpeedMode == SpeedMode.Instant)
        {
            yield return AnimateInstantMode(result, startIndex);
        }
        else
        {
            yield return AnimateNormalMode(result, startIndex);
        }

        CompleteAnimation(result);
    }

    // FIXED: Play ball falling sound for each ball in instant mode (fast)
    private IEnumerator AnimateInstantMode(GameResultData result, int startIndex)
    {
        for (int i = startIndex; i < result.drawn.Count && i < _balls.Count; i++)
        {
            _currentBallIndex = i;
            int number = result.drawn[i];
            bool isWin = _winningNumbersSet.Contains(number);

            // FIXED: Play ball falling sound FAST for each ball in instant mode
            AudioManager.Instance.PlayBallFalling();

            // Fire and forget - no waiting
            StartCoroutine(AnimateSingleBallAsync(i, number, isWin));
            GameEvents.TriggerBallDrawn(number, isWin);

            if (isWin)
                AudioManager.Instance.PlayWinBall();

            yield return new WaitForSeconds(0.02f);
        }

        float totalDuration = (_verticalFallDuration + _horizontalRollDuration) / GetSpeedMultiplier() + 0.5f;
        yield return new WaitForSeconds(totalDuration);
    }

    // OPTIMIZED: Inlined animation, no nested StartCoroutine
    private IEnumerator AnimateNormalMode(GameResultData result, int startIndex)
    {
        for (int i = startIndex; i < result.drawn.Count && i < _balls.Count; i++)
        {
            if (_speedModeChangeRequested)
            {
                _speedModeChangeRequested = false;
                yield return AnimateDrawSequence(result, i);
                yield break;
            }

            _currentBallIndex = i;
            int number = result.drawn[i];
            bool isWin = _winningNumbersSet.Contains(number);

            // INLINED ANIMATION - No nested coroutine
            BallData ball = _balls[i];
            PrepareBall(ball, number);
            Vector3 finalPos = CalculateFinalPosition(i);

            // Play ball falling sound
            AudioManager.Instance.PlayBallFalling();

            // Vertical fall
            yield return AnimateVerticalFall(ball, finalPos);

            // Horizontal roll
            yield return AnimateHorizontalRoll(ball, finalPos);

            // Bounce
            yield return AnimateBounce(ball, finalPos);

            // Settle
            yield return AnimateSettle(ball);

            // Win effect
            if (isWin)
            {
                AnimateWinEffect(ball);
                AudioManager.Instance.PlayWinBall();
            }

            GameEvents.TriggerBallDrawn(number, isWin);
            yield return new WaitForSeconds(GetCurrentDelay());
        }
    }

    // OPTIMIZED: Async version for instant mode (fire and forget)
    private IEnumerator AnimateSingleBallAsync(int ballIndex, int number, bool isWinning)
    {
        BallData ball = _balls[ballIndex];
        PrepareBall(ball, number);
        Vector3 finalPos = CalculateFinalPosition(ballIndex);

        yield return AnimateVerticalFall(ball, finalPos);
        yield return AnimateHorizontalRoll(ball, finalPos);
        yield return AnimateBounce(ball, finalPos);
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
        ball.ballObject.transform.position = _startPoint.position;
        ball.ballObject.transform.rotation = Quaternion.identity;

        if (ball.numberText != null)
            ball.numberText.text = number.ToString();

        if (ball.winImage != null)
            ball.winImage.SetActive(false);
    }

    private IEnumerator AnimateVerticalFall(BallData ball, Vector3 finalPos)
    {
        float vertDist = Vector3.Distance(_startPoint.position, _centerPoint.position);
        float vertRotations = vertDist / (_ballRadius * 2f * Mathf.PI);
        float vertRotation = -(vertRotations * 360f);

        Sequence vertSeq = DOTween.Sequence()
            .SetRecyclable(true)
            .SetUpdate(true);

        vertSeq.Append(ball.ballObject.transform.DOMove(_centerPoint.position, _verticalFallDuration).SetEase(Ease.InQuad));
        vertSeq.Join(ball.ballObject.transform.DORotate(new Vector3(0, 0, vertRotation), _verticalFallDuration, RotateMode.FastBeyond360).SetEase(Ease.Linear));

        yield return vertSeq.WaitForCompletion();
        vertSeq.Kill();
    }

    private IEnumerator AnimateHorizontalRoll(BallData ball, Vector3 finalPos)
    {
        float horzDist = Vector3.Distance(_centerPoint.position, finalPos);
        float horzRotations = horzDist / (_ballRadius * 2f * Mathf.PI);
        float naturalRoll = -(horzRotations * 360f);

        float currentRotation = ball.ballObject.transform.rotation.eulerAngles.z;
        float afterRoll = (currentRotation + naturalRoll) % 360f;
        if (afterRoll < 0) afterRoll += 360f;

        float adjustment = afterRoll <= 180f ? -afterRoll : 360f - afterRoll;
        float finalRotation = currentRotation + naturalRoll + adjustment;

        Sequence horzSeq = DOTween.Sequence()
            .SetRecyclable(true)
            .SetUpdate(true);

        horzSeq.Append(ball.ballObject.transform.DOMove(finalPos, _horizontalRollDuration).SetEase(Ease.OutQuad));
        horzSeq.Join(ball.ballObject.transform.DORotate(new Vector3(0, 0, finalRotation), _horizontalRollDuration, RotateMode.FastBeyond360).SetEase(Ease.Linear));

        yield return horzSeq.WaitForCompletion();
        horzSeq.Kill();
    }

    private IEnumerator AnimateBounce(BallData ball, Vector3 finalPos)
    {
        Vector3 bounceDir = (_centerPoint.position - _endPoint.position).normalized;
        bounceDir.y = 0;
        Vector3 bouncePos = finalPos + bounceDir * _bounceAmount;
        bouncePos.y = finalPos.y;

        Sequence bounceSeq = DOTween.Sequence()
            .SetRecyclable(true)
            .SetUpdate(true);

        bounceSeq.Append(ball.ballObject.transform.DOMove(bouncePos, _bounceDuration * 0.5f).SetEase(Ease.OutQuad));
        bounceSeq.Append(ball.ballObject.transform.DOMove(finalPos, _bounceDuration * 0.5f).SetEase(Ease.InOutQuad));

        yield return bounceSeq.WaitForCompletion();
        bounceSeq.Kill();
    }

    private IEnumerator AnimateSettle(BallData ball)
    {
        float tilt = Random.Range(-3f, 3f);
        Tween settleTween = ball.ballObject.transform.DORotate(new Vector3(0, 0, tilt), 0.2f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetRecyclable(true);

        yield return new WaitForSeconds(0.2f);
        settleTween.Kill();
    }

    private void AnimateWinEffect(BallData ball)
    {
        if (ball.winImage == null) return;

        ball.winImage.SetActive(true);

        Sequence winSeq = DOTween.Sequence()
            .SetRecyclable(true)
            .SetUpdate(true);

        winSeq.Append(ball.winImage.transform.DOScale(1.2f, 0.12f));
        winSeq.Append(ball.winImage.transform.DOScale(1f, 0.12f));
        winSeq.SetLoops(2);
        winSeq.OnComplete(() => winSeq.Kill());
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

    private Vector3 CalculateFinalPosition(int ballIndex)
    {
        Vector3 direction = (_endPoint.position - _centerPoint.position).normalized;
        float offset = ballIndex * ((_ballRadius * 2f) + _ballSpacing);
        Vector3 pos = _endPoint.position - direction * offset;
        pos.y = _endPoint.position.y;
        return pos;
    }

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
        _winningNumbersSet.Clear();
    }

    private void StopCurrentAnimation()
    {
        if (_mainAnimationCoroutine != null)
        {
            StopCoroutine(_mainAnimationCoroutine);
            _mainAnimationCoroutine = null;
        }

        // Kill all tweens properly
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

        // Final cleanup
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