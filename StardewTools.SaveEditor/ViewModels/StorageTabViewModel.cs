using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>One entry in the chest list.</summary>
public partial class ChestRowViewModel : ViewModelBase
{
    private readonly int _index;
    private readonly string _itemCountLabel;

    public ChestEditor Chest { get; }

    /// <summary>Real, base-Object field (ChestEditor.Name remarks) - the same rename a player
    /// could do via the chest's own in-game menu, just editable here without walking to it. Drives
    /// Label so a renamed chest is easy to spot in a long list ("#6b: alias chests so we can
    /// easily locate them").</summary>
    [ObservableProperty] private string _name;

    public string Label => $"{Name} #{_index} - {_itemCountLabel}";

    /// <summary>The lid color picked via right-click on a real placed Chest/Stone Chest (see
    /// ChestEditor.PlayerChoiceColor remarks) - black is the real game's own "no custom color"
    /// sentinel, not a genuine black paint job (a player can't actually choose true black via
    /// the in-game color wheel either - confirmed via decompiled Chest.draw()'s own
    /// `playerChoiceColor.Value.Equals(Color.Black)` check for "draw the plain unpainted lid").
    /// Always fully opaque - see OnColorChanged remarks for why anything less breaks rendering.
    /// Constrained to Palette (see below) - a free-form RGB picker let players choose colors the
    /// real game's own chest color wheel can never produce.</summary>
    [ObservableProperty] private Color _color;

    /// <summary>The exact 21 colors selectable here, matching the real in-game chest color wheel
    /// (see ChestColorPalette remarks) - real, reported bug: this used to be a free-form
    /// ColorPicker, letting players pick RGB values no vanilla chest can ever actually have.</summary>
    public static System.Collections.Generic.IReadOnlyList<Color> Palette => MapAssets.ChestColorPalette.Colors;

    public ChestRowViewModel(ChestEditor chest, int index)
    {
        Chest = chest;
        _index = index;
        _itemCountLabel = $"{chest.Items?.Items.Count.ToString() ?? "unknown"} item(s)";
        _name = chest.Name;

        var (r, g, b, a) = chest.PlayerChoiceColor;
        _color = Color.FromArgb(a, r, g, b);
    }

    partial void OnNameChanged(string value)
    {
        Chest.Name = value;
        OnPropertyChanged(nameof(Label));
    }

    /// <summary>Forces full opacity regardless of what the ColorPicker control produces (also
    /// hidden there via IsAlphaEnabled="False" - this is the defensive backstop). Real, reported
    /// bug: decompiled Chest.draw() tints the ENTIRE base chest sprite with
    /// `playerChoiceColor.Value * alpha` - unlike a normal color-multiply highlight, the chosen
    /// color's own alpha directly controls the whole chest's opacity, so any color picked with
    /// alpha &lt; 255 makes the chest partially or fully invisible in-game. Every vanilla-obtainable
    /// chest color is opaque by construction too (decompiled DiscreteColorPicker.getColor() only
    /// ever calls the 3-arg `new Color(r, g, b)`, which XNA defaults to A=255) - so clamping here
    /// matches a real invariant, not an artificial restriction.</summary>
    partial void OnColorChanged(Color value) => Chest.PlayerChoiceColor = (value.R, value.G, value.B, 255);
}

public partial class StorageTabViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddItemCommand))]
    private ChestRowViewModel? _selectedChest;

    [ObservableProperty] private string _itemSearchText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddItemCommand))]
    private PlaceableItem? _selectedNewItem;

    public ObservableCollection<ChestRowViewModel> Chests { get; } = new();
    public ObservableCollection<ItemRowViewModel> SelectedChestItems { get; } = new();
    public ObservableCollection<PlaceableItem> FilteredItems { get; } = new();

    /// <summary>True once a chest is selected and confirmed to have nothing in it - lets the
    /// AXAML show "this chest is empty" instead of just blank space, so an empty-but-working
    /// chest doesn't read as broken.</summary>
    public bool ChestIsEmpty => SelectedChest is not null && SelectedChestItems.Count == 0;

    public StorageTabViewModel() => RefreshFilteredItems();

    public void Bind(SaveGameEditor save)
    {
        Chests.Clear();
        SelectedChestItems.Clear();
        SelectedChest = null;

        var index = 1;
        foreach (var chest in save.Storage.Chests)
            Chests.Add(new ChestRowViewModel(chest, index++));
    }

    /// <summary>#6c: "select a chest on the map, then navigate to its contents here" - matches by
    /// ChestEditor.Identity (the underlying save element), since a ChestEditor built from a placed
    /// Object on the Map tab (PlacedObjectEditor.AsChest()) is a different C# instance from the one
    /// already in Chests, wrapping the same element. Returns whether a match was found, so the
    /// caller (SaveEditorViewModel) can decide what to do if the chest somehow isn't in this list.</summary>
    public bool SelectChest(ChestEditor chest)
    {
        var row = Chests.FirstOrDefault(r => ReferenceEquals(r.Chest.Identity, chest.Identity));
        if (row is null)
            return false;

        SelectedChest = row;
        return true;
    }

    partial void OnSelectedChestChanged(ChestRowViewModel? value)
    {
        SelectedChestItems.Clear();
        if (value?.Chest.Items is not null)
            foreach (var item in value.Chest.Items.Items)
                SelectedChestItems.Add(new ItemRowViewModel(item, RemoveSelectedChestRow));

        OnPropertyChanged(nameof(ChestIsEmpty));
    }

    partial void OnItemSearchTextChanged(string value) => RefreshFilteredItems();

    private void RefreshFilteredItems()
    {
        FilteredItems.Clear();
        var query = ItemSearchText;
        var matches = string.IsNullOrWhiteSpace(query)
            ? PlaceableItems.All
            : PlaceableItems.All.Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var item in matches)
            FilteredItems.Add(item);
    }

    private bool CanAddItem() => SelectedChest?.Chest.Items is not null && SelectedNewItem is not null;

    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private void AddItem()
    {
        if (SelectedChest?.Chest.Items is not { } items || SelectedNewItem is not { } item)
            return;

        var added = items.AddNew(item.Index, item.Name, item.Price, item.Edibility, item.Category, item.Type, 1);
        SelectedChestItems.Add(new ItemRowViewModel(added, RemoveSelectedChestRow));
        OnPropertyChanged(nameof(ChestIsEmpty));
    }

    private void RemoveSelectedChestRow(ItemRowViewModel row)
    {
        SelectedChest?.Chest.Items?.Remove(row.Item);
        SelectedChestItems.Remove(row);
        OnPropertyChanged(nameof(ChestIsEmpty));
    }
}
