using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StardewTools.Core.Models;

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
    [ObservableProperty] private ChestRowViewModel? _selectedChest;

    public ObservableCollection<ChestRowViewModel> Chests { get; } = new();
    public ObservableCollection<ItemRowViewModel> SelectedChestItems { get; } = new();

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
        if (value?.Chest.Items is null)
            return;

        foreach (var item in value.Chest.Items.Items)
            SelectedChestItems.Add(new ItemRowViewModel(item, RemoveSelectedChestRow));
    }

    private void RemoveSelectedChestRow(ItemRowViewModel row)
    {
        SelectedChest?.Chest.Items?.Remove(row.Item);
        SelectedChestItems.Remove(row);
    }
}
