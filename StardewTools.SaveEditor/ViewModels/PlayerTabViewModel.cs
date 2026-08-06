using CommunityToolkit.Mvvm.ComponentModel;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

public partial class PlayerTabViewModel : ViewModelBase
{
    private SaveGameEditor? _save;
    private bool _isBound;

    [ObservableProperty] private string _playerName = "";
    [ObservableProperty] private int _money;
    [ObservableProperty] private int _health;
    [ObservableProperty] private int _maxHealth;
    [ObservableProperty] private int _stamina;
    [ObservableProperty] private string _season = "";
    [ObservableProperty] private int _dayOfMonth;
    [ObservableProperty] private int _year;
    [ObservableProperty] private int _farmingLevel;
    [ObservableProperty] private int _fishingLevel;
    [ObservableProperty] private int _foragingLevel;
    [ObservableProperty] private int _miningLevel;
    [ObservableProperty] private int _combatLevel;
    [ObservableProperty] private int _luckLevel;

    public void Bind(SaveGameEditor save)
    {
        _isBound = false;
        _save = save;

        PlayerName = save.Player.Name;
        Money = save.Player.Money;
        Health = save.Player.Health;
        MaxHealth = save.Player.MaxHealth;
        Stamina = save.Player.Stamina;
        Season = save.Season;
        DayOfMonth = save.DayOfMonth;
        Year = save.Year;
        FarmingLevel = save.Player.FarmingLevel;
        FishingLevel = save.Player.FishingLevel;
        ForagingLevel = save.Player.ForagingLevel;
        MiningLevel = save.Player.MiningLevel;
        CombatLevel = save.Player.CombatLevel;
        LuckLevel = save.Player.LuckLevel;

        _isBound = true;
    }

    partial void OnPlayerNameChanged(string value) { if (_isBound && _save is not null) _save.Player.Name = value; }
    partial void OnMoneyChanged(int value) { if (_isBound && _save is not null) _save.Player.Money = value; }
    partial void OnHealthChanged(int value) { if (_isBound && _save is not null) _save.Player.Health = value; }
    partial void OnMaxHealthChanged(int value) { if (_isBound && _save is not null) _save.Player.MaxHealth = value; }
    partial void OnStaminaChanged(int value) { if (_isBound && _save is not null) _save.Player.Stamina = value; }
    partial void OnSeasonChanged(string value) { if (_isBound && _save is not null) _save.Season = value; }
    partial void OnDayOfMonthChanged(int value) { if (_isBound && _save is not null) _save.DayOfMonth = value; }
    partial void OnYearChanged(int value) { if (_isBound && _save is not null) _save.Year = value; }
    partial void OnFarmingLevelChanged(int value) { if (_isBound && _save is not null) _save.Player.FarmingLevel = value; }
    partial void OnFishingLevelChanged(int value) { if (_isBound && _save is not null) _save.Player.FishingLevel = value; }
    partial void OnForagingLevelChanged(int value) { if (_isBound && _save is not null) _save.Player.ForagingLevel = value; }
    partial void OnMiningLevelChanged(int value) { if (_isBound && _save is not null) _save.Player.MiningLevel = value; }
    partial void OnCombatLevelChanged(int value) { if (_isBound && _save is not null) _save.Player.CombatLevel = value; }
    partial void OnLuckLevelChanged(int value) { if (_isBound && _save is not null) _save.Player.LuckLevel = value; }
}
