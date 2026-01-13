using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using DG.Tweening;

public class PaytableView : MonoBehaviour
{
    [System.Serializable]
    public class PaytableRowUI
    {
        public TextMeshProUGUI hitsText;
        public TextMeshProUGUI multiplierText;
    }

    [Header("Paytable Rows (Need 16 rows: 0-15)")]
    [SerializeField] private List<PaytableRowUI> _paytableRows = new List<PaytableRowUI>();

    [Header("Colors")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _activeHighlightColor = Color.yellow;
    [SerializeField] private Color _winColor = Color.green;
    [SerializeField] private Color _zeroPayoutColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private int _currentSelectedCount;
    private int _currentHitsDuringAnimation;
    private bool _isAnimationActive;
    private GameResultData _pendingResult;

    // OPTIMIZATION: Cached strings to avoid allocations
    private static readonly string[] _cachedHitNumbers = new string[16];
    private static readonly string[] _cachedMultipliers = new string[10001];
    private static StringBuilder _stringBuilder = new StringBuilder(16);

    static PaytableView()
    {
        // Cache common strings
        for (int i = 0; i < 16; i++)
            _cachedHitNumbers[i] = i.ToString();

        // Cache multiplier strings up to 10000x
        _cachedMultipliers[0] = "0";
        for (int i = 1; i <= 10000; i++)
            _cachedMultipliers[i] = $"x{i}";
    }

    private void Start()
    {
        SubscribeToEvents();
        InitializeRows();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        CleanupAnimations();
    }

    private void InitializeRows()
    {
        foreach (PaytableRowUI row in _paytableRows)
        {
            SetRowVisible(row, false);
        }
    }

    private void SubscribeToEvents()
    {
        GameEvents.OnPaytableUpdate += HandlePaytableUpdate;
        GameEvents.OnGameResultReceived += HandleGameResult;
        GameEvents.OnBallDrawn += HandleBallDrawn;
        GameEvents.OnPaytableHighlight += HandlePaytableHighlight;
        GameEvents.OnGameStarted += HandleGameStarted;
        GameEvents.OnGameEnded += HandleGameEnded;
        GameEvents.OnAllNumbersCleared += HandleClearAll;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnPaytableUpdate -= HandlePaytableUpdate;
        GameEvents.OnGameResultReceived -= HandleGameResult;
        GameEvents.OnBallDrawn -= HandleBallDrawn;
        GameEvents.OnPaytableHighlight -= HandlePaytableHighlight;
        GameEvents.OnGameStarted -= HandleGameStarted;
        GameEvents.OnGameEnded -= HandleGameEnded;
        GameEvents.OnAllNumbersCleared -= HandleClearAll;
    }

    private void HandlePaytableUpdate(int selectedCount)
    {
        _currentSelectedCount = selectedCount;
        UpdatePaytableDisplay();
    }

    private void HandleClearAll()
    {
        _currentSelectedCount = 0;
        UpdatePaytableDisplay();
    }

    private void HandleGameStarted()
    {
        _isAnimationActive = true;
        _currentHitsDuringAnimation = 0;
        UpdatePaytableDisplay();
    }

    private void HandleGameResult(GameResultData result)
    {
        _pendingResult = result;
    }

    private void HandleBallDrawn(int ballNumber, bool isWinning)
    {
        if (!_isAnimationActive || !isWinning) return;

        int previousHits = _currentHitsDuringAnimation;
        _currentHitsDuringAnimation++;

        UpdatePaytableDisplayWithActiveHighlight(previousHits, _currentHitsDuringAnimation);
    }

    private void HandlePaytableHighlight()
    {
        _isAnimationActive = false;

        if (_pendingResult != null)
        {
            HighlightWinningRow(_pendingResult.hits.Count);
            _pendingResult = null;
        }
    }

    private void HandleGameEnded()
    {
        DOVirtual.DelayedCall(2f, () =>
        {
            _currentHitsDuringAnimation = 0;
            UpdatePaytableDisplay();
        }).SetUpdate(true).SetRecyclable(true).OnComplete(() => DOTween.Kill(this));
    }

    private void UpdatePaytableDisplay()
    {
        if (_currentSelectedCount < 1 ||
            _currentSelectedCount > GameController.Instance.GetModel().InitData.maximumPicks)
        {
            HideAllRows();
            return;
        }

        float[] payouts = GameController.Instance.GetModel().InitData.paytable[_currentSelectedCount - 1];

        // Row 0: Always "0 hits = 0 win"
        UpdateRow(0, 0f, _zeroPayoutColor, true);

        // Rows 1 to selectedCount
        for (int hits = 1; hits <= _currentSelectedCount && hits < _paytableRows.Count; hits++)
        {
            int paytableIndex = hits - 1;

            if (paytableIndex < payouts.Length)
            {
                float multiplier = payouts[paytableIndex];
                Color textColor = multiplier == 0 ? _zeroPayoutColor : _normalColor;
                UpdateRow(hits, multiplier, textColor, true);
            }
            else
            {
                SetRowVisible(_paytableRows[hits], false);
            }
        }

        // Hide remaining rows
        for (int hits = _currentSelectedCount + 1; hits < _paytableRows.Count; hits++)
        {
            SetRowVisible(_paytableRows[hits], false);
        }

        GameLogger.Log($"Paytable updated for {_currentSelectedCount} selections");
    }

    private void UpdatePaytableDisplayWithActiveHighlight(int previousHits, int currentHits)
    {
        if (_currentSelectedCount < 1) return;

        float[] payouts = GameController.Instance.GetModel().InitData.paytable[_currentSelectedCount - 1];

        // Clear previous highlight
        if (previousHits >= 0 && previousHits < _paytableRows.Count)
        {
            Color prevColor;
            if (previousHits == 0)
            {
                prevColor = _zeroPayoutColor;
            }
            else
            {
                int paytableIndex = previousHits - 1;
                prevColor = (paytableIndex < payouts.Length && payouts[paytableIndex] == 0)
                    ? _zeroPayoutColor
                    : _normalColor;
            }
            UpdateRowColor(previousHits, prevColor);
        }

        // Highlight current
        if (currentHits >= 0 && currentHits < _paytableRows.Count)
        {
            UpdateRowColor(currentHits, _activeHighlightColor);
            AnimateRow(_paytableRows[currentHits], 0.1f);
        }
    }

    private void UpdateRow(int hitsCount, float multiplier, Color color, bool show)
    {
        if (hitsCount >= _paytableRows.Count) return;

        PaytableRowUI row = _paytableRows[hitsCount];

        if (row.hitsText != null)
        {
            row.hitsText.gameObject.SetActive(show);
            row.hitsText.text = _cachedHitNumbers[hitsCount];
            row.hitsText.color = color;
        }

        if (row.multiplierText != null)
        {
            row.multiplierText.gameObject.SetActive(show);

            // OPTIMIZED: Use cached strings
            int multiplierInt = (int)multiplier;
            if (multiplierInt == multiplier && multiplierInt < _cachedMultipliers.Length)
            {
                row.multiplierText.text = _cachedMultipliers[multiplierInt];
            }
            else
            {
                // Fallback for decimal multipliers
                _stringBuilder.Clear();
                _stringBuilder.Append('x');
                _stringBuilder.Append(multiplier.ToString("F1"));
                row.multiplierText.text = _stringBuilder.ToString();
            }

            row.multiplierText.color = color;
        }
    }

    private void UpdateRowColor(int rowIndex, Color color)
    {
        if (rowIndex >= _paytableRows.Count) return;

        PaytableRowUI row = _paytableRows[rowIndex];

        if (row.hitsText != null)
            row.hitsText.color = color;

        if (row.multiplierText != null)
            row.multiplierText.color = color;
    }

    private void HighlightWinningRow(int hits)
    {
        if (_currentSelectedCount < 1) return;

        float[] payouts = GameController.Instance.GetModel().InitData.paytable[_currentSelectedCount - 1];

        // Reset all rows to base colors
        UpdateRowColor(0, _zeroPayoutColor);

        for (int i = 1; i <= _currentSelectedCount && i < _paytableRows.Count; i++)
        {
            int paytableIndex = i - 1;
            if (paytableIndex < payouts.Length)
            {
                Color baseColor = payouts[paytableIndex] == 0 ? _zeroPayoutColor : _normalColor;
                UpdateRowColor(i, baseColor);
            }
        }

        // Highlight winning row
        if (hits >= 0 && hits < _paytableRows.Count)
        {
            UpdateRowColor(hits, _winColor);
            AnimateRow(_paytableRows[hits], 0.2f);
        }
    }

    private void AnimateRow(PaytableRowUI row, float scale)
    {
        if (row.hitsText != null)
        {
            row.hitsText.transform.DOKill(true);

            Tween punchTween = row.hitsText.transform.DOPunchScale(Vector3.one * scale, 0.3f, 4, 0.4f)
                .SetUpdate(true)
                .SetRecyclable(true);

            punchTween.OnComplete(() => punchTween.Kill());
        }

        if (row.multiplierText != null)
        {
            row.multiplierText.transform.DOKill(true);

            Tween punchTween = row.multiplierText.transform.DOPunchScale(Vector3.one * scale, 0.3f, 4, 0.4f)
                .SetUpdate(true)
                .SetRecyclable(true);

            punchTween.OnComplete(() => punchTween.Kill());
        }
    }

    private void SetRowVisible(PaytableRowUI row, bool visible)
    {
        if (row.hitsText != null)
            row.hitsText.gameObject.SetActive(visible);

        if (row.multiplierText != null)
            row.multiplierText.gameObject.SetActive(visible);
    }

    private void HideAllRows()
    {
        foreach (PaytableRowUI row in _paytableRows)
        {
            SetRowVisible(row, false);
        }
    }

    private void CleanupAnimations()
    {
        foreach (var row in _paytableRows)
        {
            if (row.hitsText != null)
                row.hitsText.transform.DOKill(true);

            if (row.multiplierText != null)
                row.multiplierText.transform.DOKill(true);
        }

        DOTween.Kill(this);
    }
}