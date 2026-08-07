using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>One row in the Quests tab - see QuestEditor remarks for why this only edits quests
/// already in the log rather than fabricating new ones.</summary>
public partial class QuestRowViewModel : ViewModelBase
{
    private readonly Action<QuestRowViewModel> _onRemove;
    private bool _isBound;

    public QuestEditor Quest { get; }
    public string Title => Quest.Title;
    public string Description => Quest.Description;
    public string Id => Quest.Id;

    [ObservableProperty] private bool _accepted;
    [ObservableProperty] private bool _completed;

    public QuestRowViewModel(QuestEditor quest, Action<QuestRowViewModel> onRemove)
    {
        Quest = quest;
        _onRemove = onRemove;
        _accepted = quest.Accepted;
        _completed = quest.Completed;
        _isBound = true;
    }

    partial void OnAcceptedChanged(bool value) { if (_isBound) Quest.Accepted = value; }
    partial void OnCompletedChanged(bool value) { if (_isBound) Quest.Completed = value; }

    [RelayCommand]
    private void Remove() => _onRemove(this);
}
