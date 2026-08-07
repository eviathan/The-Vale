using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>
/// Quests, in two parts: the active log (whatever's really in your save right now - accept/
/// complete/dismiss, edits only, no fabrication) below a full-roster "Complete a quest" section
/// (same Available/Completed pattern as Collections/Recipes) covering every quest in the game,
/// not just ones currently offered - completing one from the roster either flips
/// Accepted/Completed on it if it's already in your log, or fabricates a full instance already
/// completed if it isn't (see PlayerEditor.CompleteQuest/QuestXmlBuilder).
/// </summary>
public partial class QuestsTabViewModel : ViewModelBase
{
    private PlayerEditor? _player;

    public ObservableCollection<QuestRowViewModel> Quests { get; } = new();

    [ObservableProperty] private CollectionSectionViewModel? _roster;

    public void Bind(SaveGameEditor save)
    {
        _player = save.Player;
        RefreshActiveQuests();

        var catalog = QuestCatalog.All.Select(q => new NamedValue(int.Parse(q.Id), q.Title)).ToList();
        var specsById = QuestCatalog.All.ToDictionary(q => q.Id);

        Roster = new CollectionSectionViewModel(
            catalog,
            keyFor: i => i.Value.ToString(),
            contains: id => _player.IsQuestCompleted(id),
            addKey: id => { _player.CompleteQuest(specsById[id]); RefreshActiveQuests(); },
            removeKey: id => { _player.RemoveQuestById(id); RefreshActiveQuests(); });
    }

    /// <summary>Re-reads the active log from the save - needed after the roster below fabricates
    /// or removes a quest, since that changes what's actually in questLog out from under this
    /// list.</summary>
    private void RefreshActiveQuests()
    {
        if (_player is null)
            return;

        Quests.Clear();
        foreach (var quest in _player.Quests)
            Quests.Add(new QuestRowViewModel(quest, RemoveRow));
    }

    private void RemoveRow(QuestRowViewModel row)
    {
        _player?.RemoveQuest(row.Quest);
        Quests.Remove(row);
    }
}
