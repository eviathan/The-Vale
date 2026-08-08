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

public enum PlacementTool { None, Object, Building, Till, PlantCrop, PlantTree, PlantBush }

/// <summary>A placement that's blocked by existing entities - Confirm removes them and places;
/// Cancel just drops this. Blocking is captured up front (at click time), not re-derived at
/// confirm time, so the confirmation panel's list can't drift from what Confirm will actually do.
/// Shared by both new-item placement (the draw tools) and dragging an existing entity to an
/// occupied tile - Label is written to read sensibly for either ("Place X" / "Move Y to (a,b)").</summary>
public sealed record PendingPlacement(string Label, IReadOnlyList<MapEntitySummary> Blocking, Action Confirm);

/// <summary>Fired by FarmMapControl once a click-and-drag that started on an existing entity
/// finishes - see FarmMapControl's move-drag handling. A drag starting on empty space is a
/// marquee range-select instead (SelectedRange), never this. Moves.Count > 1 when the press
/// landed on an entity that was part of the current SelectedRange - the whole marquee-selected
/// group drags together, each keeping its position relative to the others.</summary>
public sealed record EntityMoveRequest(IReadOnlyList<(MapEntitySummary Entity, TilePosition NewPosition)> Moves)
{
    public string DescribeLabel() => Moves.Count == 1
        ? $"Move {Moves[0].Entity.Label} to ({Moves[0].NewPosition.X}, {Moves[0].NewPosition.Y})"
        : $"Move {Moves.Count} entities";
}

public partial class MapTabViewModel : ViewModelBase
{
    private SaveGameEditor? _save;
    private FarmMapEditor? _map;

    /// <summary>Which real location _map currently represents - null until Bind(). Compared
    /// against SelectedLocationName to know whether editing tools should be enabled: they were
    /// hardcoded to "only when SelectedLocationName == Farm" before interior editing existed;
    /// now it's "only when the currently-bound map IS the currently-selected location", which
    /// covers Farm and any resolved building interior the same way, and stays false while merely
    /// browsing an unsupported location's read-only tile art (e.g. Town, Beach).</summary>
    private string? _mapLocationName;

    private ItemListEditor? _inventory;
    private List<MapEntitySummary> _farmEntitiesCache = new();

    /// <summary>Farm -> Greenhouse -> ... breadcrumb for EnterLocation/BackToParentLocation -
    /// only ever pushed to by entering a building interior, so "back" always means "go up one
    /// level", never a general location-picker history.</summary>
    private readonly Stack<string> _locationHistory = new();

