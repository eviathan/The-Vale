using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>
/// The player's inventory as a fixed 12-wide grid (matches the real game's own InventoryMenu:
/// capacity/rows columns, always 36 cells drawn with everything beyond the real backpack size
/// shown locked rather than hidden - see InventorySlotViewModel). A left sidebar lists every
/// real placeable item (drag or "Add" onto an empty slot); selecting an occupied slot shows its
/// Stack/Quality in a properties panel on the right, gated per-item the same way the Storage tab
/// already does (see ItemRowViewModel.IsStackable/HasQuality).
/// </summary>
public partial class InventoryTabViewModel : ViewModelBase
{
    /// <summary>Matches the real game's own InventoryMenu default (capacity 36, rows 3) - always
    /// drawn as the full 3-row grid regardless of the player's actual backpack size, with
    /// anything beyond it locked (see InventorySlotViewModel.IsLocked), same as the real menu's
    /// padlock overlay on inaccessible slots.</summary>
    public const int GridSize = 36;
    public const int Columns = 12;

    private SaveGameEditor? _save;
    private bool _isBound;

    public ObservableCollection<InventorySlotViewModel> Slots { get; } = new();

    /// <summary>The "Add item" sidebar's contents - a mix of PlaceableItem (plain objects/
    /// BigCraftables), PlaceableTool, and PlaceableWeapon, since all three are addable from the
    /// same list (see PlaceableIconConverters/DropNewItem, which both pattern-match on the
    /// runtime type). Kept as one flat searchable list rather than three separate pickers so
    /// "find the item I want" works the same way regardless of which class it happens to be.</summary>
    public ObservableCollection<object> FilteredItems { get; } = new();
    public IReadOnlyList<NamedValue> BackpackSizes => GameEnums.BackpackSizes;

