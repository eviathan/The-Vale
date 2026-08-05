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
    private bool _isBound;

    public ItemEditor Item { get; }

    public string ItemType => Item.ItemType;
    public string Name => Item.Name;
    public bool HasQuality => Item.HasQuality;

    public IReadOnlyList<NamedValue> Qualities => GameEnums.ItemQualities;

    [ObservableProperty] private int _stack;
    [ObservableProperty] private NamedValue _selectedQuality;

    public ItemRowViewModel(ItemEditor item, Action<ItemRowViewModel> onRemove)
    {
        Item = item;
        _onRemove = onRemove;
        _stack = item.Stack;
        _selectedQuality = GameEnums.FindOrFirst(GameEnums.ItemQualities, item.Quality ?? 0);
        _isBound = true;
    }

    partial void OnStackChanged(int value) => Item.Stack = value;

    partial void OnSelectedQualityChanged(NamedValue value)
    {
        if (_isBound && Item.HasQuality)
            Item.Quality = value.Value;
    }

    [RelayCommand]
    private void Remove() => _onRemove(this);
}
