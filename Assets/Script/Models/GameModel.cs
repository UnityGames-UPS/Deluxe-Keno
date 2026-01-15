using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameModel
{
    private GameInitData _initData;
    private PlayerData _playerData;
    private GameState _gameState;
    private List<int> _lastDrawnNumbers;
    private float _lastWinAmount;

    public GameInitData InitData => _initData;
    public PlayerData PlayerData => _playerData;
    public GameState GameState => _gameState;
    public List<int> LastDrawnNumbers => _lastDrawnNumbers;
    public float LastWinAmount => _lastWinAmount;

    public GameModel()
    {
        _playerData = new PlayerData();
        _gameState = new GameState();
        _lastDrawnNumbers = new List<int>();
    }

    public void Initialize(GameInitData initData)
    {
        _initData = initData;
        _playerData.balance = initData.initialBalance;
        _playerData.currentBet = initData.bets?[0] ?? 0.2f;
    }

    public bool CanSelectNumber(int number) =>
        !_playerData.selectedNumbers.Contains(number) &&
        _playerData.selectedNumbers.Count < _initData.maximumPicks;

    public void SelectNumber(int number)
    {
        if (CanSelectNumber(number))
        {
            _playerData.selectedNumbers.Add(number);
            _playerData.selectedNumbers.Sort();
        }
    }

    public void DeselectNumber(int number) => _playerData.selectedNumbers.Remove(number);

    public void ClearSelectedNumbers() => _playerData.selectedNumbers.Clear();

    public void SetBet(float betAmount)
    {
        if (System.Array.IndexOf(_initData.bets, betAmount) != -1)
            _playerData.currentBet = betAmount;
    }

    public void SetAutoPlayRounds(int rounds)
    {
        _playerData.autoBetRounds = rounds;
        _gameState.totalAutoRounds = rounds;
    }

    public void SetAutoPlay(bool isActive)
    {
        _playerData.isAutoPlayActive = isActive;
        if (isActive)
            _gameState.currentRound = 0;
    }

    public bool CanPlay() =>
        _playerData.selectedNumbers.Count >= GameConfig.MIN_NUMBERS &&
        _playerData.selectedNumbers.Count <= GameConfig.MAX_NUMBERS &&
        _playerData.balance >= _playerData.currentBet &&
        !_gameState.isPlaying;

    public void StartGame()
    {
        _gameState.isPlaying = true;
        _playerData.balance -= _playerData.currentBet;

        if (_playerData.isAutoPlayActive)
            _gameState.currentRound++;
    }

    public void ProcessResult(GameResultData result)
    {
        _lastDrawnNumbers = result.drawn;
        _playerData.balance = result.balance;
        _lastWinAmount = result.currentWinning;
    }

    public void EndGame()
    {
        _gameState.isPlaying = false;

        if (_playerData.isAutoPlayActive &&
            (_gameState.currentRound >= _gameState.totalAutoRounds ||
             _playerData.balance < _playerData.currentBet))
        {
            _playerData.isAutoPlayActive = false;
            _gameState.currentRound = 0;
        }
    }

    public float GetPayoutMultiplier(int hits)
    {
        int selectedCount = _playerData.selectedNumbers.Count;

        if (selectedCount < 1 || selectedCount > _initData.paytable.Length || hits <= 0)
            return 0f;

        float[] payouts = _initData.paytable[selectedCount - 1];
        return hits < payouts.Length ? payouts[hits] : 0f;
    }

    public List<int> GetWinningNumbers(List<int> drawnNumbers) =>
        _playerData.selectedNumbers.Intersect(drawnNumbers).ToList();

    public void SetSpeedMode(SpeedMode mode) => _gameState.currentSpeedMode = mode;

    public float GetBallDelay() => _gameState.currentSpeedMode switch
    {
        SpeedMode.Turbo => GameConfig.TURBO_BALL_DELAY,
        SpeedMode.Instant => GameConfig.INSTANT_BALL_DELAY,
        _ => GameConfig.NORMAL_BALL_DELAY
    };
}