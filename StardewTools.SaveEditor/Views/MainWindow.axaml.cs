using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StardewTools.SaveEditor.MapAssets;
using StardewTools.SaveEditor.ViewModels;

namespace StardewTools.SaveEditor.Views;

public partial class MainWindow : Window
{
    private const string PlaceableItemFormat = "StardewTools.PlaceableItem";
    private const string InventorySlotFormat = "StardewTools.InventorySlotIndex";

    public MainWindow()
    {
        InitializeComponent();

        var grid = this.FindControl<ItemsControl>("InventoryGrid");
        if (grid is not null)
        {
            grid.AddHandler(DragDrop.DragOverEvent, OnInventoryGridDragOver);
            grid.AddHandler(DragDrop.DropEvent, OnInventoryGridDrop);
        }
    }

    private SaveEditorViewModel ViewModel => ((MainWindowViewModel)DataContext!).SaveEditor;

    private async void OnOpenClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open Stardew Valley save file",
            AllowMultiple = false,
        });

        var file = files.FirstOrDefault();
        if (file is null)
            return;

        try
        {
            ViewModel.Load(file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Failed to open: {ex.Message}";
        }
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.Save();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Failed to save: {ex.Message}";
        }
    }

    private async void OnSavePresetClicked(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save preset as",
        });

        if (file is null)
            return;

        try
        {
            ViewModel.SavePreset(file.Path.LocalPath);
            ViewModel.StatusMessage = $"Saved preset {file.Name}";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Failed to save preset: {ex.Message}";
        }
    }

    private async void OnApplyPresetClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open a save file to apply sections from",
            AllowMultiple = false,
        });

        var file = files.FirstOrDefault();
        if (file is null)
            return;

        try
        {
            ViewModel.LoadPresetSource(file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Failed to open preset source: {ex.Message}";
        }
    }

    private void OnCancelPresetClicked(object? sender, RoutedEventArgs e) => ViewModel.CancelPreset();

    private async void OnBrowseMapFolderClicked(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select the folder unpacked by StardewXnbHack (contains a 'Maps' subfolder)",
            AllowMultiple = false,
        });

        var folder = folders.FirstOrDefault();
        if (folder is not null)
            ViewModel.Map.ContentFolder = folder.Path.LocalPath;
    }

    /// <summary>Starts a drag from the picker sidebar - fires on every press, but DragDrop.DoDragDrop
    /// only actually engages a drag session once the pointer moves past the platform's own drag
    /// threshold, so a plain click/select on the ListBox item still behaves normally either way
    /// (this is the standard Avalonia drag-drop sample pattern, not something bespoke here).</summary>
    private async void OnPickerItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // DataContext is whatever FilteredItems holds at this row - PlaceableItem, PlaceableTool,
        // or PlaceableWeapon (see InventoryTabViewModel.RefreshFilteredItems) - DropNewItem takes
        // the union as a plain object and pattern-matches internally, so no branching needed here.
        if (sender is not StyledElement el || el.DataContext is null)
            return;

        var data = new DataObject();
        data.Set(PlaceableItemFormat, el.DataContext);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
    }

    /// <summary>Selects the slot immediately (so a plain click always updates the properties
    /// panel), then - only if it's actually occupied - offers it as a drag source for reordering.
    /// See OnPickerItemPointerPressed for why starting a drag here doesn't interfere with a
    /// plain click.</summary>
    private async void OnSlotPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not StyledElement el || el.DataContext is not InventorySlotViewModel slot || slot.IsLocked)
            return;

        ViewModel.Inventory.SelectedSlot = slot;

        if (slot.Item is null)
            return;

        var data = new DataObject();
        data.Set(InventorySlotFormat, slot.Index);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
    }

    private void OnInventoryGridDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(PlaceableItemFormat) ? DragDropEffects.Copy
            : e.Data.Contains(InventorySlotFormat) ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    /// <summary>The drop target's real slot comes from e.Source.DataContext - DataContext flows
    /// down through the whole per-item template regardless of which nested control (the Border,
    /// the Image, the stack-count TextBlock, ...) the pointer happened to be over at drop time, so
    /// this works without needing pixel-position hit-testing against the grid's own layout.</summary>
    private void OnInventoryGridDrop(object? sender, DragEventArgs e)
    {
        if (e.Source is not StyledElement el || el.DataContext is not InventorySlotViewModel targetSlot)
            return;

        if (e.Data.Get(PlaceableItemFormat) is { } newItem)
            ViewModel.Inventory.DropNewItem(targetSlot.Index, newItem);
        else if (e.Data.Get(InventorySlotFormat) is int sourceIndex)
            ViewModel.Inventory.MoveItem(sourceIndex, targetSlot.Index);
    }

    // Recipes tab: double-click a row to add/remove it immediately, instead of select-then-click
    // a separate button - the buttons still work too, this is just a faster path.
    private void OnAvailableCraftingDoubleTapped(object? sender, TappedEventArgs e) => ViewModel.Recipes.AddCraftingByName((sender as ListBox)?.SelectedItem as string);
    private void OnKnownCraftingDoubleTapped(object? sender, TappedEventArgs e) => ViewModel.Recipes.RemoveCraftingByName((sender as ListBox)?.SelectedItem as string);
    private void OnAvailableCookingDoubleTapped(object? sender, TappedEventArgs e) => ViewModel.Recipes.AddCookingByName((sender as ListBox)?.SelectedItem as string);
    private void OnKnownCookingDoubleTapped(object? sender, TappedEventArgs e) => ViewModel.Recipes.RemoveCookingByName((sender as ListBox)?.SelectedItem as string);

    // Collections tab: one shared DataTemplate (CollectionSectionTemplate) across all three
    // sub-tabs (Shipping/Minerals/Cooking) - the ListBox's own DataContext is whichever
    // CollectionSectionViewModel owns it (inherited from the ContentControl, not overridden by
    // the per-row NamedValue the way SelectedItem is), so these handlers work for all three
    // without needing to know which one they're in.
    private void OnCollectionAvailableDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not ListBox { DataContext: CollectionSectionViewModel section } listBox)
            return;

        section.AddItem(listBox.SelectedItem as NamedValue);
    }

    private void OnCollectionCollectedDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not ListBox { DataContext: CollectionSectionViewModel section } listBox)
            return;

        section.RemoveItem(listBox.SelectedItem as NamedValue);
    }
}
