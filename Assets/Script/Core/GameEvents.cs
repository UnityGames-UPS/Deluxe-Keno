using System;
using System.Collections.Generic;

/// <summary>
/// FIXED GameEvents with number visual update event
/// </summary>
public static class GameEvents
{
    // Number Selection Events
    public static event Action<int> OnNumberSelected;
    public static event Action<int> OnNumberDeselected;
    public static event Action OnAllNumbersCleared;
    public static event Action<List<int>> OnQuickPickSelected;

    // NEW: Event for updating button visual state after validation
    public static event Action<int, bool> OnNumberVisualUpdate;

    // Betting Events
    public static event Action<float> OnBetChanged;
    public static event Action<int> OnAutoBetRoundsChanged;
    public static event Action<bool> OnAutoPlayToggled;

    // Game Flow Events
    public static event Action OnPlayButtonClicked;
    public static event Action OnGameStarted;
    public static event Action<GameResultData> OnGameResultReceived;
    public static event Action OnGameEnded;
    public static event Action OnRoundCompleted;
    public static event Action OnGameExit;

    // UI Events
    public static event Action<SpeedMode> OnSpeedModeChanged;
    public static event Action<float> OnBalanceUpdated;
    public static event Action<float> OnWinAmountUpdated;
    public static event Action<bool> OnBetPopupToggled;
    public static event Action<bool> OnAutoBetPopupToggled;

    // Animation Events
    public static event Action<int, bool> OnBallDrawn;
    public static event Action OnAnimationCompleted;
    public static event Action OnIntroAnimationStarted;
    public static event Action OnIntroAnimationCompleted;

    // Paytable Events
    public static event Action<int> OnPaytableUpdate;
    public static event Action OnPaytableHighlight;

    // Win Events
    public static event Action<float> OnShowWinPopup;

    // Connection Events
    public static event Action OnConnectionLost;
    public static event Action OnConnectionRestored;
    public static event Action OnConnectionUnstable;
    public static event Action<string> OnConnectionError;
    public static event Action OnAnotherDeviceLogin;

    // Audio Events
    public static event Action OnAudioSettingsChanged;

    // Trigger Methods - Number Selection
    public static void TriggerNumberSelected(int number) => OnNumberSelected?.Invoke(number);
    public static void TriggerNumberDeselected(int number) => OnNumberDeselected?.Invoke(number);
    public static void TriggerAllNumbersCleared() => OnAllNumbersCleared?.Invoke();
    public static void TriggerQuickPickSelected(List<int> numbers) => OnQuickPickSelected?.Invoke(numbers);

    // NEW: Trigger visual update for specific button
    public static void TriggerNumberVisualUpdate(int number, bool selected) => OnNumberVisualUpdate?.Invoke(number, selected);

    // Trigger Methods - Betting
    public static void TriggerBetChanged(float bet) => OnBetChanged?.Invoke(bet);
    public static void TriggerAutoBetRoundsChanged(int rounds) => OnAutoBetRoundsChanged?.Invoke(rounds);
    public static void TriggerAutoPlayToggled(bool isEnabled) => OnAutoPlayToggled?.Invoke(isEnabled);

    // Trigger Methods - Game Flow
    public static void TriggerPlayButtonClicked() => OnPlayButtonClicked?.Invoke();
    public static void TriggerGameStarted() => OnGameStarted?.Invoke();
    public static void TriggerGameResultReceived(GameResultData result) => OnGameResultReceived?.Invoke(result);
    public static void TriggerGameEnded() => OnGameEnded?.Invoke();
    public static void TriggerRoundCompleted() => OnRoundCompleted?.Invoke();
    public static void TriggerGameExit() => OnGameExit?.Invoke();

    // Trigger Methods - UI
    public static void TriggerSpeedModeChanged(SpeedMode mode) => OnSpeedModeChanged?.Invoke(mode);
    public static void TriggerBalanceUpdated(float balance) => OnBalanceUpdated?.Invoke(balance);
    public static void TriggerWinAmountUpdated(float winAmount) => OnWinAmountUpdated?.Invoke(winAmount);
    public static void TriggerBetPopupToggled(bool show) => OnBetPopupToggled?.Invoke(show);
    public static void TriggerAutoBetPopupToggled(bool show) => OnAutoBetPopupToggled?.Invoke(show);

    // Trigger Methods - Animation
    public static void TriggerBallDrawn(int ballNumber, bool isWinning) => OnBallDrawn?.Invoke(ballNumber, isWinning);
    public static void TriggerAnimationCompleted() => OnAnimationCompleted?.Invoke();
    public static void TriggerIntroAnimationStarted() => OnIntroAnimationStarted?.Invoke();
    public static void TriggerIntroAnimationCompleted() => OnIntroAnimationCompleted?.Invoke();

    // Trigger Methods - Paytable
    public static void TriggerPaytableUpdate(int selectedCount) => OnPaytableUpdate?.Invoke(selectedCount);
    public static void TriggerPaytableHighlight() => OnPaytableHighlight?.Invoke();

    // Trigger Methods - Win
    public static void TriggerShowWinPopup(float winAmount) => OnShowWinPopup?.Invoke(winAmount);

    // Trigger Methods - Connection
    public static void TriggerConnectionLost() => OnConnectionLost?.Invoke();
    public static void TriggerConnectionRestored() => OnConnectionRestored?.Invoke();
    public static void TriggerConnectionUnstable() => OnConnectionUnstable?.Invoke();
    public static void TriggerConnectionError(string message) => OnConnectionError?.Invoke(message);
    public static void TriggerAnotherDeviceLogin() => OnAnotherDeviceLogin?.Invoke();

    // Trigger Methods - Audio
    public static void TriggerAudioSettingsChanged() => OnAudioSettingsChanged?.Invoke();
}

public enum SpeedMode
{
    Normal,
    Turbo,
    Instant
}