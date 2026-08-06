using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>One row in an inventory/chest item list. Edits write straight through to the item.</summary>
public partial class ItemRowViewModel : ViewModelBase
{
    private readonly Action<ItemRowViewModel> _onRemove;
    private readonly Action<ItemRowViewModel>? _onChanged;
    private bool _isBound;

    public ItemEditor Item { get; }

    public string ItemType => Item.ItemType;
    public string Name => Item.Name;
    public bool HasQuality => Item.HasQuality;

    public IReadOnlyList<NamedValue> Qualities => GameEnums.ItemQualities;

    [ObservableProperty] private int _stack;
    [ObservableProperty] private NamedValue _selectedQuality;

    /// <summary><paramref name="onChanged"/> is only needed when something outside this row
    /// (e.g. a map-entity details panel embedding it) needs to react to an edit - the
    /// Inventory/Storage tabs that own this row directly don't need it.</summary>
    public ItemRowViewModel(ItemEditor item, Action<ItemRowViewModel> onRemove, Action<ItemRowViewModel>? onChanged = null)
    {
        Item = item;
        _onRemove = onRemove;
        _onChanged = onChanged;
        _stack = item.Stack;
        _selectedQuality = GameEnums.FindOrFirst(GameEnums.ItemQualities, item.Quality ?? 0);
        _isBound = true;
    }

    partial void OnStackChanged(int value)
    {
        Item.Stack = value;
        if (_isBound) _onChanged?.Invoke(this);
    }

    partial void OnSelectedQualityChanged(NamedValue value)
    {
        if (_isBound && Item.HasQuality)
            Item.Quality = value.Value;
        if (_isBound) _onChanged?.Invoke(this);
    }

    [RelayCommand]
    private void Remove() => _onRemove(this);
}
