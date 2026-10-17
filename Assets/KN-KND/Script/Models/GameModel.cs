using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class GameModel
{
    private GameInitData _initData;
    private PlayerData _playerData;
    private GameState _gameState;
    private List<int> _lastDrawnNumbers;
    private float _lastWinAmount;

    internal GameInitData InitData => _initData;
    internal PlayerData PlayerData => _playerData;
    internal GameState GameState => _gameState;
    private List<int> LastDrawnNumbers => _lastDrawnNumbers;
    internal float LastWinAmount => _lastWinAmount;

    internal GameModel()
    {
        _playerData = new PlayerData();
        _gameState = new GameState();
        _lastDrawnNumbers = new List<int>();
    }

    internal void Initialize(GameInitData initData)
    {
        _initData = initData;
        _playerData.balance = initData.initialBalance;
        _playerData.currentBet = initData.bets?[0] ?? 0.2f;
    }

    internal bool CanSelectNumber(int number) =>
        !_playerData.selectedNumbers.Contains(number) &&
        _playerData.selectedNumbers.Count < _initData.maximumPicks;

    internal void SelectNumber(int number)
    {
        if (CanSelectNumber(number))
        {
            _playerData.selectedNumbers.Add(number);
            _playerData.selectedNumbers.Sort();
        }
    }

    internal void DeselectNumber(int number) => _playerData.selectedNumbers.Remove(number);

    internal void ClearSelectedNumbers() => _playerData.selectedNumbers.Clear();

    internal void SetBet(float betAmount)
    {
        if (System.Array.IndexOf(_initData.bets, betAmount) != -1)
            _playerData.currentBet = betAmount;
    }

    internal void SetAutoPlayRounds(int rounds)
    {
        _playerData.autoBetRounds = rounds;
        _gameState.totalAutoRounds = rounds;
    }

    internal void SetAutoPlay(bool isActive)
    {
        _playerData.isAutoPlayActive = isActive;
        if (isActive)
            _gameState.currentRound = 0;
    }

    internal bool CanPlay() =>
        _playerData.selectedNumbers.Count >= GameConfig.MIN_NUMBERS &&
        _playerData.selectedNumbers.Count <= GameConfig.MAX_NUMBERS &&
        !_gameState.isPlaying;

    internal void StartGame()
    {
        _gameState.isPlaying = true;
        _playerData.balance -= _playerData.currentBet;

        if (_playerData.isAutoPlayActive)
            _gameState.currentRound++;
    }

    internal void ProcessResult(GameResultData result)
    {
        _lastDrawnNumbers = result.drawn;
        _playerData.balance = result.balance;
        _lastWinAmount = result.currentWinning;
    }

    internal void EndGame()
    {
        _gameState.isPlaying = false;
        Debug.Log("EndGame");
        Debug.Log("isautoplayactive in endgame" + _playerData.isAutoPlayActive);
        Debug.Log("currentround " + _gameState.currentRound + "totalautoround" + _gameState.totalAutoRounds);
        if (_playerData.isAutoPlayActive && (_gameState.currentRound >= _gameState.totalAutoRounds))
        {
            _playerData.isAutoPlayActive = false;
            _gameState.currentRound = 0;
        }
    }

    private float GetPayoutMultiplier(int hits)
    {
        int selectedCount = _playerData.selectedNumbers.Count;

        if (selectedCount < 1 || selectedCount > _initData.paytable.Length || hits <= 0)
            return 0f;

        float[] payouts = _initData.paytable[selectedCount - 1];
        return hits < payouts.Length ? payouts[hits] : 0f;
    }

    private List<int> GetWinningNumbers(List<int> drawnNumbers) =>
        _playerData.selectedNumbers.Intersect(drawnNumbers).ToList();

    internal void SetSpeedMode(SpeedMode mode) => _gameState.currentSpeedMode = mode;

    private float GetBallDelay() => _gameState.currentSpeedMode switch
    {
        SpeedMode.Turbo => GameConfig.TURBO_BALL_DELAY,
        SpeedMode.Instant => GameConfig.INSTANT_BALL_DELAY,
        _ => GameConfig.NORMAL_BALL_DELAY
    };
}