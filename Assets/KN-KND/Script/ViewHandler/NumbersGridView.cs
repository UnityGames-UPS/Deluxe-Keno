using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class NumbersGridView : MonoBehaviour
{
    [Header("Container")]
    [SerializeField] private Transform _gridContainer;

    private Dictionary<int, NumberButton> _numberButtons = new Dictionary<int, NumberButton>();

    private void Start()
    {
        InitializeExistingButtons();
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        CleanupAllButtons();
    }

    private void CleanupAllButtons()
    {
        foreach (var button in _numberButtons.Values)
        {
            if (button != null)
                button.transform.DOKill();
        }
    }

    private void InitializeExistingButtons()
    {
        _numberButtons.Clear();
        NumberButton[] buttons = _gridContainer.GetComponentsInChildren<NumberButton>();

        for (int i = 0; i < buttons.Length && i < 80; i++)
        {
            int number = i + 1;
            buttons[i].Initialize(number);
            _numberButtons[number] = buttons[i];
        }
    }

    private void SubscribeToEvents()
    {
        GameEvents.OnAllNumbersCleared += HandleClearAll;
        GameEvents.OnQuickPickSelected += HandleQuickPickSelected;
        GameEvents.OnGameStarted += HandleGameStarted;
        GameEvents.OnBallDrawn += HandleBallDrawn;
        GameEvents.OnGameEnded += HandleGameEnded;
        GameEvents.OnNumberVisualUpdate += HandleNumberVisualUpdate;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnAllNumbersCleared -= HandleClearAll;
        GameEvents.OnQuickPickSelected -= HandleQuickPickSelected;
        GameEvents.OnGameStarted -= HandleGameStarted;
        GameEvents.OnBallDrawn -= HandleBallDrawn;
        GameEvents.OnGameEnded -= HandleGameEnded;
        GameEvents.OnNumberVisualUpdate -= HandleNumberVisualUpdate;
    }

    private void HandleNumberVisualUpdate(int number, bool selected)
    {
        if (_numberButtons.TryGetValue(number, out NumberButton button))
        {
            if (selected)
                button.Select();
            else
                button.Deselect();
        }
    }

    private void HandleClearAll()
    {
        foreach (var button in _numberButtons.Values)
            button.ResetState();
    }

    private void HandleQuickPickSelected(List<int> numbers)
    {
        foreach (var button in _numberButtons.Values)
            button.ResetState();

        foreach (int number in numbers)
        {
            if (_numberButtons.TryGetValue(number, out NumberButton button))
                button.Select();
        }
    }

    private void HandleGameStarted()
    {
        SetAllButtonsInteractable(false);

        // Clear drawn/win states from previous round
        foreach (var kvp in _numberButtons)
            kvp.Value.ClearDrawnState();
    }

    private void HandleBallDrawn(int drawnNumber, bool isWinning)
    {
        if (_numberButtons.TryGetValue(drawnNumber, out NumberButton button))
        {
            if (isWinning)
                button.MarkAsWinning();
            else
                button.MarkAsDrawn();
        }
    }

    private void HandleGameEnded()
    {
        SetAllButtonsInteractable(true);

        // DON'T clear drawn/win states automatically
        // Let them stay visible until user clicks on the button
    }

    private void SetAllButtonsInteractable(bool interactable)
    {
        foreach (var button in _numberButtons.Values)
            button.SetInteractable(interactable);
    }
}