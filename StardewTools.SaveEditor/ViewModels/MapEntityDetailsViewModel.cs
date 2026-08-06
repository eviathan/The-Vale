using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>
/// Base for the "Selected entity" panel's per-kind editing view. One concrete subclass per
/// <see cref="MapEntityKind"/>, dispatched by runtime type via AXAML DataTemplates keyed on
/// x:DataType (MainWindow.axaml) - the same mechanism ListBox.ItemTemplate already uses for
/// ItemRowViewModel, just not restricted to one type. Adding a new entity kind later means one
/// more subclass + one more DataTemplate block; nothing here changes.
/// </summary>
public abstract partial class MapEntityDetailsViewModel : ViewModelBase
{
    private readonly Action<MapEntityDetailsViewModel> _onEdited;
    private readonly Action<MapEntitySummary> _onRemove;

    protected MapEntityDetailsViewModel(MapEntitySummary summary, Action<MapEntityDetailsViewModel> onEdited, Action<MapEntitySummary> onRemove)
    {
        Summary = summary;
        _onEdited = onEdited;
        _onRemove = onRemove;
    }

    public MapEntitySummary Summary { get; internal set; }
    public TilePosition Position => Summary.Position;

    /// <summary>Rebuilds a fresh MapEntitySummary from current field values. FarmMapControl
    /// reads sprite-affecting fields straight off Source on every render - there's no
    /// per-entity cache to invalidate - but Entities/Selected only trigger a repaint on an
    /// actual reference change, so an edit has to produce a new summary object, not just
    /// mutate the Core editor in place.</summary>
    public abstract MapEntitySummary Resummarize();

    protected void NotifyEdited() => _onEdited(this);

    [RelayCommand]
    private void Remove() => _onRemove(Summary);
}

public sealed partial class TreeDetailsViewModel : MapEntityDetailsViewModel
{
    public TreeEditor Tree { get; }
    public IReadOnlyList<NamedValue> TreeTypes => GameEnums.TreeTypes;

    [ObservableProperty] private NamedValue _selectedTreeType;
    [ObservableProperty] private int _growthStage;
    [ObservableProperty] private int _health;
    [ObservableProperty] private bool _stump;
    [ObservableProperty] private bool _tapped;
    [ObservableProperty] private bool _flipped;
    [ObservableProperty] private bool _hasSeed;
    [ObservableProperty] private bool _fertilized;
    [ObservableProperty] private bool _shakeLeft;
    [ObservableProperty] private bool _hasMoss;
    [ObservableProperty] private bool _wasShakenToday;

    public TreeDetailsViewModel(MapEntitySummary summary, TreeEditor tree, Action<MapEntityDetailsViewModel> onEdited, Action<MapEntitySummary> onRemove)
        : base(summary, onEdited, onRemove)
    {
        Tree = tree;
        _selectedTreeType = GameEnums.FindOrFirst(GameEnums.TreeTypes, tree.TreeType);
        _growthStage = tree.GrowthStage;
        _health = tree.Health;
        _stump = tree.Stump;
        _tapped = tree.Tapped;
        _flipped = tree.Flipped;
        _hasSeed = tree.HasSeed;
        _fertilized = tree.Fertilized;
        _shakeLeft = tree.ShakeLeft;
        _hasMoss = tree.HasMoss;
        _wasShakenToday = tree.WasShakenToday;
    }

    partial void OnSelectedTreeTypeChanged(NamedValue value) { Tree.TreeType = value.Value; NotifyEdited(); }
    partial void OnGrowthStageChanged(int value) { Tree.GrowthStage = value; NotifyEdited(); }
    partial void OnHealthChanged(int value) { Tree.Health = value; NotifyEdited(); }
    partial void OnStumpChanged(bool value) { Tree.Stump = value; NotifyEdited(); }
    partial void OnTappedChanged(bool value) { Tree.Tapped = value; NotifyEdited(); }
    partial void OnFlippedChanged(bool value) { Tree.Flipped = value; NotifyEdited(); }
    partial void OnHasSeedChanged(bool value) { Tree.HasSeed = value; NotifyEdited(); }
    partial void OnFertilizedChanged(bool value) { Tree.Fertilized = value; NotifyEdited(); }
    partial void OnShakeLeftChanged(bool value) { Tree.ShakeLeft = value; NotifyEdited(); }
    partial void OnHasMossChanged(bool value) { Tree.HasMoss = value; NotifyEdited(); }
    partial void OnWasShakenTodayChanged(bool value) { Tree.WasShakenToday = value; NotifyEdited(); }

