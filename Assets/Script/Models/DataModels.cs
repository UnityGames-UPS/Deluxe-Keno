using System;
using System.Collections.Generic;
using UnityEngine;

#region Game Data Models

[Serializable]
public class GameInitData
{
    public string id;
    public bool isSpecial;
    public int total = 80;
    public int draws = 20;
    public int maximumPicks = 15;
    public float[] bets;
    public float[][] paytable;
    public float initialBalance;
}

[Serializable]
public class GameResultData
{
    public bool success;
    public float currentWinning;
    public float totalBet;
    public List<int> hits;
    public List<int> drawn;
    public float balance;
}

[Serializable]
public class PlayerData
{
    public float balance;
    public float currentBet;
    public List<int> selectedNumbers = new List<int>();
    public int autoBetRounds;
    public bool isAutoPlayActive;
}

[Serializable]
public class GameState
{
    public bool isPlaying;
    public bool isAnimating;
    public SpeedMode currentSpeedMode = SpeedMode.Normal;
    public int currentRound;
    public int totalAutoRounds;
}

public static class GameConfig
{
    public const int MIN_NUMBERS = 1;
    public const int MAX_NUMBERS = 15;
    public const int TOTAL_NUMBERS = 80;
    public const int DRAWS_PER_GAME = 20;

    public const float NORMAL_BALL_DELAY = 0.4f;
    public const float TURBO_BALL_DELAY = 0.1f;
    public const float INSTANT_BALL_DELAY = 0.0f;

    public const float BALL_ANIMATION_DURATION = 0.5f;
    public const float BUTTON_SCALE_DURATION = 0.15f;
    public const float BUTTON_SCALE_AMOUNT = 1.1f;
}

public class PaytableEntry
{
    public int hits;
    public float multiplier;

    public PaytableEntry(int hits, float multiplier)
    {
        this.hits = hits;
        this.multiplier = multiplier;
    }
}

#endregion

#region Server Data Models

[Serializable]
public class ServerInitResponse
{
    public string id;
    public ServerGameData gameData;
    public ServerUIData uiData;
    public ServerPlayer player;
}

[Serializable]
public class ServerGameData
{
    public int total;
    public bool isSpecial;
    public int draws;
    public int maximumPicks;
    public List<double> bets;
    public List<List<double>> paytable;
}

[Serializable]
public class ServerUIData
{
    public string description;
}

[Serializable]
public class ServerPlayer
{
    public double balance;
}

[Serializable]
public class ServerResultResponse
{
    public bool success;
    public string id;
    public ServerResultPayload payload;
    public ServerPlayer player;
}

[Serializable]
public class ServerResultPayload
{
    public double currenWinning;
    public double totalBet;
    public List<int> hits;
    public List<int> drawn;
}

[Serializable]
public class DrawRequest
{
    public string type;
    public DrawRequestPayload payload;
}

[Serializable]
public class DrawRequestPayload
{
    public int betIndex;
    public List<int> picks;
}

#endregion