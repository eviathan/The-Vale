using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

public partial class InventoryTabViewModel : ViewModelBase
{
    private SaveGameEditor? _save;

    [ObservableProperty] private ItemRowViewModel? _selectedItem;
    [ObservableProperty] private int _duplicateStack = 1;

    public ObservableCollection<ItemRowViewModel> Items { get; } = new();

    public void Bind(SaveGameEditor save)
    {
        _save = save;

        Items.Clear();
        foreach (var item in save.Player.Inventory.Items)
            Items.Add(new ItemRowViewModel(item, RemoveRow));
    }

    [RelayCommand]
    private void DuplicateSelected()
    {
        if (_save is null || SelectedItem is not { } toDuplicate)
            return;

        var added = _save.Player.Inventory.AddCopy(toDuplicate.Item, DuplicateStack);
        Items.Add(new ItemRowViewModel(added, RemoveRow));
    }

    private void RemoveRow(ItemRowViewModel row)
    {
        _save?.Player.Inventory.Remove(row.Item);
        Items.Remove(row);
    }
}