    public override MapEntitySummary Resummarize() => MapEntitySummary.FromTree(Tree);
}

public sealed partial class GrassDetailsViewModel : MapEntityDetailsViewModel
{
    public GrassEditor Grass { get; }
    public IReadOnlyList<NamedValue> GrassTypes => GameEnums.GrassTypes;

    [ObservableProperty] private NamedValue _selectedGrassType;
    [ObservableProperty] private int _numberOfWeeds;

    public GrassDetailsViewModel(MapEntitySummary summary, GrassEditor grass, Action<MapEntityDetailsViewModel> onEdited, Action<MapEntitySummary> onRemove)
        : base(summary, onEdited, onRemove)
    {
        Grass = grass;
        _selectedGrassType = GameEnums.FindOrFirst(GameEnums.GrassTypes, grass.GrassType);
        _numberOfWeeds = grass.NumberOfWeeds;
    }

    partial void OnSelectedGrassTypeChanged(NamedValue value) { Grass.GrassType = value.Value; NotifyEdited(); }
    partial void OnNumberOfWeedsChanged(int value) { Grass.NumberOfWeeds = value; NotifyEdited(); }

    public override MapEntitySummary Resummarize() => MapEntitySummary.FromGrass(Grass);
}

/// <summary>The optional planted-crop sub-panel of a HoeDirt tile. Not a MapEntityDetailsViewModel
/// itself - it never exists independently of the HoeDirt tile that owns it - just a nested VM
/// with its own "remove crop, keep tilled soil" action distinct from the tile's own Remove.</summary>
public sealed partial class CropDetailsViewModel : ViewModelBase
{
    private readonly CropEditor _crop;
    private readonly Action _onEdited;
    private readonly Action _onRemoveCrop;
    private readonly bool _isBound;

    [ObservableProperty] private int _currentPhase;
    [ObservableProperty] private int _dayOfCurrentPhase;
    [ObservableProperty] private bool _dead;
    [ObservableProperty] private bool _fullGrown;
    [ObservableProperty] private bool _flip;
    [ObservableProperty] private int _regrowAfterHarvest;
    [ObservableProperty] private int _minHarvest;
    [ObservableProperty] private int _maxHarvest;
    [ObservableProperty] private double _chanceForExtraCrops;

    public int SeedIndex => _crop.SeedIndex;
    public int IndexOfHarvest => _crop.IndexOfHarvest;

    public CropDetailsViewModel(CropEditor crop, Action onEdited, Action onRemoveCrop)
    {
        _crop = crop;
        _onEdited = onEdited;
        _onRemoveCrop = onRemoveCrop;
        _currentPhase = crop.CurrentPhase;
        _dayOfCurrentPhase = crop.DayOfCurrentPhase;
        _dead = crop.Dead;
        _fullGrown = crop.FullGrown;
        _flip = crop.Flip;
        _regrowAfterHarvest = crop.RegrowAfterHarvest;
        _minHarvest = crop.MinHarvest;
        _maxHarvest = crop.MaxHarvest;
        _chanceForExtraCrops = crop.ChanceForExtraCrops;
        _isBound = true;
    }

    partial void OnCurrentPhaseChanged(int value) { _crop.CurrentPhase = value; if (_isBound) _onEdited(); }
    partial void OnDayOfCurrentPhaseChanged(int value) { _crop.DayOfCurrentPhase = value; if (_isBound) _onEdited(); }
    partial void OnDeadChanged(bool value) { _crop.Dead = value; if (_isBound) _onEdited(); }
    partial void OnFullGrownChanged(bool value) { _crop.FullGrown = value; if (_isBound) _onEdited(); }
    partial void OnFlipChanged(bool value) { _crop.Flip = value; if (_isBound) _onEdited(); }
    partial void OnRegrowAfterHarvestChanged(int value) { _crop.RegrowAfterHarvest = value; if (_isBound) _onEdited(); }
    partial void OnMinHarvestChanged(int value) { _crop.MinHarvest = value; if (_isBound) _onEdited(); }
    partial void OnMaxHarvestChanged(int value) { _crop.MaxHarvest = value; if (_isBound) _onEdited(); }
    partial void OnChanceForExtraCropsChanged(double value) { _crop.ChanceForExtraCrops = value; if (_isBound) _onEdited(); }

