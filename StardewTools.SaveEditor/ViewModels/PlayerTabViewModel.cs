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
}
