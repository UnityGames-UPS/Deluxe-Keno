using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class SocketEventHandler
{
    private float[] _availableBets;

    public GameInitData ParseInitData(string json)
    {
        try
        {
            ServerInitResponse response = JsonConvert.DeserializeObject<ServerInitResponse>(json);

            if (response == null || response.gameData == null)
            {
                Debug.LogError("Invalid init data - null response or gameData");
                return null;
            }

            _availableBets = ConvertDoubleListToFloat(response.gameData.bets);

            GameInitData initData = new GameInitData
            {
                id = response.id ?? "KN-KND",
                isSpecial = response.gameData.isSpecial,
                total = response.gameData.total,
                draws = response.gameData.draws,
                maximumPicks = response.gameData.maximumPicks,
                bets = _availableBets,
                paytable = ConvertDoublePaytableToFloat(response.gameData.paytable),
                initialBalance = response.player != null ? (float)response.player.balance : 0f
            };

            Debug.Log("=== PARSED INIT DATA ===");
            Debug.Log($"Game ID: {initData.id}");
            Debug.Log($"Total Numbers: {initData.total}");
            Debug.Log($"Draws Per Game: {initData.draws}");
            Debug.Log($"Max Picks: {initData.maximumPicks}");
            Debug.Log($"Available Bets: {string.Join(", ", System.Array.ConvertAll(initData.bets, b => $"${b:F2}"))}");
            Debug.Log($"Initial Balance: ${initData.initialBalance:F3}");
            Debug.Log("========================");

            return initData;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing init data: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
            Debug.LogError($"JSON that failed: {json}");
            return null;
        }
    }

    public GameResultData ParseResultData(string json)
    {
        try
        {
            ServerResultResponse response = JsonConvert.DeserializeObject<ServerResultResponse>(json);

            if (response == null || response.payload == null || response.player == null || !response.success)
            {
                Debug.LogError("Invalid result data");
                return null;
            }

            GameResultData resultData = new GameResultData
            {
                success = response.success,
                currentWinning = (float)response.payload.currenWinning,
                totalBet = (float)response.payload.totalBet,
                hits = response.payload.hits ?? new List<int>(),
                drawn = response.payload.drawn ?? new List<int>(),
                balance = (float)response.player.balance
            };

          /*  Debug.Log("=== PARSED RESULT DATA ===");
            Debug.Log($"Success: {resultData.success}");
            Debug.Log($"Drawn Numbers: {string.Join(", ", resultData.drawn)}");
            Debug.Log($"Hit Numbers: {string.Join(", ", resultData.hits)}");
            Debug.Log($"Hits Count: {resultData.hits.Count}");
            Debug.Log($"Win Amount: ${resultData.currentWinning:F2}");
            Debug.Log($"Total Bet: ${resultData.totalBet:F2}");
            Debug.Log($"New Balance: ${resultData.balance:F3}");
            Debug.Log("==========================");*/

            return resultData;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing result data: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
            Debug.LogError($"JSON that failed: {json}");
            return null;
        }
    }

    public int GetBetIndex(float betAmount)
    {
        if (_availableBets == null || _availableBets.Length == 0)
        {
            Debug.LogError("Bets not initialized - cannot convert bet amount");
            return -1;
        }

        for (int i = 0; i < _availableBets.Length; i++)
        {
            if (Mathf.Approximately(_availableBets[i], betAmount))
            {
                Debug.Log($"Bet ${betAmount:F2} mapped to index {i}");
                return i;
            }
        }

        string availableBets = string.Join(", ", System.Array.ConvertAll(_availableBets, b => $"${b:F2}"));
        Debug.LogError($"Bet amount ${betAmount:F2} not found in available bets: [{availableBets}]");
        return -1;
    }

    private float[] ConvertDoubleListToFloat(List<double> doubleList)
    {
        if (doubleList == null || doubleList.Count == 0)
        {
            Debug.LogWarning("Empty bets list received from server");
            return new float[0];
        }

        float[] floatArray = new float[doubleList.Count];
        for (int i = 0; i < doubleList.Count; i++)
            floatArray[i] = (float)doubleList[i];

        return floatArray;
    }

    private float[][] ConvertDoublePaytableToFloat(List<List<double>> doublePaytable)
    {
        if (doublePaytable == null || doublePaytable.Count == 0)
        {
            Debug.LogWarning("Empty paytable received from server");
            return new float[0][];
        }

        float[][] floatPaytable = new float[doublePaytable.Count][];

        for (int i = 0; i < doublePaytable.Count; i++)
        {
            floatPaytable[i] = new float[doublePaytable[i].Count];
            for (int j = 0; j < doublePaytable[i].Count; j++)
                floatPaytable[i][j] = (float)doublePaytable[i][j];
        }

        return floatPaytable;
    }
}