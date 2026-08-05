using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor.ViewModels;

public partial class MapTabViewModel : ViewModelBase
{
    private FarmMapEditor? _map;
    private List<MapEntitySummary> _farmEntitiesCache = new();

    [ObservableProperty] private MapEntitySummary? _selected;
    [ObservableProperty] private string _season = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string _contentFolder = "";
    [ObservableProperty] private string _extractStatus = "";
    [ObservableProperty] private bool _isExtracting;
    [ObservableProperty] private string _selectedLocationName = "Farm";

    public ObservableCollection<MapEntitySummary> Entities { get; } = new();
    public ObservableCollection<string> AvailableLocations { get; } = new();

    public MapTabViewModel()
    {
        // Prefer whatever the user explicitly set last time; if this is a fresh machine/settings
        // file, fall back to searching common install locations before giving up and asking.
        var saved = AppSettings.Load().MapContentFolder;
        ContentFolder = !string.IsNullOrEmpty(saved) && Directory.Exists(saved)
            ? saved
            : GameInstallLocator.FindExtractedContentFolder() ?? "";
    }

    partial void OnContentFolderChanged(string value)
    {
        var settings = AppSettings.Load();
        settings.MapContentFolder = value;
        settings.Save();
    }

    partial void OnSelectedLocationNameChanged(string value)
    {
        Selected = null;
        Entities.Clear();

        // Only Farm has placed-entity data wired up (trees/objects/clumps) - other locations
        // show real tile art with no overlay until that's built out per location.
        if (value == "Farm")
            foreach (var entity in _farmEntitiesCache)
                Entities.Add(entity);
    }

    public void Bind(SaveGameEditor save)
    {
        _map = save.Map;
        Season = save.Season;
        Selected = null;

        AvailableLocations.Clear();
        foreach (var name in save.LocationNames.OrderBy(n => n))
            AvailableLocations.Add(name);
        SelectedLocationName = AvailableLocations.Contains("Farm") ? "Farm" : AvailableLocations.FirstOrDefault() ?? "Farm";

        _farmEntitiesCache = new List<MapEntitySummary>();
        foreach (var tree in _map.Trees) _farmEntitiesCache.Add(MapEntitySummary.FromTree(tree));
        foreach (var grass in _map.Grass) _farmEntitiesCache.Add(MapEntitySummary.FromGrass(grass));
        foreach (var clump in _map.ResourceClumps) _farmEntitiesCache.Add(MapEntitySummary.FromClump(clump));
        foreach (var obj in _map.Objects) _farmEntitiesCache.Add(MapEntitySummary.FromObject(obj));
        foreach (var building in _map.Buildings) _farmEntitiesCache.Add(MapEntitySummary.FromBuilding(building));

        Entities.Clear();
        if (SelectedLocationName == "Farm")
            foreach (var entity in _farmEntitiesCache)
                Entities.Add(entity);

        var unmodeled = _map.UnmodeledTerrainFeatures;
        Summary = unmodeled.Count == 0
            ? $"{_farmEntitiesCache.Count} placed entities on the Farm."
            : $"{_farmEntitiesCache.Count} placed entities on the Farm. Also {unmodeled.Count} tile(s) of " +
              $"unmodeled terrain feature type(s) not shown: {string.Join(", ", unmodeled.Select(u => u.Type).Distinct())}.";
    }

    [RelayCommand]
    private async Task AutoExtractAsync()
    {
        IsExtracting = true;
        ExtractStatus = "Locating your game install...";

        try
        {
            var gameFolder = GameInstallLocator.FindGameFolder()
                ?? throw new InvalidOperationException("Couldn't find a local Stardew Valley install in common locations.");

            var progress = new Progress<string>(s => ExtractStatus = s);
            var extracted = await TileArtExtractor.ExtractAsync(gameFolder, progress);
            ContentFolder = extracted;
            ExtractStatus = "Done.";
        }
        catch (Exception ex)
        {
            ExtractStatus = $"Extraction failed: {ex.Message}";
        }
        finally
        {
            IsExtracting = false;
        }
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (_map is null || Selected is not { } entity)
            return;

        switch (entity.Source)
        {
            case TreeEditor tree: _map.Remove(tree); break;
            case GrassEditor grass: _map.Remove(grass); break;
            case ResourceClumpEditor clump: _map.Remove(clump); break;
            case PlacedObjectEditor obj: _map.Remove(obj); break;
            case BuildingEditor building: _map.Remove(building); break;
        }

        Entities.Remove(entity);
        _farmEntitiesCache.Remove(entity);
        Selected = null;
    }
}
