using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

public partial class AchievementsTabViewModel : ViewModelBase
{
    private AchievementsEditor? _achievements;

    [ObservableProperty] private NamedValue? _selectedNewAchievement;
    [ObservableProperty] private NamedValue? _selectedUnlocked;

    /// <summary>Every known achievement (see GameEnums.AchievementNames), for the "add" picker.</summary>
    public IReadOnlyList<NamedValue> AvailableAchievements { get; }
        = GameEnums.AchievementNames
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new NamedValue(kvp.Key, GameEnums.AchievementLabel(kvp.Key)))
            .ToList();

    public ObservableCollection<NamedValue> Unlocked { get; } = new();

    public void Bind(SaveGameEditor save)
    {
        _achievements = save.Achievements;

        Unlocked.Clear();
        foreach (var id in _achievements.Ids)
            Unlocked.Add(new NamedValue(id, GameEnums.AchievementLabel(id)));
    }

    [RelayCommand]
    private void Add()
    {
        if (_achievements is null || SelectedNewAchievement is not { } toAdd || _achievements.Contains(toAdd.Value))
            return;

        _achievements.Add(toAdd.Value);
        Unlocked.Add(toAdd);
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (_achievements is null || SelectedUnlocked is not { } toRemove)
            return;

        _achievements.Remove(toRemove.Value);
        Unlocked.Remove(toRemove);
    }
}
