using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

public partial class FarmTabViewModel : ViewModelBase
{
    private FarmEditor? _farm;
    private SaveGameEditor? _save;
    private bool _isBound;

    [ObservableProperty] private NamedValue _selectedFarmType = GameEnums.FarmTypes[0];
    [ObservableProperty] private string _weatherForTomorrow = "Sun";
    [ObservableProperty] private double _dailyLuck;

    /// <summary>Real field (Farm.greenhouseUnlocked), gates whether crops planted in the
    /// Greenhouse actually grow - normally flips true on completing the Pantry community-center
    /// bundle (or the Joja equivalent). The Greenhouse building/location itself always exists and
    /// renders the same regardless - this is purely a gameplay-functionality gate, not something
    /// that changes what's shown on the map.</summary>
    [ObservableProperty] private bool _isGreenhouseUnlocked;

    public IReadOnlyList<NamedValue> FarmTypes => GameEnums.FarmTypes;

    public ObservableCollection<string> BuildingTypes { get; } = new();

    /// <summary>Fired when the user confirms "Regenerate Farm" (see IsConfirmingRegenerate/
    /// ConfirmRegenerateCommand below) - SaveEditorViewModel is the only subscriber, forwarding
    /// into MapTabViewModel.RegenerateFarmContent. Kept as an event/callback rather than a direct
    /// reference to MapTabViewModel so this tab doesn't need to know the Map tab exists at all.</summary>
    public event Action<SaveGameEditor>? RegenerateConfirmed;

    /// <summary>Whether the "this will delete everything on your Farm" warning is showing -
    /// picking a Farm Type on its own (SelectedFarmType below) only ever writes the harmless
    /// whichFarm metadata field; actually regenerating the terrain is a separate, explicit,
    /// confirmed step so browsing the dropdown can never accidentally trigger it.</summary>
    [ObservableProperty] private bool _isConfirmingRegenerate;

    public void Bind(SaveGameEditor save)
    {
        _isBound = false;
        _save = save;
        _farm = save.Farm;

        SelectedFarmType = GameEnums.FindOrFirst(GameEnums.FarmTypes, _farm.WhichFarm);
        WeatherForTomorrow = _farm.WeatherForTomorrow;
        DailyLuck = _farm.DailyLuck;
        IsGreenhouseUnlocked = save.Map.GreenhouseUnlocked;

        BuildingTypes.Clear();
        foreach (var type in _farm.BuildingTypes)
            BuildingTypes.Add(type);

        IsConfirmingRegenerate = false;
        _isBound = true;
    }

    partial void OnSelectedFarmTypeChanged(NamedValue value) { if (_isBound && _farm is not null) _farm.WhichFarm = value.Value; }
    partial void OnWeatherForTomorrowChanged(string value) { if (_isBound && _farm is not null) _farm.WeatherForTomorrow = value; }
    partial void OnDailyLuckChanged(double value) { if (_isBound && _farm is not null) _farm.DailyLuck = value; }
    /// <summary>MapTabViewModel's own GreenhouseUnlocked (a rendering-only copy) needs to stay in
    /// sync live, not just at the next Bind() - SaveEditorViewModel wires this into it.</summary>
    public event Action<bool>? GreenhouseUnlockedChanged;

    partial void OnIsGreenhouseUnlockedChanged(bool value)
    {
        if (!_isBound || _save is null)
            return;
        _save.Map.GreenhouseUnlocked = value;
        GreenhouseUnlockedChanged?.Invoke(value);
    }

    [RelayCommand]
    private void RequestRegenerate() => IsConfirmingRegenerate = true;

    [RelayCommand]
    private void CancelRegenerate() => IsConfirmingRegenerate = false;

    [RelayCommand]
    private void ConfirmRegenerate()
    {
        IsConfirmingRegenerate = false;
        if (_save is not null)
            RegenerateConfirmed?.Invoke(_save);
    }
}
