using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.MapAssets;

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

    /// <summary>Item.HasQuality just checks whether the save XML has a &lt;quality&gt; field -
    /// true for every Object, since the game always serializes it. Real quality tiers only
    /// ever get generated for certain categories (see ItemCategories) - showing an editable
    /// quality control for e.g. Stone would be technically harmless but misleading. The
    /// plain-Object guard matters: Chests/Furniture/other subtypes use a completely separate
    /// id space from plain Objects (confirmed - Chest's ParentSheetIndex 130 collides with
    /// Tuna's 130 in Data/Objects.json), so looking up their index there would silently check
    /// the wrong item's category. xsi:type is only emitted when the actual type differs from
    /// the field's declared type (standard XmlSerializer polymorphism) - a plain Object omits
    /// it entirely when placed on the farm (declared type is already Object) but gets an
    /// explicit xsi:type="Object" when carried in inventory (declared type there is the base
    /// Item), so both need treating as "plain object", not just an exact "Object" match.</summary>
    private bool IsPlainObject => Item.ItemType is "" or "Object";

    public bool HasQuality => Item.HasQuality && IsPlainObject && ItemCategories.SupportsQuality(Item.ParentSheetIndex);

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
