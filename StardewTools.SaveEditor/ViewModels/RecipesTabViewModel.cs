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
/// Known crafting/cooking recipes. The "available" lists are searchable and already exclude
/// recipes you know (nothing to add twice, no scrolling past 150 entries to find the 20 you
/// don't have). Each list supports multi-select (shift-click a range, ctrl/cmd-click to
/// toggle individual rows, ctrl/cmd-A to select all - see MainWindow.axaml's
/// SelectionMode="Multiple") - Add/Remove move every selected row at once. After a move, the
/// selection lands on whatever now sits at the same position the moved item(s) vacated, so
/// clicking Add/Remove repeatedly keeps working through a list without having to reselect each
/// time (see SelectNear).
/// </summary>
public partial class RecipesTabViewModel : ViewModelBase
{
    private RecipeListEditor? _crafting;
    private RecipeListEditor? _cooking;

    public ObservableCollection<string> AvailableCrafting { get; } = new();
    public ObservableCollection<string> KnownCrafting { get; } = new();
    public ObservableCollection<string> AvailableCooking { get; } = new();
    public ObservableCollection<string> KnownCooking { get; } = new();

    /// <summary>Bound to each ListBox's SelectedItems (see MainWindow.axaml) - Avalonia mutates
    /// these collections directly as the user shift/ctrl-clicks, so CanExecute is refreshed via
    /// CollectionChanged rather than a normal property setter.</summary>
    public ObservableCollection<string> SelectedAvailableCraftingItems { get; } = new();
    public ObservableCollection<string> SelectedKnownCraftingItems { get; } = new();
    public ObservableCollection<string> SelectedAvailableCookingItems { get; } = new();
    public ObservableCollection<string> SelectedKnownCookingItems { get; } = new();

    [ObservableProperty] private string _craftingSearchText = "";
    [ObservableProperty] private string _cookingSearchText = "";

    public RecipesTabViewModel()
    {
        SelectedAvailableCraftingItems.CollectionChanged += (_, _) => AddCraftingCommand.NotifyCanExecuteChanged();
        SelectedKnownCraftingItems.CollectionChanged += (_, _) => RemoveCraftingCommand.NotifyCanExecuteChanged();
        SelectedAvailableCookingItems.CollectionChanged += (_, _) => AddCookingCommand.NotifyCanExecuteChanged();
        SelectedKnownCookingItems.CollectionChanged += (_, _) => RemoveCookingCommand.NotifyCanExecuteChanged();
    }