    [ObservableProperty] private InventorySlotViewModel? _selectedSlot;
    [ObservableProperty] private NamedValue _selectedBackpackSize = GameEnums.BackpackSizes[0];
    [ObservableProperty] private string _itemSearchText = "";
    [ObservableProperty] private string? _statusMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddSelectedItemCommand))]
    private object? _selectedPickerItem;

    public InventoryTabViewModel()
    {
        for (var i = 0; i < GridSize; i++)
            Slots.Add(new InventorySlotViewModel(i));

        RefreshFilteredItems();
    }

    public void Bind(SaveGameEditor save)
    {
        _save = save;
        _isBound = false;

        // Self-normalize: guarantees SlotCount == MaxItems even against a save that predates
        // this tool's own writes ever touching it (see PlayerEditor.MaxItems/ItemListEditor.Resize).
        save.Player.MaxItems = save.Player.MaxItems;

        SelectedSlot = null;
        StatusMessage = null;
        SelectedBackpackSize = GameEnums.FindOrFirst(GameEnums.BackpackSizes, save.Player.MaxItems);
        RefreshSlots();
        _isBound = true;
    }

    partial void OnSelectedSlotChanged(InventorySlotViewModel? oldValue, InventorySlotViewModel? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
    }

    partial void OnSelectedBackpackSizeChanged(NamedValue value)
    {
        if (!_isBound || _save is null)
            return;

        _save.Player.MaxItems = value.Value;

        var actual = _save.Player.MaxItems;
        if (actual != value.Value)
        {
            StatusMessage = $"Couldn't shrink to {value.Value} - items exist beyond slot {actual} (backpack stayed at {actual}).";
            _isBound = false;
            SelectedBackpackSize = GameEnums.FindOrFirst(GameEnums.BackpackSizes, actual);
            _isBound = true;
        }
        else
        {
            StatusMessage = null;
        }

        RefreshSlots();
    }

    partial void OnItemSearchTextChanged(string value) => RefreshFilteredItems();

    private void RefreshFilteredItems()
    {
        FilteredItems.Clear();
        var query = ItemSearchText;

        bool Matches(string name) => string.IsNullOrWhiteSpace(query) || name.Contains(query, StringComparison.OrdinalIgnoreCase);

        // Objects/BigCraftables first (by far the largest group), then tools, then weapons -
        // each group internally name-sorted already (see each Load()), so search results stay
        // predictable rather than fully interleaved by name across three unrelated categories.
        foreach (var item in PlaceableItems.All)
            if (Matches(item.Name))
                FilteredItems.Add(item);

        foreach (var tool in PlaceableTools.All)
            if (Matches(tool.Name))
                FilteredItems.Add(tool);

        foreach (var weapon in PlaceableWeapons.All)
            if (Matches(weapon.Name))
                FilteredItems.Add(weapon);
    }

    private void RefreshSlots()
    {
        if (_save is null)
            return;

        var maxItems = _save.Player.MaxItems;
        var selectedIndex = SelectedSlot?.Index;

        for (var i = 0; i < GridSize; i++)
        {
            var slot = Slots[i];
            slot.IsLocked = i >= maxItems;

            var editor = _save.Player.Inventory.GetSlot(i);
            var index = i;
            slot.Item = editor is null ? null : new ItemRowViewModel(editor, _ => ClearSlot(index));
        }

        SelectedSlot = selectedIndex is int idx ? Slots[idx] : null;
    }

    private void ClearSlot(int index)
    {
        if (_save is null)
            return;

        _save.Player.Inventory.ClearSlot(index);
        StatusMessage = null;
        RefreshSlots();
    }

    /// <summary>Drops a new item (from the picker sidebar, or the "Add" button) onto a grid slot -
    /// entry is whatever FilteredItems holds (PlaceableItem/PlaceableTool/PlaceableWeapon).
    /// Places it if the slot's empty; for a stackable PlaceableItem onto a matching occupied
    /// slot, adds one to the stack instead. Tools/weapons never stack (real game rule - see
    /// ItemEditor.MaximumStackSize), so dropping one onto any occupied slot is just blocked with
    /// a message rather than attempting a merge that could never succeed.</summary>
    public void DropNewItem(int targetIndex, object entry)
    {
        if (_save is null || targetIndex < 0 || targetIndex >= GridSize)
            return;

        var slot = Slots[targetIndex];
        if (slot.IsLocked)
        {
            StatusMessage = $"Slot {targetIndex + 1} is locked - increase backpack size first.";
            return;
        }

        var existing = slot.Item;
        var name = entry switch { PlaceableItem i => i.Name, PlaceableTool t => t.Name, PlaceableWeapon w => w.Name, _ => "item" };

        if (existing is null)
        {
            switch (entry)
            {
                case PlaceableItem item:
                    _save.Player.Inventory.SetSlotToNewObject(targetIndex, item.Index, item.Name, item.Price, item.Edibility, item.Category, item.Type, 1);
                    break;
                case PlaceableTool tool:
                    _save.Player.Inventory.SetSlotToNewTool(targetIndex, tool.ClassName, tool.Name, tool.SpriteIndex, tool.MenuSpriteIndex, tool.UpgradeLevel);
                    break;
                case PlaceableWeapon weapon:
                    _save.Player.Inventory.SetSlotToNewWeapon(targetIndex, weapon.Name, weapon.SpriteIndex, weapon.Type, weapon.MinDamage, weapon.MaxDamage,
                        weapon.Speed, weapon.Precision, weapon.Defense, weapon.AreaOfEffect, weapon.Knockback, weapon.CritChance, weapon.CritMultiplier);
                    break;
                default:
                    return;
            }

            StatusMessage = null;
            RefreshSlots();
            SelectedSlot = Slots[targetIndex];
            return;
        }

        if (entry is PlaceableItem placeableItem)
        {
            var sameItem = existing.Item.ParentSheetIndex == placeableItem.Index && existing.Item.BigCraftable == placeableItem.IsBigCraftable;
            if (sameItem && existing.IsStackable && existing.Stack < existing.MaximumStackSize)
            {
                existing.Stack += 1;
                StatusMessage = null;
                return;
            }

            StatusMessage = sameItem
                ? $"{name} is already at max stack in that slot."
                : $"Slot {targetIndex + 1} already has {existing.Name} - drop onto an empty slot instead.";
            return;
        }

        StatusMessage = $"Slot {targetIndex + 1} already has {existing.Name} - drop onto an empty slot instead.";
    }

    /// <summary>Drags an already-carried item from one slot to another - swaps the two slots'
    /// contents (matches the real inventory menu's own drag-to-swap behavior, and means nothing
    /// is ever lost even when the target is occupied).</summary>
    public void MoveItem(int sourceIndex, int targetIndex)
    {
        if (_save is null || sourceIndex == targetIndex)
            return;

        if (targetIndex < 0 || targetIndex >= GridSize || Slots[targetIndex].IsLocked)
        {
            StatusMessage = "Can't move an item onto a locked slot.";
            return;
        }

        _save.Player.Inventory.SwapSlots(sourceIndex, targetIndex);
        StatusMessage = null;
        RefreshSlots();
        SelectedSlot = Slots[targetIndex];
    }

    private bool CanAddSelectedItem() => SelectedPickerItem is not null;

    [RelayCommand(CanExecute = nameof(CanAddSelectedItem))]
    private void AddSelectedItem()
    {
        if (SelectedPickerItem is not { } item)
            return;

        var target = Slots.FirstOrDefault(s => !s.IsLocked && s.Item is null);
        if (target is null)
        {
            StatusMessage = "No empty, unlocked slot to add to - free one up or increase backpack size.";
            return;
        }

        DropNewItem(target.Index, item);
    }
}
