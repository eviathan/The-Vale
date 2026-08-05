using CommunityToolkit.Mvvm.ComponentModel;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

public partial class StatsTabViewModel : ViewModelBase
{
    private StatsEditor? _stats;
    private bool _isBound;

    [ObservableProperty] private int _daysPlayed;
    [ObservableProperty] private int _stepsTaken;
    [ObservableProperty] private int _monstersKilled;
    [ObservableProperty] private int _itemsShipped;
    [ObservableProperty] private int _itemsCrafted;
    [ObservableProperty] private int _itemsCooked;
    [ObservableProperty] private int _questsCompleted;
    [ObservableProperty] private int _timesFished;
    [ObservableProperty] private int _trufflesFound;
    [ObservableProperty] private int _diamondsFound;
    [ObservableProperty] private int _goldFound;
    [ObservableProperty] private int _iridiumFound;
    [ObservableProperty] private int _totalMoneyGifted;
    [ObservableProperty] private int _individualMoneyEarned;

    public void Bind(SaveGameEditor save)
    {
        _isBound = false;
        _stats = save.Stats;

        DaysPlayed = _stats.DaysPlayed;
        StepsTaken = _stats.StepsTaken;
        MonstersKilled = _stats.MonstersKilled;
        ItemsShipped = _stats.ItemsShipped;
        ItemsCrafted = _stats.ItemsCrafted;
        ItemsCooked = _stats.ItemsCooked;
        QuestsCompleted = _stats.QuestsCompleted;
        TimesFished = _stats.TimesFished;
        TrufflesFound = _stats.TrufflesFound;
        DiamondsFound = _stats.DiamondsFound;
        GoldFound = _stats.GoldFound;
        IridiumFound = _stats.IridiumFound;
        TotalMoneyGifted = _stats.TotalMoneyGifted;
        IndividualMoneyEarned = _stats.IndividualMoneyEarned;

        _isBound = true;
    }

    partial void OnDaysPlayedChanged(int value) { if (_isBound && _stats is not null) _stats.DaysPlayed = value; }
    partial void OnStepsTakenChanged(int value) { if (_isBound && _stats is not null) _stats.StepsTaken = value; }
    partial void OnMonstersKilledChanged(int value) { if (_isBound && _stats is not null) _stats.MonstersKilled = value; }
    partial void OnItemsShippedChanged(int value) { if (_isBound && _stats is not null) _stats.ItemsShipped = value; }
    partial void OnItemsCraftedChanged(int value) { if (_isBound && _stats is not null) _stats.ItemsCrafted = value; }
    partial void OnItemsCookedChanged(int value) { if (_isBound && _stats is not null) _stats.ItemsCooked = value; }
    partial void OnQuestsCompletedChanged(int value) { if (_isBound && _stats is not null) _stats.QuestsCompleted = value; }
    partial void OnTimesFishedChanged(int value) { if (_isBound && _stats is not null) _stats.TimesFished = value; }
    partial void OnTrufflesFoundChanged(int value) { if (_isBound && _stats is not null) _stats.TrufflesFound = value; }
    partial void OnDiamondsFoundChanged(int value) { if (_isBound && _stats is not null) _stats.DiamondsFound = value; }
    partial void OnGoldFoundChanged(int value) { if (_isBound && _stats is not null) _stats.GoldFound = value; }
    partial void OnIridiumFoundChanged(int value) { if (_isBound && _stats is not null) _stats.IridiumFound = value; }
    partial void OnTotalMoneyGiftedChanged(int value) { if (_isBound && _stats is not null) _stats.TotalMoneyGifted = value; }
    partial void OnIndividualMoneyEarnedChanged(int value) { if (_isBound && _stats is not null) _stats.IndividualMoneyEarned = value; }
}