    public void Bind(SaveGameEditor save)
    {
        _crafting = save.Player.CraftingRecipes;
        _cooking = save.Player.CookingRecipes;

        KnownCrafting.Clear();
        foreach (var name in _crafting.KnownRecipeNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            KnownCrafting.Add(name);

        KnownCooking.Clear();
        foreach (var name in _cooking.KnownRecipeNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            KnownCooking.Add(name);

        RefreshAvailableCrafting();
        RefreshAvailableCooking();
    }

    partial void OnCraftingSearchTextChanged(string value) => RefreshAvailableCrafting();
    partial void OnCookingSearchTextChanged(string value) => RefreshAvailableCooking();

    private void RefreshAvailableCrafting()
    {
        AvailableCrafting.Clear();
        var known = new HashSet<string>(KnownCrafting);
        foreach (var name in Filter(RecipeCatalog.CraftingRecipeNames, known, CraftingSearchText))
            AvailableCrafting.Add(name);
    }

    private void RefreshAvailableCooking()
    {
        AvailableCooking.Clear();
        var known = new HashSet<string>(KnownCooking);
        foreach (var name in Filter(RecipeCatalog.CookingRecipeNames, known, CookingSearchText))
            AvailableCooking.Add(name);
    }

    private static IEnumerable<string> Filter(IReadOnlyList<string> catalog, HashSet<string> known, string query)
        => catalog.Where(n => !known.Contains(n))
            .Where(n => string.IsNullOrWhiteSpace(query) || n.Contains(query, StringComparison.OrdinalIgnoreCase));

    [RelayCommand(CanExecute = nameof(CanAddCrafting))]
    private void AddCrafting()
    {
        var names = SelectedAvailableCraftingItems.ToList();
        var anchor = MinIndex(AvailableCrafting, names);

        foreach (var name in names)
            AddCraftingByName(name);

        SelectNear(AvailableCrafting, SelectedAvailableCraftingItems, anchor);
    }
    private bool CanAddCrafting() => SelectedAvailableCraftingItems.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRemoveCrafting))]
    private void RemoveCrafting()
    {
        var names = SelectedKnownCraftingItems.ToList();
        var anchor = MinIndex(KnownCrafting, names);

        foreach (var name in names)
            RemoveCraftingByName(name);

        SelectNear(KnownCrafting, SelectedKnownCraftingItems, anchor);
    }
    private bool CanRemoveCrafting() => SelectedKnownCraftingItems.Count > 0;

    [RelayCommand(CanExecute = nameof(CanAddCooking))]
    private void AddCooking()
    {
        var names = SelectedAvailableCookingItems.ToList();
        var anchor = MinIndex(AvailableCooking, names);

        foreach (var name in names)
            AddCookingByName(name);

        SelectNear(AvailableCooking, SelectedAvailableCookingItems, anchor);
    }
    private bool CanAddCooking() => SelectedAvailableCookingItems.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRemoveCooking))]
    private void RemoveCooking()
    {
        var names = SelectedKnownCookingItems.ToList();
        var anchor = MinIndex(KnownCooking, names);

        foreach (var name in names)
            RemoveCookingByName(name);

        SelectNear(KnownCooking, SelectedKnownCookingItems, anchor);
    }
    private bool CanRemoveCooking() => SelectedKnownCookingItems.Count > 0;

    /// <summary>Lowest index any of the given names currently occupies in list - "where the
    /// gap will open up" once they're removed, so the post-move selection can land there.</summary>
    private static int MinIndex(ObservableCollection<string> list, IReadOnlyList<string> names)
    {
        var indices = names.Select(list.IndexOf).Where(i => i >= 0).ToList();
        return indices.Count > 0 ? indices.Min() : -1;
    }

    /// <summary>Re-selects whatever now sits at (or just past) the vacated position, so clicking
    /// Add/Remove again immediately acts on the next item instead of requiring a fresh click to
    /// reselect - this is the whole point of tracking `anchor` before the move.</summary>
    private static void SelectNear(ObservableCollection<string> list, ObservableCollection<string> selection, int anchor)
    {
        selection.Clear();
        if (anchor < 0 || list.Count == 0)
            return;

        selection.Add(list[Math.Min(anchor, list.Count - 1)]);
    }

    /// <summary>Public (not just the batch commands above) so double-click handlers in
    /// MainWindow.axaml.cs can add/remove whatever was actually double-clicked, independent of -
    /// and without disturbing - the current multi-selection.</summary>
    public void AddCraftingByName(string? name)
    {
        if (_crafting is null || name is null || _crafting.IsKnown(name))
            return;

        _crafting.Learn(name);
        InsertSorted(KnownCrafting, name);
        AvailableCrafting.Remove(name);
    }

    public void RemoveCraftingByName(string? name)
    {
        if (_crafting is null || name is null)
            return;

        _crafting.Forget(name);
        KnownCrafting.Remove(name);
        RefreshAvailableCrafting();
    }

    public void AddCookingByName(string? name)
    {
        if (_cooking is null || name is null || _cooking.IsKnown(name))
            return;

        _cooking.Learn(name);
        InsertSorted(KnownCooking, name);
        AvailableCooking.Remove(name);
    }

    public void RemoveCookingByName(string? name)
    {
        if (_cooking is null || name is null)
            return;

        _cooking.Forget(name);
        KnownCooking.Remove(name);
        RefreshAvailableCooking();
    }

    private static void InsertSorted(ObservableCollection<string> list, string value)
    {
        var index = 0;
        while (index < list.Count && string.Compare(list[index], value, StringComparison.OrdinalIgnoreCase) < 0)
            index++;

        list.Insert(index, value);
    }
}
