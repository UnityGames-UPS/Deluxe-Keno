using System;
using System.Collections.Generic;

/// <summary>
/// FIXED GameEvents with number visual update event
/// </summary>
internal static class GameEvents
{
    // Number Selection Events
    internal static event Action<int> OnNumberSelected;
    internal static event Action<int> OnNumberDeselected;
    internal static event Action OnAllNumbersCleared;
    internal static event Action<List<int>> OnQuickPickSelected;
    internal static event Action<int, bool> OnNumberVisualUpdate;
    //public static event Action OnClearAllDrawnStates;
    
    // Betting Events
    internal static event Action<float> OnBetChanged;
    internal static event Action<int> OnAutoBetRoundsChanged;
    internal static event Action<bool> OnAutoPlayToggled;

    // Game Flow Events
    internal static event Action OnPlayButtonClicked;
    internal static event Action OnGameStarted;
    internal static event Action<GameResultData> OnGameResultReceived;
    internal static event Action OnGameEnded;
    internal static event Action OnRoundCompleted;
    internal static event Action OnGameExit;

    // UI Events
    internal static event Action<SpeedMode> OnSpeedModeChanged;
    internal static event Action<float> OnBalanceUpdated;
    internal static event Action<float> OnWinAmountUpdated;
    internal static event Action<bool> OnBetPopupToggled;
    internal static event Action<bool> OnAutoBetPopupToggled;

    // Animation Events
    internal static event Action<int, bool> OnBallDrawn;
    internal static event Action OnAnimationCompleted;
    internal static event Action OnIntroAnimationStarted;
    internal static event Action OnIntroAnimationCompleted;

    // Paytable Events
    internal static event Action<int> OnPaytableUpdate;
    internal static event Action OnPaytableHighlight;

    // Win Events
    internal static event Action<float> OnShowWinPopup;

    // Connection Events
    internal static event Action OnConnectionLost;
    internal static event Action OnConnectionRestored;
    internal static event Action OnConnectionUnstable;
    internal static event Action<string> OnConnectionError;
    internal static event Action OnAnotherDeviceLogin;

    // Audio Events
    internal static event Action OnAudioSettingsChanged;

    // Trigger Methods - Number Selection
    internal static void TriggerNumberSelected(int number) => OnNumberSelected?.Invoke(number);
    internal static void TriggerNumberDeselected(int number) => OnNumberDeselected?.Invoke(number);
    internal static void TriggerAllNumbersCleared() => OnAllNumbersCleared?.Invoke();
    internal static void TriggerQuickPickSelected(List<int> numbers) => OnQuickPickSelected?.Invoke(numbers);
    internal static void TriggerNumberVisualUpdate(int number, bool selected) => OnNumberVisualUpdate?.Invoke(number, selected);

    //public static void TriggerClearAllDrawnStates() => OnClearAllDrawnStates?.Invoke();
    // Trigger Methods - Betting
    internal static void TriggerBetChanged(float bet) => OnBetChanged?.Invoke(bet);
    internal static void TriggerAutoBetRoundsChanged(int rounds) => OnAutoBetRoundsChanged?.Invoke(rounds);
    internal static void TriggerAutoPlayToggled(bool isEnabled) => OnAutoPlayToggled?.Invoke(isEnabled);

    // Trigger Methods - Game Flow
    internal static void TriggerPlayButtonClicked() => OnPlayButtonClicked?.Invoke();
    internal static void TriggerGameStarted() => OnGameStarted?.Invoke();
    internal static void TriggerGameResultReceived(GameResultData result) => OnGameResultReceived?.Invoke(result);
    internal static void TriggerGameEnded() => OnGameEnded?.Invoke();
    internal static void TriggerRoundCompleted() => OnRoundCompleted?.Invoke();
    private static void TriggerGameExit() => OnGameExit?.Invoke();

    // Trigger Methods - UI
    internal static void TriggerSpeedModeChanged(SpeedMode mode) => OnSpeedModeChanged?.Invoke(mode);
    internal static void TriggerBalanceUpdated(float balance) => OnBalanceUpdated?.Invoke(balance);
    internal static void TriggerWinAmountUpdated(float winAmount) => OnWinAmountUpdated?.Invoke(winAmount);
    private static void TriggerBetPopupToggled(bool show) => OnBetPopupToggled?.Invoke(show);
    private static void TriggerAutoBetPopupToggled(bool show) => OnAutoBetPopupToggled?.Invoke(show);

    // Trigger Methods - Animation
    internal static void TriggerBallDrawn(int ballNumber, bool isWinning) => OnBallDrawn?.Invoke(ballNumber, isWinning);
    internal static void TriggerAnimationCompleted() => OnAnimationCompleted?.Invoke();
    internal static void TriggerIntroAnimationStarted() => OnIntroAnimationStarted?.Invoke();
    internal static void TriggerIntroAnimationCompleted() => OnIntroAnimationCompleted?.Invoke();

    // Trigger Methods - Paytable
    internal static void TriggerPaytableUpdate(int selectedCount) => OnPaytableUpdate?.Invoke(selectedCount);
    internal static void TriggerPaytableHighlight() => OnPaytableHighlight?.Invoke();

    // Trigger Methods - Win
    internal static void TriggerShowWinPopup(float winAmount) => OnShowWinPopup?.Invoke(winAmount);

    // Trigger Methods - Connection
    internal static void TriggerConnectionLost() => OnConnectionLost?.Invoke();
    internal static void TriggerConnectionRestored() => OnConnectionRestored?.Invoke();
    internal static void TriggerConnectionUnstable() => OnConnectionUnstable?.Invoke();
    internal static void TriggerConnectionError(string message) => OnConnectionError?.Invoke(message);
    internal static void TriggerAnotherDeviceLogin() => OnAnotherDeviceLogin?.Invoke();

    // Trigger Methods - Audio
    internal static void TriggerAudioSettingsChanged() => OnAudioSettingsChanged?.Invoke();
}

public enum SpeedMode
{
    Normal,
    Turbo,
    Instant
}