    /// <summary>Own copy of the parsed map, independent of FarmMapControl's - used only for
    /// tile-placement validation (see CanPlaceFootprint), not rendering, so the ViewModel can
    /// enforce the game's real placement rules without reaching into the View's internals.</summary>
    private TmxMap? _tmxMap;

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
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlaceObjectCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlaceBuildingCommand))]
    private string _selectedLocationName = "Farm";
    [ObservableProperty] private int _houseUpgradeLevel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackToParentLocationCommand))]
    private bool _hasLocationHistory;
    [ObservableProperty] private string _locationBreadcrumb = "Farm";

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

    [ObservableProperty] private PlaceableCrop? _selectedPlaceableCrop;

    /// <summary>Whether the Plant Crop tool plants at harvest-ready maturity (the common case -
    /// "just plant already matured crops") or as a freshly-planted seed (currentPhase 0).</summary>
    [ObservableProperty] private bool _plantCropMature = true;

    [ObservableProperty] private NamedValue _selectedTreeType = GameEnums.TreeTypes[0];

    /// <summary>0-5, default 5 (adult - see TreeEditor.GrowthStage remarks) - "just plant
    /// already matured trees" by default, but still adjustable for a sapling/etc.</summary>
    [ObservableProperty] private int _plantTreeGrowthStage = 5;

    [ObservableProperty] private NamedValue _selectedBushSize = GameEnums.BushSizes[1];

    /// <summary>Whether a newly-planted bush starts bloom/harvest-ready (TileSheetOffset 1) -
    /// same "just plant already matured" default as crops/trees.</summary>
    [ObservableProperty] private bool _plantBushMature = true;

    /// <summary>Player.Stats.DaysPlayed - only needed for a placed tea bush's age-based growth
    /// sprite (see BushEditor.DatePlanted / FarmMapControl.CurrentDaysPlayed). Set once in Bind()
    /// and bound OneWay through to FarmMapControl; not itself editable from the Map tab.</summary>
    [ObservableProperty] private int _currentDaysPlayed;

    public const int MaxBrushSize = 9;

    /// <summary>How many tiles square the Object/Till/Plant Crop/Plant Tree tools stamp per
    /// click (centered on the clicked tile) - TwoWay bound to FarmMapControl, which changes it
    /// via the [ / ] keyboard shortcut (see FarmMapControl.OnKeyDown). Doesn't apply to the
    /// Building tool - a building keeps its own real footprint regardless of brush size.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HoverFootprintWidth))]
    [NotifyPropertyChangedFor(nameof(HoverFootprintHeight))]
    private int _brushSize = 1;

    /// <summary>What FarmMapControl should outline under the cursor while a draw tool is armed -
    /// the building's own size for the Building tool, BrushSize for everything else. Purely a
    /// rendering hint (OneWay to the control); BrushSize itself is what OnClickedTileChanged
    /// actually stamps with.</summary>
    public int HoverFootprintWidth => PlacementTool == PlacementTool.Building ? SelectedPlaceableBuilding?.TilesWide ?? 1 : BrushSize;
    public int HoverFootprintHeight => PlacementTool == PlacementTool.Building ? SelectedPlaceableBuilding?.TilesHigh ?? 1 : BrushSize;

    /// <summary>Set by FarmMapControl (OneWayToSource) once a drag that started on an existing
    /// entity finishes - see OnMoveRequestChanged.</summary>
    [ObservableProperty] private EntityMoveRequest? _moveRequest;

    [ObservableProperty] private PlacementTool _placementTool = PlacementTool.None;

    /// <summary>Bool proxies over PlacementTool so the toolbar's toggle buttons don't need an
    /// enum-to-bool converter - setting one turns the others off (mutually exclusive tools), and
    /// OnPlacementToolChanged keeps all of them in sync when any one changes. IsSelectToolActive
    /// is the "no draw tool armed" state (plain click-select/marquee) - setting it just clears
    /// PlacementTool, same as any other tool being turned off.</summary>
    public bool IsSelectToolActive
    {
        get => PlacementTool == PlacementTool.None;
        set { if (value) PlacementTool = PlacementTool.None; }
    }

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

    public bool IsTillToolActive
    {
        get => PlacementTool == PlacementTool.Till;
        set => PlacementTool = value ? PlacementTool.Till : PlacementTool.None;
    }

    public bool IsPlantCropToolActive
    {
        get => PlacementTool == PlacementTool.PlantCrop;
        set => PlacementTool = value ? PlacementTool.PlantCrop : PlacementTool.None;
    }

    public bool IsPlantTreeToolActive
    {
        get => PlacementTool == PlacementTool.PlantTree;
        set => PlacementTool = value ? PlacementTool.PlantTree : PlacementTool.None;
    }

    public bool IsPlantBushToolActive
    {
        get => PlacementTool == PlacementTool.PlantBush;
        set => PlacementTool = value ? PlacementTool.PlantBush : PlacementTool.None;
    }

    /// <summary>Drives FarmMapControl.IsPlacementToolActive - whether any draw tool is armed,
    /// so a click-and-drag paints instead of marquee-selecting.</summary>
    public bool IsAnyToolActive => PlacementTool != PlacementTool.None;

    partial void OnPlacementToolChanged(PlacementTool value)
    {
        OnPropertyChanged(nameof(IsSelectToolActive));
        OnPropertyChanged(nameof(IsObjectToolActive));
        OnPropertyChanged(nameof(IsBuildingToolActive));
        OnPropertyChanged(nameof(IsTillToolActive));
        OnPropertyChanged(nameof(IsPlantCropToolActive));
        OnPropertyChanged(nameof(IsPlantTreeToolActive));
        OnPropertyChanged(nameof(IsPlantBushToolActive));
        OnPropertyChanged(nameof(IsAnyToolActive));
        OnPropertyChanged(nameof(HoverFootprintWidth));
        OnPropertyChanged(nameof(HoverFootprintHeight));
    }

    partial void OnSelectedPlaceableBuildingChanged(PlaceableBuilding? value)
    {
        OnPropertyChanged(nameof(HoverFootprintWidth));
        OnPropertyChanged(nameof(HoverFootprintHeight));
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmPendingPlacementCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelPendingPlacementCommand))]
    private PendingPlacement? _pendingPlacement;

    /// <summary>Set when a placement/move was rejected because the target tile(s) don't allow
    /// it per the game's own rules (water, no back tile, "NoFurniture", not "Buildable"/
    /// "Diggable" for a building) - see CanPlaceFootprint. Shown briefly in the side panel;
    /// cleared on the next successful placement/move attempt.</summary>
    [ObservableProperty] private string? _placementBlockedMessage;

    public ObservableCollection<MapEntitySummary> Entities { get; } = new();
    public ObservableCollection<string> AvailableLocations { get; } = new();
    public IReadOnlyList<PlaceableItem> AvailablePlaceableItems => PlaceableItems.All;
    public IReadOnlyList<PlaceableBuilding> AvailablePlaceableBuildings => PlaceableBuildings.All;
    public IReadOnlyList<PlaceableCrop> AvailablePlaceableCrops => PlaceableCrops.All;
    public IReadOnlyList<NamedValue> AvailableTreeTypes => GameEnums.TreeTypes;
    public IReadOnlyList<NamedValue> AvailableBushSizes => GameEnums.BushSizes;

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
        TryLoadTmxMap();
    }

    /// <summary>Own, render-independent parse of the current location's map, used only to check
    /// real placement rules (CanPlaceFootprint) - best-effort, same as FarmMapControl's own
    /// loader: a missing/unreadable map just means placement checks are skipped (CanPlaceFootprint
    /// returns true, i.e. no restriction) rather than blocking the whole tab.</summary>
    private void TryLoadTmxMap()
    {
        _tmxMap = null;
        if (string.IsNullOrWhiteSpace(ContentFolder))
            return;

        try
        {
            var loader = new MapAssetLoader(ContentFolder);
            if (loader.HasMap(SelectedLocationName))
                _tmxMap = loader.LoadMap(SelectedLocationName);
        }
        catch
        {
            _tmxMap = null;
        }
    }

    /// <summary>
    /// Mirrors the game's own tile-placement rules (GameLocation.isTilePlaceable/isBuildable -
    /// see TmxMap's remarks for exactly how, verified against the decompiled source). Checks
    /// every tile in the footprint, not just the anchor tile, so a multi-tile building can't
    /// have half of itself hanging over water. Buildings use the stricter IsTileBuildable
    /// (matches isBuildable); plain objects use IsTilePlaceable (matches CanItemBePlacedHere's
    /// isTilePlaceable check). Returns true (no restriction) when no map is loaded, so this
    /// never blocks placement in the abstract/no-content-folder view. Doesn't check "already
    /// occupied by another placed entity" - TryPlaceOrConfirm's overlap check covers that
    /// separately (and unlike terrain, that one's fine to resolve via a confirmation dialog).
    /// </summary>
    private bool CanPlaceFootprint(TilePosition tile, int width, int height, bool isBuilding)
    {
        if (_tmxMap is null)
            return true;

        for (var x = tile.X; x < tile.X + width; x++)
        {
            for (var y = tile.Y; y < tile.Y + height; y++)
            {
                var ok = isBuilding ? _tmxMap.IsTileBuildable(x, y) : _tmxMap.IsTilePlaceable(x, y);
                if (!ok)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// CanPlaceFootprint for the core TilesWide x TilesHigh rectangle, plus every one of the
    /// building's own AdditionalPlacementTiles (Data/Buildings.json) - e.g. Farmhouse's mailbox
    /// tile sits outside its main footprint. Confirmed real, enforced validation (see
    /// MAP_AUDIT.md 2.3): GameLocation.buildStructure checks isBuildable for these too, not just
    /// the core rectangle, with an OnlyNeedsToBePassable variant for tiles that don't need to be
    /// strictly Buildable/Diggable - approximated here as IsTilePlaceable (the closest existing
    /// check) rather than replicating isBuildable's separate isTilePassable-based branch exactly.
    /// </summary>
    private bool CanPlaceBuildingFootprint(TilePosition tile, PlaceableBuilding building)
    {
        if (!CanPlaceFootprint(tile, building.TilesWide, building.TilesHigh, isBuilding: true))
            return false;

        foreach (var area in building.AdditionalPlacementTiles)
        {
            for (var dx = 0; dx < area.Width; dx++)
            {
                for (var dy = 0; dy < area.Height; dy++)
                {
                    var extraTile = new TilePosition(tile.X + area.X + dx, tile.Y + area.Y + dy);
                    if (!CanPlaceFootprint(extraTile, 1, 1, isBuilding: !area.OnlyNeedsToBePassable))
                        return false;
                }
            }
        }

        return true;
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
            { Kind: MapEntityKind.Building, Source: BuildingEditor b } => new BuildingDetailsViewModel(value, b, OnEntityEdited, RemoveEntity, EnterLocation, () => HouseUpgradeLevel, v => HouseUpgradeLevel = v),
            { Kind: MapEntityKind.Bush, Source: BushEditor bu } => new BushDetailsViewModel(value, bu, OnEntityEdited, RemoveEntity),
            { Kind: MapEntityKind.Flooring, Source: FlooringEditor fl } => new FlooringDetailsViewModel(value, fl, OnEntityEdited, RemoveEntity),
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
            case BushEditor bush: _map.Remove(bush); break;
            case FlooringEditor flooring: _map.Remove(flooring); break;
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

    /// <summary>Walks every modeled entity kind off a FarmMapEditor - shared by Bind() (for the
    /// Farm) and OnSelectedLocationNameChanged (for a building interior or any other resolvable
    /// location), so both end up with an identical entity list built the same way.</summary>
    private static List<MapEntitySummary> LoadEntitiesFrom(FarmMapEditor map)
    {
        var entities = new List<MapEntitySummary>();
        foreach (var tree in map.Trees) entities.Add(MapEntitySummary.FromTree(tree));
        foreach (var grass in map.Grass) entities.Add(MapEntitySummary.FromGrass(grass));
        foreach (var dirt in map.HoeDirtTiles) entities.Add(MapEntitySummary.FromHoeDirt(dirt));
        foreach (var clump in map.ResourceClumps) entities.Add(MapEntitySummary.FromClump(clump));
        foreach (var obj in map.Objects) entities.Add(MapEntitySummary.FromObject(obj));
        foreach (var bush in map.Bushes) entities.Add(MapEntitySummary.FromBush(bush));
        foreach (var flooring in map.Flooring) entities.Add(MapEntitySummary.FromFlooring(flooring));
        foreach (var building in map.Buildings) entities.Add(MapEntitySummary.FromBuilding(building));
        return entities;
    }

    /// <summary>
    /// Resolves the newly-selected location by its real name (SaveGameEditor.GetLocationMap,
    /// which - per BuildingEditor/SaveGameEditor remarks - works for Farm, Greenhouse, FarmHouse,
    /// and any other top-level location whose &lt;GameLocation&gt; shape FarmMapEditor can read,
    /// not just ones this tool specifically knows about) and, if resolvable, rebinds _map/
    /// _mapLocationName and rebuilds Entities from it - this is what makes "enter a building's
    /// interior" work: EnterLocation just changes SelectedLocationName, and this handles the
    /// rest the same way it already handles the top-level location picker.
    /// If NOT resolvable (e.g. Town, Beach - real locations with no placed-entity modeling here),
    /// _map/_mapLocationName are left as whatever they last were, so the SelectedLocationName ==
    /// _mapLocationName gate on CanPlaceObject/CanPlaceBuilding/OnClickedTileChanged correctly
    /// disables editing while still allowing read-only tile art to load below.
    /// </summary>
    partial void OnSelectedLocationNameChanged(string value)
    {
        Selected = null;
        Entities.Clear();
        TryLoadTmxMap();

        if (_save?.GetLocationMap(value) is { } resolvedMap)
        {
            _map = resolvedMap;
            _mapLocationName = value;
            _farmEntitiesCache = LoadEntitiesFrom(_map);
            foreach (var entity in _farmEntitiesCache)
                Entities.Add(entity);

            var unmodeled = _map.UnmodeledTerrainFeatures;
            Summary = unmodeled.Count == 0
                ? $"{_farmEntitiesCache.Count} placed entities in {value}."
                : $"{_farmEntitiesCache.Count} placed entities in {value}. Also {unmodeled.Count} tile(s) of " +
                  $"unmodeled terrain feature type(s) not shown: {string.Join(", ", unmodeled.Select(u => u.Type).Distinct())}.";
        }
    }

    partial void OnHouseUpgradeLevelChanged(int value)
    {
        if (_save is not null)
            _save.Player.HouseUpgradeLevel = value;
    }

    /// <summary>Pushes the current location onto the breadcrumb stack and switches to name -
    /// called by BuildingDetailsViewModel's "Enter Interior" action. Only ever pushed to by
    /// entering a building interior, so BackToParentLocation always means "go up one level".</summary>
    public void EnterLocation(string name)
    {
        _locationHistory.Push(SelectedLocationName);
        HasLocationHistory = true;
        LocationBreadcrumb = string.Join(" > ", _locationHistory.Reverse().Append(name));
        SelectedLocationName = name;
    }

    private bool CanGoBackToParentLocation() => _locationHistory.Count > 0;

    [RelayCommand(CanExecute = nameof(CanGoBackToParentLocation))]
    private void BackToParentLocation()
    {
        if (_locationHistory.Count == 0)
            return;

        var parent = _locationHistory.Pop();
        HasLocationHistory = _locationHistory.Count > 0;
        LocationBreadcrumb = _locationHistory.Count == 0 ? parent : string.Join(" > ", _locationHistory.Reverse().Append(parent));
        SelectedLocationName = parent;
    }

    public void Bind(SaveGameEditor save)
    {
        _save = save;
        _map = save.Map;
        _mapLocationName = "Farm";
        _inventory = save.Player.Inventory;
        Season = save.Season;
        HouseUpgradeLevel = save.Player.HouseUpgradeLevel;
        CurrentDaysPlayed = save.Stats.DaysPlayed;
        Selected = null;
        SelectedRange = Array.Empty<MapEntitySummary>();
        _locationHistory.Clear();
        HasLocationHistory = false;
        LocationBreadcrumb = "Farm";

        var buildings = _map.Buildings;
        if (buildings.All(b => b.BuildingType != "Farmhouse"))
        {
            // Most saves don't have one yet - the base game only creates this element lazily
            // ("if missing", confirmed against the decompiled Farm.AddDefaultBuildings/
            // GameLocation.AddDefaultBuilding), so until then the house's position is computed
            // purely from the map's FarmHouseEntry property, not tracked anywhere in the save -
            // nothing to select or move. (59, 12) is Farm.GetStarterFarmhouseLocation() with
            // Farm.tmx's actual FarmHouseEntry fallback of (64, 15) - confirmed Farm.tmx sets no
            // FarmHouseEntry override, so the base game's own hardcoded default applies here too.
            // Done before SelectedLocationName is set below so a resolve triggered by that
            // (OnSelectedLocationNameChanged, when switching saves from a non-Farm location)
            // sees the farmhouse already present instead of rebuilding entities twice.
            _map.AddFarmhouse(new TilePosition(59, 12));
        }

        AvailableLocations.Clear();
        foreach (var name in save.LocationNames.OrderBy(n => n))
            AvailableLocations.Add(name);
        SelectedLocationName = AvailableLocations.Contains("Farm") ? "Farm" : AvailableLocations.FirstOrDefault() ?? "Farm";
        TryLoadTmxMap(); // explicit, not just relying on OnSelectedLocationNameChanged - that partial won't fire above if SelectedLocationName happened to already equal "Farm"

        _farmEntitiesCache = LoadEntitiesFrom(_map);

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

    private bool CanPlaceObject() => _map is not null && SelectedPlaceableItem is not null && ClickedTile is not null && SelectedLocationName == _mapLocationName;

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

        if (!CanPlaceFootprint(tile, 1, 1, isBuilding: false))
        {
            PlacementBlockedMessage = $"Can't place {item.Name} at ({tile.X}, {tile.Y}) - the game wouldn't allow an item there (water, no ground, or marked unplaceable).";
            return;
        }

        PlacementBlockedMessage = null;

        // Floor/path items (Wood Path, Cobblestone Path, ...) don't become a placed Object at
        // all in the real game - they add a Flooring to terrainFeatures instead (confirmed via
        // decompiled Object.placementAction's IsFloorPathItem() branch), which is what makes
        // paths join up with their same-type neighbors and never block placing an object on top
        // of them (Flooring is always passable and lives in a separate collision category - see
        // MAP_AUDIT.md's Path/Flooring section). The generic Object path used to place these as
        // an inert static-sprite Object, which is why paths never joined and always blocked
        // further placement on the same tile.
        if (FarmMapEditor.IsFloorPathItemId(item.Index))
        {
            var flooring = _map.AddFlooring(tile, item.Index);
            var flooringSummary = MapEntitySummary.FromFlooring(flooring);
            _farmEntitiesCache.Add(flooringSummary);
            Entities.Add(flooringSummary);
            Selected = flooringSummary;
            return;
        }

        // Chest-family (Chest/Stone/Junimo/Mini-Fridge/Mini-Shipping Bin/Hopper), Auto-Grabber,
        // and a further ~13 Object subclasses (Cask, Item Pedestal, Torch, Signs, Garden Pot,
        // Workbench, Mini-Jukebox, Wood Chipper, Telephone, Crab Pot, Fences) all need their own
        // real shape (an items container and xsi:type="Chest", a heldObject Chest, or their own
        // xsi:type + extra fields respectively) - the generic path used to write all of these as
        // an inert plain Object, which loaded in-game as uninteractable/non-functional rather
        // than the real thing (confirmed via direct user report for Chest; see FarmMapEditor's
        // AddChest/AddAutoGrabber/AddExoticObject remarks for the rest).
        var placed = FarmMapEditor.IsChestId(item.Index)
            ? _map.AddChest(tile, item.Index, item.Name, item.Price)
            : FarmMapEditor.IsAutoGrabberId(item.Index)
                ? _map.AddAutoGrabber(tile, item.Index, item.Name, item.Price, item.Edibility, item.Category, item.Type)
                : FarmMapEditor.IsExoticObjectId(item.Index)
                    ? _map.AddExoticObject(tile, item.Index, item.Name, item.Price, item.Edibility, item.Category, item.Type, item.IsBigCraftable)
                    : _map.AddObject(tile, item.Index, item.Name, item.Price, item.Edibility, item.Category, item.Type, item.IsBigCraftable);
        var summary = MapEntitySummary.FromObject(placed);
        _farmEntitiesCache.Add(summary);
        Entities.Add(summary);
        Selected = summary;
    }

    private bool CanPlaceBuilding() => _map is not null && SelectedPlaceableBuilding is not null && ClickedTile is not null && SelectedLocationName == _mapLocationName;

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

        if (!CanPlaceBuildingFootprint(tile, building))
        {
            PlacementBlockedMessage = $"Can't build {building.Name} at ({tile.X}, {tile.Y}) - part of its footprint (or one of its extra required tiles, e.g. a mailbox spot) isn't buildable ground.";
            return;
        }

        PlacementBlockedMessage = null;
        var placed = _map.AddBuilding(tile, building.Name, building.TilesWide, building.TilesHigh, building.Magical, building.HayCapacity);
        var summary = MapEntitySummary.FromBuilding(placed);
        _farmEntitiesCache.Add(summary);
        Entities.Add(summary);
        Selected = summary;
    }

    private void TillAt(TilePosition tile)
    {
        if (_map is null)
            return;

        var dirt = _map.AddHoeDirt(tile);
        var summary = MapEntitySummary.FromHoeDirt(dirt);
        _farmEntitiesCache.Add(summary);
        Entities.Add(summary);
        Selected = summary;
    }

    /// <summary>Plants into an existing bare (crop-less) HoeDirt tile at this position if one's
    /// already there (the normal till-then-plant flow), otherwise tills a fresh one first -
    /// either way ending with exactly one HoeDirt entity at this tile. PlantCropMature decides
    /// whether it's planted as a freshly-sown seed (currentPhase 0) or already harvest-ready
    /// (currentPhase at the crop's own last real growth phase - "just plant already matured
    /// crops"); fullGrown is only meaningful for regrowable crops (RegrowDays != -1) - see
    /// HoeDirtEditor.PlantCrop remarks for why matching the game's own fullyGrown semantics
    /// matters for harvestability.</summary>
    private void PlantCropAt(TilePosition tile, PlaceableCrop crop)
    {
        if (_map is null)
            return;

        var existing = Entities.FirstOrDefault(e => e.Kind == MapEntityKind.HoeDirt && e.Position == tile);
        var dirt = existing?.Source as HoeDirtEditor ?? _map.AddHoeDirt(tile);

        var currentPhase = PlantCropMature ? crop.MaturePhase : 0;
        var fullGrown = PlantCropMature && crop.RegrowDays != -1;
        var flip = Random.Shared.Next(2) == 0;

        dirt.PlantCrop(crop.SeedIndex, crop.DaysInPhase, crop.RegrowDays, crop.HarvestItemId, crop.HarvestMinStack,
            crop.HarvestMaxStack, crop.HarvestMaxIncreasePerFarmingLevel, crop.IsScytheHarvest, crop.IsRaisedSeeds,
            crop.ExtraHarvestChance, crop.SpriteIndex, crop.Seasons, currentPhase, 0, fullGrown, flip);

        var fresh = MapEntitySummary.FromHoeDirt(dirt);
        if (existing is not null)
        {
            var idx = Entities.IndexOf(existing);
            if (idx >= 0) Entities[idx] = fresh;
            var cacheIdx = _farmEntitiesCache.IndexOf(existing);
            if (cacheIdx >= 0) _farmEntitiesCache[cacheIdx] = fresh;
        }
        else
        {
            _farmEntitiesCache.Add(fresh);
            Entities.Add(fresh);
        }

        Selected = fresh;
    }

    private void PlantTreeAt(TilePosition tile, int treeType, int growthStage)
    {
        if (_map is null)
            return;

        var tree = _map.AddTree(tile, treeType, growthStage);
        var summary = MapEntitySummary.FromTree(tree);
        _farmEntitiesCache.Add(summary);
        Entities.Add(summary);
        Selected = summary;
    }

    private void PlantBushAt(TilePosition tile, int size)
    {
        if (_map is null)
            return;

        var bush = _map.AddBush(tile, size, CurrentDaysPlayed, PlantBushMature ? 1 : 0);
        var summary = MapEntitySummary.FromBush(bush);
        _farmEntitiesCache.Add(summary);
        Entities.Add(summary);
        Selected = summary;
    }

    /// <summary>The "draw tool" flow: when a placement tool is active and the map reports a
    /// new click, place immediately if the target footprint is clear, or stage a
    /// PendingPlacement (and wait for the Confirm/Cancel buttons) if something's in the way -
    /// "clicking drops a single at that position if possible [...] if possible to change the
    /// existing tiles to a state where its possible triggers a confirmation". Object/Till/
    /// PlantCrop/PlantTree stamp a BrushSize x BrushSize area (see ApplyBrushTool) instead of
    /// just the one clicked tile; Building always keeps its own real footprint regardless of
    /// brush size - stamping several buildings at once doesn't make sense the way a wider
    /// tilled patch or a denser thicket of trees does.</summary>
    partial void OnClickedTileChanged(TilePosition? value)
    {
        if (value is not { } tile || _map is null || SelectedLocationName != _mapLocationName)
            return;

        switch (PlacementTool)
        {
            case PlacementTool.Object when SelectedPlaceableItem is { } item:
                // A floor/path item placed on top of existing Flooring should still prompt
                // (two terrain features can't share a tile - see FarmMapEditor.AddFlooring), but
                // every other object should be placeable straight over existing Flooring without
                // a confirmation - real Flooring is always passable and excluded from the game's
                // default placement collision mask (GameLocation.CanItemBePlacedHere), which is
                // exactly why real players can put chests/scarecrows/furniture on their paths.
                ApplyBrushTool(tile, $"Place {item}", $"Can't place {item.Name} there - the game wouldn't allow an item there (water, no ground, or marked unplaceable).",
                    t => CanPlaceFootprint(t, 1, 1, isBuilding: false),
                    ignoreBlocking: FarmMapEditor.IsFloorPathItemId(item.Index) ? null : e => e.Kind == MapEntityKind.Flooring,
                    applyAt: t => PlaceObjectAt(t, item));
                break;
            case PlacementTool.Building when SelectedPlaceableBuilding is { } building:
                if (!CanPlaceBuildingFootprint(tile, building))
                {
                    PlacementBlockedMessage = $"Can't build {building.Name} at ({tile.X}, {tile.Y}) - part of its footprint (or one of its extra required tiles, e.g. a mailbox spot) isn't buildable ground.";
                    break;
                }
                TryPlaceOrConfirm(tile, building.TilesWide, building.TilesHigh, $"Place {building}", () => PlaceBuildingAt(tile, building));
                break;
            case PlacementTool.Till:
                ApplyBrushTool(tile, "Till soil", "Can't till there - not diggable ground.",
                    t => (_tmxMap?.IsTileDiggable(t.X, t.Y) ?? true) && CanPlaceFootprint(t, 1, 1, isBuilding: false)
                        && !Entities.Any(e => e.Kind == MapEntityKind.HoeDirt && e.Position == t), // already tilled - nothing to do, skip silently rather than re-confirm
                    ignoreBlocking: null, applyAt: TillAt);
                break;
            case PlacementTool.PlantCrop when SelectedPlaceableCrop is { } crop:
                ApplyBrushTool(tile, $"Plant {crop.Name}", $"Can't plant {crop.Name} there - not diggable/plantable ground.",
                    t => (_tmxMap?.IsTileDiggable(t.X, t.Y) ?? true) && CanPlaceFootprint(t, 1, 1, isBuilding: false),
                    ignoreBlocking: e => e.Kind == MapEntityKind.HoeDirt && ((HoeDirtEditor)e.Source).Crop is null, // bare tilled soil is fine to plant straight into
                    applyAt: t => PlantCropAt(t, crop));
                break;
            case PlacementTool.PlantTree:
                ApplyBrushTool(tile, $"Plant {SelectedTreeType.Name} tree", "Can't plant a tree there - the game wouldn't allow one there.",
                    t => CanPlaceFootprint(t, 1, 1, isBuilding: false), ignoreBlocking: null,
                    applyAt: t => PlantTreeAt(t, SelectedTreeType.Value, PlantTreeGrowthStage));
                break;
            case PlacementTool.PlantBush:
                // A bush's footprint varies by size (1-3 tiles wide - MapEntitySummary.FootprintWidth),
                // so like Building it keeps its own real footprint per click rather than stamping a
                // BrushSize x BrushSize area - several overlapping wide bushes from one brush stroke
                // wouldn't make sense the way a wider tilled patch does.
                var bushWidth = MapEntitySummary.FootprintWidth(SelectedBushSize.Value);
                if (!CanPlaceFootprint(tile, bushWidth, 1, isBuilding: false))
                {
                    PlacementBlockedMessage = $"Can't plant a bush at ({tile.X}, {tile.Y}) - the game wouldn't allow one there.";
                    break;
                }
                // Bush is a LargeTerrainFeature, a real, separate collision category from
                // Flooring (same "always passable, doesn't block real object placement" rule as
                // the Object tool above) - see FarmMapEditor.AddFlooring's remarks.
                TryPlaceOrConfirm(tile, bushWidth, 1, $"Plant {SelectedBushSize.Name} bush", () => PlantBushAt(tile, SelectedBushSize.Value),
                    ignoreBlocking: e => e.Kind == MapEntityKind.Flooring);
                break;
        }
    }

    /// <summary>Every tile in a BrushSize x BrushSize square centered on (as close as an even
    /// size allows) the given tile - size 1 is just the tile itself.</summary>
    private IEnumerable<TilePosition> BrushTiles(TilePosition center)
    {
        var startX = center.X - (BrushSize - 1) / 2;
        var startY = center.Y - (BrushSize - 1) / 2;
        for (var dx = 0; dx < BrushSize; dx++)
            for (var dy = 0; dy < BrushSize; dy++)
                yield return new TilePosition(startX + dx, startY + dy);
    }

    /// <summary>Shared "brush stroke" flow for the single-tile draw tools. Tiles that fail
    /// isValidTile are silently skipped - a stroke that clips water/an edge/already-tilled soil
    /// just doesn't act there, like a real brush - but if none of the brush's tiles are valid,
    /// PlacementBlockedMessage explains why. Blocking entities across every still-valid tile are
    /// batched into one PendingPlacement/confirmation, same as everywhere else on this map;
    /// ignoreBlocking lets a tool treat some entity kinds (bare tilled soil, for Till/PlantCrop)
    /// as fine to build into directly rather than something to clear first.</summary>
    private void ApplyBrushTool(TilePosition center, string label, string invalidMessage,
        Func<TilePosition, bool> isValidTile, Func<MapEntitySummary, bool>? ignoreBlocking, Action<TilePosition> applyAt)
    {
        var tiles = BrushTiles(center).Where(isValidTile).ToList();
        if (tiles.Count == 0)
        {
            PlacementBlockedMessage = invalidMessage;
            return;
        }

        PlacementBlockedMessage = null;
        var blocking = Entities
            .Where(e => tiles.Any(t => Overlaps(e, t, 1, 1)))
            .Where(e => ignoreBlocking is null || !ignoreBlocking(e))
            .ToList();

        void Apply()
        {
            foreach (var t in tiles)
                applyAt(t);
        }

        if (blocking.Count == 0)
        {
            Apply();
            return;
        }

        var confirmLabel = tiles.Count == 1 ? $"{label} at ({tiles[0].X}, {tiles[0].Y})" : $"{label} at {tiles.Count} tiles";
        PendingPlacement = new PendingPlacement(confirmLabel, blocking, Apply);
    }

    /// <summary>Dragging an existing entity (or, if the press landed on a member of the current
    /// SelectedRange, the whole marquee-selected group) to new tiles - see FarmMapControl's
    /// move-drag handling (a drag starting on empty space is a marquee range-select instead,
    /// never this). Blocked by anything in the way of ANY move in the batch, excluding the
    /// entities being moved from their own "am I blocked" check (they're about to vacate their
    /// current tiles, and a group's members can sit right next to each other en route).</summary>
    partial void OnMoveRequestChanged(EntityMoveRequest? value)
    {
        if (value is not { } request || _map is null || request.Moves.Count == 0)
            return;

        // Terrain rules block the whole batch, not just the offending member - a group drag
        // either all lands or none of it does, rather than silently dropping one entity where
        // the others moved fine.
        var invalidMove = request.Moves.FirstOrDefault(m => !CanPlaceFootprint(m.NewPosition, m.Entity.Width, m.Entity.Height, isBuilding: m.Entity.Kind == MapEntityKind.Building));
        if (invalidMove.Entity is not null)
        {
            PlacementBlockedMessage = $"Can't move {invalidMove.Entity.Label} to ({invalidMove.NewPosition.X}, {invalidMove.NewPosition.Y}) - the game wouldn't allow it there.";
            return;
        }

        PlacementBlockedMessage = null;
        var moving = request.Moves.Select(m => m.Entity).ToHashSet();
        // Existing Flooring only actually blocks a move that's itself a terrain feature (moving
        // onto the same terrainFeatures dictionary slot) - a moved Object/Building/Bush should
        // pass straight over it, same "Flooring never blocks real object placement" rule as the
        // Object/Bush placement tools above (see FarmMapEditor.AddFlooring's remarks).
        static bool IsTerrainFeatureKind(MapEntityKind kind) => kind is MapEntityKind.Tree or MapEntityKind.Grass or MapEntityKind.HoeDirt or MapEntityKind.Flooring;
        var blocking = Entities
            .Where(e => !moving.Contains(e))
            .Where(e => request.Moves.Any(m => Overlaps(e, m.NewPosition, m.Entity.Width, m.Entity.Height)
                && (e.Kind != MapEntityKind.Flooring || IsTerrainFeatureKind(m.Entity.Kind))))
            .Distinct()
            .ToList();

        void ApplyMoves()
        {
            foreach (var (entity, newPosition) in request.Moves)
                MoveEntityTo(entity, newPosition);
        }

        if (blocking.Count == 0)
        {
            ApplyMoves();
            return;
        }

        PendingPlacement = new PendingPlacement(request.DescribeLabel(), blocking, ApplyMoves);
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
            case BushEditor bush: _map.Move(bush, newPosition); break;
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
        BushEditor bu => MapEntitySummary.FromBush(bu),
        FlooringEditor fl => MapEntitySummary.FromFlooring(fl),
        _ => throw new InvalidOperationException($"Unknown entity source type: {source.GetType()}."),
    };

    private void TryPlaceOrConfirm(TilePosition tile, int width, int height, string label, Action place, MapEntitySummary? exclude = null, Func<MapEntitySummary, bool>? ignoreBlocking = null)
    {
        var blocking = Entities
            .Where(e => !ReferenceEquals(e, exclude))
            .Where(e => Overlaps(e, tile, width, height))
            .Where(e => ignoreBlocking is null || !ignoreBlocking(e))
            .ToList();

        if (blocking.Count == 0)
        {
            place();
            return;
        }

        PendingPlacement = new PendingPlacement(label, blocking, place);
    }

    private static bool Overlaps(MapEntitySummary entity, TilePosition tile, int width, int height)
        => entity.Position.X + entity.Width - 1 >= tile.X && entity.Position.X <= tile.X + width - 1
        && entity.Position.Y + entity.Height - 1 >= tile.Y && entity.Position.Y <= tile.Y + height - 1;

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
