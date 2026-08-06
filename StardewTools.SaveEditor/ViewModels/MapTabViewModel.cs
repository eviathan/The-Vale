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

public enum PlacementTool { None, Object, Building }

/// <summary>A placement that's blocked by existing entities - Confirm removes them and places;
/// Cancel just drops this. Blocking is captured up front (at click time), not re-derived at
/// confirm time, so the confirmation panel's list can't drift from what Confirm will actually do.
/// Shared by both new-item placement (the draw tools) and dragging an existing entity to an
/// occupied tile - Label is written to read sensibly for either ("Place X" / "Move Y to (a,b)").</summary>
public sealed record PendingPlacement(string Label, IReadOnlyList<MapEntitySummary> Blocking, Action Confirm);

/// <summary>Fired by FarmMapControl once a click-and-drag that started on an existing entity
/// finishes - see FarmMapControl's move-drag handling. A drag starting on empty space is a
/// marquee range-select instead (SelectedRange), never this.</summary>
public sealed record EntityMoveRequest(MapEntitySummary Entity, TilePosition NewPosition);

public partial class MapTabViewModel : ViewModelBase
{
    private FarmMapEditor? _map;
    private ItemListEditor? _inventory;
    private List<MapEntitySummary> _farmEntitiesCache = new();

    [ObservableProperty] private MapEntitySummary? _selected;
    [ObservableProperty] private MapEntityDetailsViewModel? _selectedDetails;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveRangeCommand))]
    [NotifyCanExecuteChangedFor(nameof(CollectRangeCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelectedRange))]
    private IReadOnlyList<MapEntitySummary> _selectedRange = Array.Empty<MapEntitySummary>();
    [ObservableProperty] private string _season = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string _contentFolder = "";
    [ObservableProperty] private string _extractStatus = "";
    [ObservableProperty] private bool _isExtracting;
    [ObservableProperty] private string _selectedLocationName = "Farm";
    [ObservableProperty] private int _houseUpgradeLevel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlaceObjectCommand))]
    private PlaceableItem? _selectedPlaceableItem;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlaceObjectCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlaceBuildingCommand))]
    private TilePosition? _clickedTile;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlaceBuildingCommand))]
    private PlaceableBuilding? _selectedPlaceableBuilding;

    /// <summary>Set by FarmMapControl (OneWayToSource) once a drag that started on an existing
    /// entity finishes - see OnMoveRequestChanged.</summary>
    [ObservableProperty] private EntityMoveRequest? _moveRequest;

    [ObservableProperty] private PlacementTool _placementTool = PlacementTool.None;

    /// <summary>Bool proxies over PlacementTool so the two toggle buttons in the panel don't
    /// need an enum-to-bool converter - setting one turns the other off (mutually exclusive
    /// tools), and OnPlacementToolChanged keeps both in sync when either one changes.</summary>
    public bool IsObjectToolActive
    {
        get => PlacementTool == PlacementTool.Object;
        set => PlacementTool = value ? PlacementTool.Object : PlacementTool.None;
    }

    public bool IsBuildingToolActive
    {
        get => PlacementTool == PlacementTool.Building;
        set => PlacementTool = value ? PlacementTool.Building : PlacementTool.None;
    }

    /// <summary>Drives FarmMapControl.IsPlacementToolActive - whether either draw tool is
    /// armed, regardless of which one, so a click-and-drag paints instead of marquee-selecting.</summary>
    public bool IsAnyToolActive => PlacementTool != PlacementTool.None;

    partial void OnPlacementToolChanged(PlacementTool value)
    {
        OnPropertyChanged(nameof(IsObjectToolActive));
        OnPropertyChanged(nameof(IsBuildingToolActive));
        OnPropertyChanged(nameof(IsAnyToolActive));
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmPendingPlacementCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelPendingPlacementCommand))]
    private PendingPlacement? _pendingPlacement;

    public ObservableCollection<MapEntitySummary> Entities { get; } = new();
    public ObservableCollection<string> AvailableLocations { get; } = new();
    public IReadOnlyList<PlaceableItem> AvailablePlaceableItems => PlaceableItems.All;
    public IReadOnlyList<PlaceableBuilding> AvailablePlaceableBuildings => PlaceableBuildings.All;

    public MapTabViewModel()
    {
        // Prefer whatever the user explicitly set last time (e.g. after Auto-Extract pulled
        // fresher art from a live install); otherwise the tile art committed to the repo and
        // copied next to the exe at build time (see BundledContent) means there's real art
        // out of the box with no per-machine setup. Searching common install locations is only
        // a last resort for the rare case neither of those is present.
        var saved = AppSettings.Load().MapContentFolder;
        ContentFolder = !string.IsNullOrEmpty(saved) && Directory.Exists(saved)
            ? saved
            : BundledContent.IsAvailable
                ? BundledContent.FolderPath
                : GameInstallLocator.FindExtractedContentFolder() ?? "";
    }

    partial void OnContentFolderChanged(string value)
    {
        var settings = AppSettings.Load();
        settings.MapContentFolder = value;
        settings.Save();
    }

    partial void OnSelectedChanged(MapEntitySummary? value)
    {
        // Editing a field reassigns Selected to a fresh MapEntitySummary wrapping the *same*
        // underlying Source editor (see OnEntityEdited) purely to fire FarmMapControl's
        // AffectsRender - rebuilding SelectedDetails from scratch on every keystroke would
        // reset the panel (losing focus/cursor mid-edit) for no reason, so only rebuild when
        // the selected entity actually changed.
        if (value is not null && SelectedDetails is not null && ReferenceEquals(SelectedDetails.Summary.Source, value.Source))
            return;

        SelectedDetails = value switch
        {
            null => null,
            { Kind: MapEntityKind.Tree, Source: TreeEditor t } => new TreeDetailsViewModel(value, t, OnEntityEdited, RemoveEntity),
            { Kind: MapEntityKind.Grass, Source: GrassEditor g } => new GrassDetailsViewModel(value, g, OnEntityEdited, RemoveEntity),
            { Kind: MapEntityKind.HoeDirt, Source: HoeDirtEditor d } => new HoeDirtDetailsViewModel(value, d, OnEntityEdited, RemoveEntity),
            { Kind: MapEntityKind.ResourceClump, Source: ResourceClumpEditor c } => new ResourceClumpDetailsViewModel(value, c, OnEntityEdited, RemoveEntity),
            { Kind: MapEntityKind.Object, Source: PlacedObjectEditor o } => new PlacedObjectDetailsViewModel(value, o, OnEntityEdited, RemoveEntity),
            { Kind: MapEntityKind.Building, Source: BuildingEditor b } => new BuildingDetailsViewModel(value, b, OnEntityEdited, RemoveEntity),
            _ => null,
        };
    }

    private void OnEntityEdited(MapEntityDetailsViewModel details)
    {
        var fresh = details.Resummarize();
        var oldSummary = details.Summary;

        var idx = Entities.IndexOf(oldSummary);
        if (idx >= 0) Entities[idx] = fresh;

        var cacheIdx = _farmEntitiesCache.IndexOf(oldSummary);
        if (cacheIdx >= 0) _farmEntitiesCache[cacheIdx] = fresh;

        details.Summary = fresh;
        Selected = fresh; // different reference -> AffectsRender fires -> map redraws with the edit
    }

    private void RemoveFromMap(MapEntitySummary entity)
    {
        if (_map is null)
            return;

        switch (entity.Source)
        {
            case TreeEditor tree: _map.Remove(tree); break;
            case GrassEditor grass: _map.Remove(grass); break;
            case HoeDirtEditor dirt: _map.Remove(dirt); break;
            case ResourceClumpEditor clump: _map.Remove(clump); break;
            case PlacedObjectEditor obj: _map.Remove(obj); break;
            case BuildingEditor building: _map.Remove(building); break;
        }
    }

    private void RemoveEntity(MapEntitySummary entity)
    {
        RemoveFromMap(entity);
        Entities.Remove(entity);
        _farmEntitiesCache.Remove(entity);
        Selected = null;
    }

    public bool HasSelectedRange => SelectedRange.Count > 0;

    [RelayCommand(CanExecute = nameof(HasSelectedRange))]
    private void RemoveRange()
    {
        foreach (var entity in SelectedRange.ToList())
        {
            RemoveFromMap(entity);
            Entities.Remove(entity);
            _farmEntitiesCache.Remove(entity);
        }

        SelectedRange = Array.Empty<MapEntitySummary>();
    }

    /// <summary>Removes every entity in the range and deposits its real yield (EntityYields -
    /// a placed Object's "yield" is just itself) into the player's inventory first.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedRange))]
    private void CollectRange()
    {
        foreach (var entity in SelectedRange.ToList())
        {
            if (_inventory is not null)
            {
                foreach (var (index, stack) in EntityYields.Resolve(entity))
                {
                    var meta = PlaceableItems.All.FirstOrDefault(i => i.Index == index && !i.IsBigCraftable);
                    if (meta is not null)
                        _inventory.AddNew(meta.Index, meta.Name, meta.Price, meta.Edibility, meta.Category, meta.Type, stack);
                }
            }

            RemoveFromMap(entity);
            Entities.Remove(entity);
            _farmEntitiesCache.Remove(entity);
        }

        SelectedRange = Array.Empty<MapEntitySummary>();
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
        _inventory = save.Player.Inventory;
        Season = save.Season;
        HouseUpgradeLevel = save.Player.HouseUpgradeLevel;
        Selected = null;
        SelectedRange = Array.Empty<MapEntitySummary>();

        AvailableLocations.Clear();
        foreach (var name in save.LocationNames.OrderBy(n => n))
            AvailableLocations.Add(name);
        SelectedLocationName = AvailableLocations.Contains("Farm") ? "Farm" : AvailableLocations.FirstOrDefault() ?? "Farm";

        _farmEntitiesCache = new List<MapEntitySummary>();
        foreach (var tree in _map.Trees) _farmEntitiesCache.Add(MapEntitySummary.FromTree(tree));
        foreach (var grass in _map.Grass) _farmEntitiesCache.Add(MapEntitySummary.FromGrass(grass));
        foreach (var dirt in _map.HoeDirtTiles) _farmEntitiesCache.Add(MapEntitySummary.FromHoeDirt(dirt));
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

    private bool CanPlaceObject() => _map is not null && SelectedPlaceableItem is not null && ClickedTile is not null && SelectedLocationName == "Farm";

    /// <summary>Places SelectedPlaceableItem at ClickedTile (last click on the map, set by
    /// FarmMapControl regardless of whether it hit an entity - see FarmMapControl.ClickedTile).
    /// Only Objects are placeable so far; trees/grass would need their own item pickers with
    /// type-specific defaults (species, growth stage, ...), not just an index.</summary>
    [RelayCommand(CanExecute = nameof(CanPlaceObject))]
    private void PlaceObject()
    {
        if (SelectedPlaceableItem is { } item && ClickedTile is { } tile)
            PlaceObjectAt(tile, item);
    }

    private void PlaceObjectAt(TilePosition tile, PlaceableItem item)
    {
        if (_map is null)
            return;

        var placed = _map.AddObject(tile, item.Index, item.Name, item.Price, item.Edibility, item.Category, item.Type, item.IsBigCraftable);
        var summary = MapEntitySummary.FromObject(placed);
        _farmEntitiesCache.Add(summary);
        Entities.Add(summary);
        Selected = summary;
    }

    private bool CanPlaceBuilding() => _map is not null && SelectedPlaceableBuilding is not null && ClickedTile is not null && SelectedLocationName == "Farm";

    /// <summary>Places SelectedPlaceableBuilding at ClickedTile - see PlaceableBuildings for
    /// why this is limited to buildings with no interior (Gold Clock, Obelisks, Well, Silo,
    /// Mill, ...); Barn/Coop/etc. need their interior location verified first.</summary>
    [RelayCommand(CanExecute = nameof(CanPlaceBuilding))]
    private void PlaceBuilding()
    {
        if (SelectedPlaceableBuilding is { } building && ClickedTile is { } tile)
            PlaceBuildingAt(tile, building);
    }

    private void PlaceBuildingAt(TilePosition tile, PlaceableBuilding building)
    {
        if (_map is null)
            return;

        var placed = _map.AddBuilding(tile, building.Name, building.TilesWide, building.TilesHigh, building.Magical);
        var summary = MapEntitySummary.FromBuilding(placed);
        _farmEntitiesCache.Add(summary);
        Entities.Add(summary);
        Selected = summary;
    }

    /// <summary>The "draw tool" flow: when a placement tool is active and the map reports a
    /// new click, place immediately if the target footprint is clear, or stage a
    /// PendingPlacement (and wait for the Confirm/Cancel buttons) if something's in the way -
    /// "clicking drops a single at that position if possible [...] if possible to change the
    /// existing tiles to a state where its possible triggers a confirmation".</summary>
    partial void OnClickedTileChanged(TilePosition? value)
    {
        if (value is not { } tile || _map is null || SelectedLocationName != "Farm")
            return;

        switch (PlacementTool)
        {
            case PlacementTool.Object when SelectedPlaceableItem is { } item:
                TryPlaceOrConfirm(tile, 1, 1, $"Place {item}", () => PlaceObjectAt(tile, item));
                break;
            case PlacementTool.Building when SelectedPlaceableBuilding is { } building:
                TryPlaceOrConfirm(tile, building.TilesWide, building.TilesHigh, $"Place {building}", () => PlaceBuildingAt(tile, building));
                break;
        }
    }

    /// <summary>Dragging an existing entity to a new tile - see FarmMapControl's move-drag
    /// handling (a drag starting on empty space is a marquee range-select instead, never this).
    /// Goes through the same TryPlaceOrConfirm staging as a brand-new placement, just excluding
    /// the entity being moved from its own "am I blocked" check.</summary>
    partial void OnMoveRequestChanged(EntityMoveRequest? value)
    {
        if (value is not { } request || _map is null)
            return;

        var (entity, newPosition) = (request.Entity, request.NewPosition);
        TryPlaceOrConfirm(newPosition, entity.Width, entity.Height, $"Move {entity.Label} to ({newPosition.X}, {newPosition.Y})",
            () => MoveEntityTo(entity, newPosition), exclude: entity);
    }

    private void MoveEntityTo(MapEntitySummary entity, TilePosition newPosition)
    {
        if (_map is null)
            return;

        switch (entity.Source)
        {
            case TreeEditor tree: _map.Move(tree, newPosition); break;
            case GrassEditor grass: _map.Move(grass, newPosition); break;
            case HoeDirtEditor dirt: _map.Move(dirt, newPosition); break;
            case ResourceClumpEditor clump: _map.Move(clump, newPosition); break;
            case PlacedObjectEditor obj: _map.Move(obj, newPosition); break;
            case BuildingEditor building: _map.Move(building, newPosition); break;
            default: return;
        }

        // Position is init-only on MapEntitySummary (like every other edit in this file) - the
        // underlying Source editor is the SAME instance (Move updates it in place), only the
        // summary wrapper needs rebuilding so Entities/Selected/SelectedRange pick up the change.
        var fresh = ResummarizeSource(entity.Source);

        var idx = Entities.IndexOf(entity);
        if (idx >= 0) Entities[idx] = fresh;

        var cacheIdx = _farmEntitiesCache.IndexOf(entity);
        if (cacheIdx >= 0) _farmEntitiesCache[cacheIdx] = fresh;

        if (SelectedRange.Contains(entity))
            SelectedRange = SelectedRange.Select(e => ReferenceEquals(e, entity) ? fresh : e).ToList();

        if (ReferenceEquals(Selected, entity))
            Selected = fresh;
    }

    private static MapEntitySummary ResummarizeSource(object source) => source switch
    {
        TreeEditor t => MapEntitySummary.FromTree(t),
        GrassEditor g => MapEntitySummary.FromGrass(g),
        HoeDirtEditor d => MapEntitySummary.FromHoeDirt(d),
        ResourceClumpEditor c => MapEntitySummary.FromClump(c),
        PlacedObjectEditor o => MapEntitySummary.FromObject(o),
        BuildingEditor b => MapEntitySummary.FromBuilding(b),
        _ => throw new InvalidOperationException($"Unknown entity source type: {source.GetType()}."),
    };

    private void TryPlaceOrConfirm(TilePosition tile, int width, int height, string label, Action place, MapEntitySummary? exclude = null)
    {
        var blocking = Entities
            .Where(e => !ReferenceEquals(e, exclude))
            .Where(e => e.Position.X + e.Width - 1 >= tile.X && e.Position.X <= tile.X + width - 1
                     && e.Position.Y + e.Height - 1 >= tile.Y && e.Position.Y <= tile.Y + height - 1)
            .ToList();

        if (blocking.Count == 0)
        {
            place();
            return;
        }

        PendingPlacement = new PendingPlacement(label, blocking, place);
    }

    private bool HasPendingPlacement => PendingPlacement is not null;

    [RelayCommand(CanExecute = nameof(HasPendingPlacement))]
    private void ConfirmPendingPlacement()
    {
        if (PendingPlacement is not { } pending)
            return;

        foreach (var entity in pending.Blocking)
        {
            RemoveFromMap(entity);
            Entities.Remove(entity);
            _farmEntitiesCache.Remove(entity);
        }

        pending.Confirm();
        PendingPlacement = null;
    }

    [RelayCommand(CanExecute = nameof(HasPendingPlacement))]
    private void CancelPendingPlacement() => PendingPlacement = null;

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

}
