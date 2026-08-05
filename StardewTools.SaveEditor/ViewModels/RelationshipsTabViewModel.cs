using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

public partial class RelationshipsTabViewModel : ViewModelBase
{
    private FriendshipsEditor? _friendships;
    private FriendshipEditor? _selectedFriendship;
    private bool _isBound;

    [ObservableProperty] private string? _selectedNpcName;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private int _points;
    [ObservableProperty] private bool _talkedToToday;

    public ObservableCollection<string> NpcNames { get; } = new();

    public void Bind(SaveGameEditor save)
    {
        _friendships = save.Friendships;

        NpcNames.Clear();
        foreach (var name in _friendships.NpcNames)
            NpcNames.Add(name);

        SelectedNpcName = null;
        HasSelection = false;
    }

    partial void OnSelectedNpcNameChanged(string? value)
    {
        _isBound = false;
        _selectedFriendship = value is null ? null : _friendships?.TryGet(value);

        HasSelection = _selectedFriendship is not null;
        Points = _selectedFriendship?.Points ?? 0;
        TalkedToToday = _selectedFriendship?.TalkedToToday ?? false;

        _isBound = true;
    }

    partial void OnPointsChanged(int value) { if (_isBound && _selectedFriendship is not null) _selectedFriendship.Points = value; }
    partial void OnTalkedToTodayChanged(bool value) { if (_isBound && _selectedFriendship is not null) _selectedFriendship.TalkedToToday = value; }
}