    [RelayCommand]
    private void RemoveCrop() => _onRemoveCrop();
}

public sealed partial class HoeDirtDetailsViewModel : MapEntityDetailsViewModel
{
    public HoeDirtEditor Dirt { get; }
    public IReadOnlyList<NamedValue> States => GameEnums.HoeDirtStates;

    [ObservableProperty] private NamedValue _selectedState;
    [ObservableProperty] private bool _isGreenhouseDirt;
    [ObservableProperty] private CropDetailsViewModel? _crop;

    public HoeDirtDetailsViewModel(MapEntitySummary summary, HoeDirtEditor dirt, Action<MapEntityDetailsViewModel> onEdited, Action<MapEntitySummary> onRemove)
        : base(summary, onEdited, onRemove)
    {
        Dirt = dirt;
        _selectedState = GameEnums.FindOrFirst(GameEnums.HoeDirtStates, dirt.State);
        _isGreenhouseDirt = dirt.IsGreenhouseDirt;
        _crop = dirt.Crop is { } crop ? new CropDetailsViewModel(crop, NotifyEdited, RemoveCropAndRefresh) : null;
    }

    partial void OnSelectedStateChanged(NamedValue value) { Dirt.State = value.Value; NotifyEdited(); }
    partial void OnIsGreenhouseDirtChanged(bool value) { Dirt.IsGreenhouseDirt = value; NotifyEdited(); }

    private void RemoveCropAndRefresh()
    {
        Dirt.RemoveCrop();
        Crop = null;
        NotifyEdited();
    }

    public override MapEntitySummary Resummarize() => MapEntitySummary.FromHoeDirt(Dirt);
}

public sealed partial class ResourceClumpDetailsViewModel : MapEntityDetailsViewModel
{
    public ResourceClumpEditor Clump { get; }

    [ObservableProperty] private int _parentSheetIndex;
    [ObservableProperty] private int _health;

    public ResourceClumpDetailsViewModel(MapEntitySummary summary, ResourceClumpEditor clump, Action<MapEntityDetailsViewModel> onEdited, Action<MapEntitySummary> onRemove)
        : base(summary, onEdited, onRemove)
    {
        Clump = clump;
        _parentSheetIndex = clump.ParentSheetIndex;
        _health = clump.Health;
    }

    partial void OnParentSheetIndexChanged(int value) { Clump.ParentSheetIndex = value; NotifyEdited(); }
    partial void OnHealthChanged(int value) { Clump.Health = value; NotifyEdited(); }

    public override MapEntitySummary Resummarize() => MapEntitySummary.FromClump(Clump);
}

/// <summary>Reuses ItemRowViewModel (Inventory/Storage tabs' own row VM) for the Stack/Quality
/// fields one binding path deeper (Item.Stack) instead of re-declaring that logic here.</summary>
public sealed class PlacedObjectDetailsViewModel : MapEntityDetailsViewModel
{
    public PlacedObjectEditor Placed { get; }
    public ItemRowViewModel Item { get; }

    public PlacedObjectDetailsViewModel(MapEntitySummary summary, PlacedObjectEditor placed, Action<MapEntityDetailsViewModel> onEdited, Action<MapEntitySummary> onRemove)
        : base(summary, onEdited, onRemove)
    {
        Placed = placed;
        // The row's own onRemove is unused here - removal goes through this panel's inherited
        // Remove command instead, which removes the whole placed object, not just the item row.
        Item = new ItemRowViewModel(placed.Item, _ => { }, _ => NotifyEdited());
    }

    public override MapEntitySummary Resummarize() => MapEntitySummary.FromObject(Placed);
}

