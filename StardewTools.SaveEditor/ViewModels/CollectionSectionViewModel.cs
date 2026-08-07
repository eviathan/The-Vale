using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>
/// One Collections page (Shipping/Minerals/Cooking/Fish/Artifacts) - same two-list/searchable/
/// multi-select/reselect-after-move UX as RecipesTabViewModel, generalized over NamedValue (id +
/// display name) instead of a bare string, since these dictionaries key on item id, not name.
/// Storage-agnostic by design (keyFor/contains/add/remove delegates rather than a concrete
/// dictionary editor type) - Shipping/Minerals/Cooking are backed by NameCountDictionaryEditor,
/// Fish/Artifacts by ArrayCountDictionaryEditor, and each category's key format differs too
/// (plain id vs. "(O)"-qualified id) - see CollectionsTabViewModel.Bind for how each is wired.
/// </summary>
public partial class CollectionSectionViewModel : ViewModelBase
{
    private readonly IReadOnlyList<NamedValue> _catalog;
    private readonly Func<NamedValue, string> _keyFor;
    private readonly Func<string, bool> _contains;
    private readonly Action<string> _addKey;
    private readonly Action<string> _removeKey;

    public ObservableCollection<NamedValue> Available { get; } = new();
    public ObservableCollection<NamedValue> Collected { get; } = new();
    public ObservableCollection<NamedValue> SelectedAvailableItems { get; } = new();
    public ObservableCollection<NamedValue> SelectedCollectedItems { get; } = new();

    [ObservableProperty] private string _searchText = "";

    public CollectionSectionViewModel(IReadOnlyList<NamedValue> catalog, Func<NamedValue, string> keyFor,
        Func<string, bool> contains, Action<string> addKey, Action<string> removeKey)
    {
        _catalog = catalog;
        _keyFor = keyFor;
        _contains = contains;
        _addKey = addKey;
        _removeKey = removeKey;

        foreach (var item in catalog.Where(i => contains(keyFor(i))).OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
            Collected.Add(item);

        RefreshAvailable();

        SelectedAvailableItems.CollectionChanged += (_, _) => AddCommand.NotifyCanExecuteChanged();
        SelectedCollectedItems.CollectionChanged += (_, _) => RemoveCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value) => RefreshAvailable();

    private void RefreshAvailable()
    {
        Available.Clear();
        var collectedIds = new HashSet<int>(Collected.Select(c => c.Value));
        var query = SearchText;

        foreach (var item in _catalog)
        {
            if (collectedIds.Contains(item.Value))
                continue;
            if (!string.IsNullOrWhiteSpace(query) && !item.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            Available.Add(item);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        var items = SelectedAvailableItems.ToList();
        var anchor = MinIndex(Available, items);

        foreach (var item in items)
            AddItem(item);

        SelectNear(Available, SelectedAvailableItems, anchor);
    }
    private bool CanAdd() => SelectedAvailableItems.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        var items = SelectedCollectedItems.ToList();
        var anchor = MinIndex(Collected, items);

        foreach (var item in items)
            RemoveItem(item);

        SelectNear(Collected, SelectedCollectedItems, anchor);
    }
    private bool CanRemove() => SelectedCollectedItems.Count > 0;

    /// <summary>Public so double-click handlers (MainWindow.axaml.cs) can act on a single
    /// clicked row independent of whatever else is multi-selected.</summary>
    public void AddItem(NamedValue? item)
    {
        if (item is null)
            return;

        var key = _keyFor(item);
        if (_contains(key))
            return;

        _addKey(key);
        InsertSorted(Collected, item);
        Available.Remove(item);
    }

    public void RemoveItem(NamedValue? item)
    {
        if (item is null)
            return;

        _removeKey(_keyFor(item));
        Collected.Remove(item);
        RefreshAvailable();
    }

    private static int MinIndex(ObservableCollection<NamedValue> list, IReadOnlyList<NamedValue> items)
    {
        var indices = items.Select(list.IndexOf).Where(i => i >= 0).ToList();
        return indices.Count > 0 ? indices.Min() : -1;
    }

    private static void SelectNear(ObservableCollection<NamedValue> list, ObservableCollection<NamedValue> selection, int anchor)
    {
        selection.Clear();
        if (anchor < 0 || list.Count == 0)
            return;

        selection.Add(list[Math.Min(anchor, list.Count - 1)]);
    }

    private static void InsertSorted(ObservableCollection<NamedValue> list, NamedValue value)
    {
        var index = 0;
        while (index < list.Count && string.Compare(list[index].Name, value.Name, StringComparison.OrdinalIgnoreCase) < 0)
            index++;

        list.Insert(index, value);
    }
}
