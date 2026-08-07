using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>
/// Every real relationship-trackable NPC (VillagerRoster - 35 names, not just whichever ones the
/// save already has a &lt;Friendship&gt; entry for), so an unmet villager can still be selected
/// and set from zero - editing any field lazily fabricates the entry via
/// FriendshipsEditor.GetOrCreate on first touch (see EnsureFriendship), matching the exact real
/// shape confirmed against 2 real examples in an actual save.
/// </summary>
public partial class RelationshipsTabViewModel : ViewModelBase
{
    private FriendshipsEditor? _friendships;
    private FriendshipEditor? _selectedFriendship;
    private bool _isBound;

    [ObservableProperty] private string? _selectedNpcName;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _isMet;
    [ObservableProperty] private bool _canBeRomanced;
    [ObservableProperty] private int _points;
    [ObservableProperty] private int _giftsThisWeek;
    [ObservableProperty] private int _giftsToday;
    [ObservableProperty] private bool _talkedToToday;
    [ObservableProperty] private bool _proposalRejected;
    [ObservableProperty] private string _selectedStatus = GameEnums.FriendshipStatuses[0];
    [ObservableProperty] private bool _roommateMarriage;

    public ObservableCollection<string> NpcNames { get; } = new();
    public IReadOnlyList<string> FriendshipStatuses => GameEnums.FriendshipStatuses;

    public void Bind(SaveGameEditor save)
    {
        _friendships = save.Friendships;

        NpcNames.Clear();
        foreach (var villager in VillagerRoster.All)
            NpcNames.Add(villager.Name);

        SelectedNpcName = null;
        HasSelection = false;
    }

    partial void OnSelectedNpcNameChanged(string? value)
    {
        _isBound = false;
        _selectedFriendship = value is null ? null : _friendships?.TryGet(value);

        HasSelection = value is not null;
        IsMet = _selectedFriendship is not null;
        CanBeRomanced = value is not null && VillagerRoster.All.Any(v => v.Name == value && v.CanBeRomanced);
        Points = _selectedFriendship?.Points ?? 0;
        GiftsThisWeek = _selectedFriendship?.GiftsThisWeek ?? 0;
        GiftsToday = _selectedFriendship?.GiftsToday ?? 0;
        TalkedToToday = _selectedFriendship?.TalkedToToday ?? false;
        ProposalRejected = _selectedFriendship?.ProposalRejected ?? false;
        SelectedStatus = _selectedFriendship?.Status ?? GameEnums.FriendshipStatuses[0];
        RoommateMarriage = _selectedFriendship?.RoommateMarriage ?? false;

        _isBound = true;
    }

    /// <summary>Fabricates the &lt;Friendship&gt; entry on first edit of an unmet NPC, so
    /// selecting someone and just looking at their (zeroed) stats doesn't write anything -
    /// only an actual change does.</summary>
    private FriendshipEditor? EnsureFriendship()
    {
        if (_selectedFriendship is null && SelectedNpcName is { } name && _friendships is not null)
        {
            _selectedFriendship = _friendships.GetOrCreate(name);
            IsMet = true;
        }

        return _selectedFriendship;
    }

    partial void OnPointsChanged(int value) { if (_isBound) { var f = EnsureFriendship(); if (f is not null) f.Points = value; } }
    partial void OnGiftsThisWeekChanged(int value) { if (_isBound) { var f = EnsureFriendship(); if (f is not null) f.GiftsThisWeek = value; } }
    partial void OnGiftsTodayChanged(int value) { if (_isBound) { var f = EnsureFriendship(); if (f is not null) f.GiftsToday = value; } }
    partial void OnTalkedToTodayChanged(bool value) { if (_isBound) { var f = EnsureFriendship(); if (f is not null) f.TalkedToToday = value; } }
    partial void OnProposalRejectedChanged(bool value) { if (_isBound) { var f = EnsureFriendship(); if (f is not null) f.ProposalRejected = value; } }
    partial void OnSelectedStatusChanged(string value) { if (_isBound) { var f = EnsureFriendship(); if (f is not null) f.Status = value; } }
    partial void OnRoommateMarriageChanged(bool value) { if (_isBound) { var f = EnsureFriendship(); if (f is not null) f.RoommateMarriage = value; } }
}