/// <summary>BuildingEditor's other fields are unverified (see its own remarks) - Skin is the
/// one confirmed, real, editable field (see BuildingEditor.SkinId), shown only for building
/// types that actually have alternate skins (Data/Buildings.json's Skins list - currently just
/// Pet Bowl among the types this tool can place/edit).</summary>
public sealed partial class BuildingDetailsViewModel : MapEntityDetailsViewModel
{
    private const string DefaultSkinLabel = "(Default)";

    public BuildingEditor Building { get; }

    /// <summary>"(Default)" plus every real skin id for this building type - empty (just the
    /// default entry) for the vast majority of buildings, which have no alternates at all.</summary>
    public IReadOnlyList<string> AvailableSkins { get; }
    public bool HasSkins => AvailableSkins.Count > 1;

    [ObservableProperty] private string _selectedSkin = DefaultSkinLabel;

    public BuildingDetailsViewModel(MapEntitySummary summary, BuildingEditor building, Action<MapEntityDetailsViewModel> onEdited, Action<MapEntitySummary> onRemove)
        : base(summary, onEdited, onRemove)
    {
        Building = building;
        AvailableSkins = new[] { DefaultSkinLabel }.Concat(MapAssets.BuildingSprites.SkinsFor(building.BuildingType)).ToList();
        _selectedSkin = building.SkinId ?? DefaultSkinLabel;
    }

    partial void OnSelectedSkinChanged(string value)
    {
        Building.SkinId = value == DefaultSkinLabel ? null : value;
        NotifyEdited();
    }

    public override MapEntitySummary Resummarize() => MapEntitySummary.FromBuilding(Building);
}

public sealed partial class BushDetailsViewModel : MapEntityDetailsViewModel
{
    public BushEditor Bush { get; }
    public IReadOnlyList<NamedValue> BushSizes => GameEnums.BushSizes;

    [ObservableProperty] private NamedValue _selectedSize;
    [ObservableProperty] private int _health;
    [ObservableProperty] private bool _flipped;
    [ObservableProperty] private bool _townBush;
    [ObservableProperty] private bool _greenhouseBush;
    [ObservableProperty] private bool _drawShadow;

    /// <summary>Whether this bush is currently bloom/harvest-ready - a raw sprite-sheet column
    /// selector on the real field (TileSheetOffset), not a separate bool, but exposed as a
    /// checkbox here since 0/1 is all it ever meaningfully takes for size 0-2 bushes (see
    /// BushSprites - walnut bushes also use it as a 0/1 "found today" flag).</summary>
    [ObservableProperty] private bool _bloomReady;

    public BushDetailsViewModel(MapEntitySummary summary, BushEditor bush, Action<MapEntityDetailsViewModel> onEdited, Action<MapEntitySummary> onRemove)
        : base(summary, onEdited, onRemove)
    {
        Bush = bush;
        _selectedSize = GameEnums.FindOrFirst(GameEnums.BushSizes, bush.Size);
        _health = bush.Health;
        _flipped = bush.Flipped;
        _townBush = bush.TownBush;
        _greenhouseBush = bush.GreenhouseBush;
        _drawShadow = bush.DrawShadow;
        _bloomReady = bush.TileSheetOffset != 0;
    }

    partial void OnSelectedSizeChanged(NamedValue value) { Bush.Size = value.Value; NotifyEdited(); }
    partial void OnHealthChanged(int value) { Bush.Health = value; NotifyEdited(); }
    partial void OnFlippedChanged(bool value) { Bush.Flipped = value; NotifyEdited(); }
    partial void OnTownBushChanged(bool value) { Bush.TownBush = value; NotifyEdited(); }
    partial void OnGreenhouseBushChanged(bool value) { Bush.GreenhouseBush = value; NotifyEdited(); }
    partial void OnDrawShadowChanged(bool value) { Bush.DrawShadow = value; NotifyEdited(); }
    partial void OnBloomReadyChanged(bool value) { Bush.TileSheetOffset = value ? 1 : 0; NotifyEdited(); }

    public override MapEntitySummary Resummarize() => MapEntitySummary.FromBush(Bush);
}
