using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Backend service interface
/// </summary>
/// test push
public interface IBackendService
{
    void Initialize(Action<GameInitData> onInitialized);
    void SendDrawRequest(float bet, List<int> picks, Action<GameResultData> onResult);
    bool IsConnected { get; }
    void Close();
}

/// <summary>
/// Dummy backend for testing (unchanged)
/// </summary>
public class DummyBackendService : IBackendService
{
    private GameInitData _initData;
    private float _balance = 10.0f;
    private System.Random _random = new System.Random();

    public bool IsConnected => true;

    public void Initialize(Action<GameInitData> onInitialized)
    {
        _initData = new GameInitData
        {
            id = "KN-KND",
            isSpecial = false,
            total = 80,
            draws = 20,
            maximumPicks = 15,
            bets = new float[] { 0.2f, 0.4f, 0.6f, 1.0f, 2.0f, 3.0f, 5.0f, 10f },
            paytable = new float[][]
            {
                new float[] { 0, 3.8f },
                new float[] { 0, 0, 16 },
                new float[] { 0, 0, 2, 48 },
                new float[] { 0, 0, 1.5f, 10, 65 },
                new float[] { 0, 0, 1.5f, 4, 12, 100 },
                new float[] { 0, 0, 1, 1.5f, 10, 44, 200 },
                new float[] { 0, 0, 0, 2, 6, 24, 100, 300 },
                new float[] { 0, 0, 0, 1, 4, 10, 60, 500, 1000 },
                new float[] { 0, 0, 0, 1, 2.5f, 8, 20, 50, 400, 2000 },
                new float[] { 0, 0, 0, 1, 2, 4, 10, 32, 100, 500, 3000 },
                new float[] { 0, 0, 0, 0, 1.5f, 4, 10, 20, 70, 600, 1000, 4000 },
                new float[] { 0, 1, 0, 0, 1, 2, 5, 20, 50, 300, 800, 2000, 5000 },
                new float[] { 0, 1, 0, 0, 1, 2, 3, 10, 20, 50, 500, 1500, 3000, 6000 },
                new float[] { 0, 1, 0, 0, 0, 2, 4, 8, 16, 50, 150, 1000, 1500, 4000, 8000 },
                new float[] { 0, 1, 0, 0, 0, 2, 3, 5, 8, 25, 50, 500, 1500, 3000, 5000, 10000 }
            }
        };

        onInitialized?.Invoke(_initData);
    }

    public void SendDrawRequest(float bet, List<int> picks, Action<GameResultData> onResult)
    {
        CoroutineRunner.Instance.StartCoroutine(SimulateDrawRequest(bet, picks, onResult));
    }

    private IEnumerator SimulateDrawRequest(float bet, List<int> picks, Action<GameResultData> onResult)
    {
        yield return new WaitForSeconds(0.2f);

        List<int> drawn = GenerateRandomNumbers(20, 1, 80);
        List<int> hits = new List<int>(picks.Count);

        foreach (int pick in picks)
        {
            if (drawn.Contains(pick))
                hits.Add(pick);
        }

        float multiplier = GetPayoutMultiplier(picks.Count, hits.Count);
        float winning = bet * multiplier;
        _balance = _balance - bet + winning;

        onResult?.Invoke(new GameResultData
        {
            success = true,
            currentWinning = winning,
            totalBet = bet,
            hits = hits,
            drawn = drawn,
            balance = _balance
        });
    }

    private List<int> GenerateRandomNumbers(int count, int min, int max)
    {
        List<int> numbers = new List<int>(count);
        HashSet<int> used = new HashSet<int>();

        while (numbers.Count < count)
        {
            int num = _random.Next(min, max + 1);
            if (used.Add(num))
                numbers.Add(num);
        }

        return numbers;
    }

    private float GetPayoutMultiplier(int picksCount, int hitsCount)
    {
        if (picksCount < 1 || picksCount > _initData.paytable.Length || hitsCount < 0)
            return 0f;

        float[] payouts = _initData.paytable[picksCount - 1];
        return hitsCount < payouts.Length ? payouts[hitsCount] : 0f;
    }

    public void Close()
    {
        // No cleanup needed for dummy
    }
}

/// <summary>
/// Coroutine runner helper
/// </summary>
public class CoroutineRunner : MonoBehaviour
{
    private static CoroutineRunner _instance;

    public static CoroutineRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("CoroutineRunner");
                _instance = go.AddComponent<CoroutineRunner>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
}