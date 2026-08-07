using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>One entry in the chest list.</summary>
public partial class ChestRowViewModel : ViewModelBase
{
    public ChestEditor Chest { get; }
    public string Label { get; }

    public ChestRowViewModel(ChestEditor chest, int index)
    {
        Chest = chest;
        var count = chest.Items?.Items.Count.ToString() ?? "unknown";
        Label = $"Chest #{index} - {count} item(s)";
    }
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
