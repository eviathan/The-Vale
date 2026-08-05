using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

public partial class FarmTabViewModel : ViewModelBase
{
    private FarmEditor? _farm;
    private bool _isBound;

    [ObservableProperty] private NamedValue _selectedFarmType = GameEnums.FarmTypes[0];
    [ObservableProperty] private NamedValue _selectedWeather = GameEnums.WeatherTypes[0];
    [ObservableProperty] private double _dailyLuck;

    public IReadOnlyList<NamedValue> FarmTypes => GameEnums.FarmTypes;
    public IReadOnlyList<NamedValue> WeatherTypes => GameEnums.WeatherTypes;

    public ObservableCollection<string> BuildingTypes { get; } = new();

    public void Bind(SaveGameEditor save)
    {
        _isBound = false;
        _farm = save.Farm;

        SelectedFarmType = GameEnums.FindOrFirst(GameEnums.FarmTypes, _farm.WhichFarm);
        SelectedWeather = GameEnums.FindOrFirst(GameEnums.WeatherTypes, _farm.WeatherForTomorrow);
        DailyLuck = _farm.DailyLuck;

        BuildingTypes.Clear();
        foreach (var type in _farm.BuildingTypes)
            BuildingTypes.Add(type);

        _isBound = true;
    }

    partial void OnSelectedFarmTypeChanged(NamedValue value) { if (_isBound && _farm is not null) _farm.WhichFarm = value.Value; }
    partial void OnSelectedWeatherChanged(NamedValue value) { if (_isBound && _farm is not null) _farm.WeatherForTomorrow = value.Value; }
    partial void OnDailyLuckChanged(double value) { if (_isBound && _farm is not null) _farm.DailyLuck = value; }
}
