using System;
using System.Collections.Generic;
using UnityEngine;

#region Game Data Models (Existing - No Changes)

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

#region Server Data Models - EXACT MATCH TO PRODUCTION SERVER

/// <summary>
/// Server init response structure
/// Event: "game:init"
/// Verified from production logs
/// </summary>
[Serializable]
public class ServerInitResponse
{
    public string id;                    // "initData"
    public ServerGameData gameData;
    public ServerUIData uiData;
    public ServerPlayer player;
}

/// <summary>
/// Game configuration from server
/// </summary>
[Serializable]
public class ServerGameData
{
    public int total;              // 80 - total numbers available
    public bool isSpecial;         // false
    public int draws;              // 20 - numbers drawn per game
    public int maximumPicks;       // 10 - max numbers player can select
    public List<double> bets;      // Available bet amounts
    public List<List<double>> paytable;  // Payout multipliers
}

/// <summary>
/// UI data from server (optional)
/// </summary>
[Serializable]
public class ServerUIData
{
    public string description;     // Game instructions
}

/// <summary>
/// Player data from server
/// Note: Only contains balance in production responses
/// </summary>
[Serializable]
public class ServerPlayer
{
    public double balance;         // Current player balance
}

/// <summary>
/// Server result response structure
/// Event: "result"
/// Verified from production logs
/// IMPORTANT: Contains "success" field at root level
/// </summary>
[Serializable]
public class ServerResultResponse
{
    public bool success;                 // ⚠️ Server success flag
    public string id;                    // "ResultData"
    public ServerResultPayload payload;
    public ServerPlayer player;
}

/// <summary>
/// Result payload from server
/// CRITICAL: Server uses "currenWinning" (typo - missing 't')
/// This MUST match exactly or deserialization will fail silently
/// </summary>
[Serializable]
public class ServerResultPayload
{
    public double currenWinning;   // ⚠️ TYPO IN SERVER - Keep as is!
    public double totalBet;        // Bet amount for this round
    public List<int> hits;         // Player's numbers that were drawn
    public List<int> drawn;        // All 20 numbers drawn
}

/// <summary>
/// Draw request structure (Unity → Server)
/// Event: "request"
/// </summary>
[Serializable]
public class DrawRequest
{
    public string type;                  // "DRAW"
    public DrawRequestPayload payload;
}

/// <summary>
/// Draw request payload
/// </summary>
[Serializable]
public class DrawRequestPayload
{
    public int betIndex;           // Index of selected bet (0-based)
    public List<int> picks;        // Player's selected numbers
}

#endregion