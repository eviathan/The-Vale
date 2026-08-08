using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.MapAssets;
using StardewTools.SaveEditor.ViewModels;

namespace StardewTools.SaveEditor.Controls;

/// <summary>
/// Draws the farm. When <see cref="ContentFolder"/> points at a real StardewXnbHack
/// extraction, this renders the game's actual tile art (terrain, paths, buildings), with
/// placed entities drawn as real sprites where we have a verified sprite sheet mapping
/// (trees, objects) and an outlined marker otherwise (resource clumps, unmapped tree types).
/// Entities are interleaved row-by-row with the terrain layers so a tall sprite occludes
/// (and is occluded by) neighboring rows correctly, approximating the game's Y-sorted draw
/// order without a full scene graph. Without a content folder, it falls back to an abstract
/// flat-color dot grid scaled to the entities' own bounding box.
/// </summary>
public sealed class FarmMapControl : Control
{
    public static readonly StyledProperty<IEnumerable<MapEntitySummary>?> EntitiesProperty =
        AvaloniaProperty.Register<FarmMapControl, IEnumerable<MapEntitySummary>?>(nameof(Entities));

    public static readonly StyledProperty<MapEntitySummary?> SelectedProperty =
        AvaloniaProperty.Register<FarmMapControl, MapEntitySummary?>(nameof(Selected), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> SeasonProperty =
        AvaloniaProperty.Register<FarmMapControl, string>(nameof(Season), "spring");

    public static readonly StyledProperty<string?> ContentFolderProperty =
        AvaloniaProperty.Register<FarmMapControl, string?>(nameof(ContentFolder));

    public static readonly StyledProperty<string> StatusProperty =
        AvaloniaProperty.Register<FarmMapControl, string>(nameof(Status), "Abstract view (no tile art loaded).");

    public static readonly StyledProperty<string?> SelectedTileInfoProperty =
        AvaloniaProperty.Register<FarmMapControl, string?>(nameof(SelectedTileInfo));

    public static readonly StyledProperty<string> LocationNameProperty =
        AvaloniaProperty.Register<FarmMapControl, string>(nameof(LocationName), "Farm");

    public static readonly StyledProperty<int> HouseUpgradeLevelProperty =
        AvaloniaProperty.Register<FarmMapControl, int>(nameof(HouseUpgradeLevel));

    public static readonly StyledProperty<TilePosition?> ClickedTileProperty =
        AvaloniaProperty.Register<FarmMapControl, TilePosition?>(nameof(ClickedTile), defaultBindingMode: Avalonia.Data.BindingMode.OneWayToSource);

    public static readonly StyledProperty<IReadOnlyList<MapEntitySummary>> SelectedRangeProperty =
        AvaloniaProperty.Register<FarmMapControl, IReadOnlyList<MapEntitySummary>>(nameof(SelectedRange), Array.Empty<MapEntitySummary>(), defaultBindingMode: Avalonia.Data.BindingMode.OneWayToSource);

    /// <summary>Fired once when a click-and-drag that started on an existing entity finishes -
    /// see OnPointerPressed/OnPointerReleased. A drag starting on empty space marquees instead
    /// (SelectedRange) and never touches this.</summary>
    public static readonly StyledProperty<EntityMoveRequest?> MoveRequestProperty =
        AvaloniaProperty.Register<FarmMapControl, EntityMoveRequest?>(nameof(MoveRequest), defaultBindingMode: Avalonia.Data.BindingMode.OneWayToSource);

    /// <summary>Ctrl+C/Ctrl+V/Ctrl+D (see OnKeyDown) - each fires by incrementing a counter
    /// (rather than toggling a bool or re-sending the same value) so the ViewModel's partial
    /// On*Changed always sees a genuinely new value and fires every press, even for two presses
    /// in a row - a plain bool/reused-reference property would silently no-op on a repeat
    /// (CommunityToolkit [ObservableProperty]'s default equality-check skip, the same gotcha this
    /// session already hit once with ClickedTile in the render-harness tests).</summary>
    public static readonly StyledProperty<int> CopyRequestProperty =
        AvaloniaProperty.Register<FarmMapControl, int>(nameof(CopyRequest), defaultBindingMode: Avalonia.Data.BindingMode.OneWayToSource);

    public static readonly StyledProperty<int> PasteRequestProperty =
        AvaloniaProperty.Register<FarmMapControl, int>(nameof(PasteRequest), defaultBindingMode: Avalonia.Data.BindingMode.OneWayToSource);

    public static readonly StyledProperty<int> DuplicateRequestProperty =
        AvaloniaProperty.Register<FarmMapControl, int>(nameof(DuplicateRequest), defaultBindingMode: Avalonia.Data.BindingMode.OneWayToSource);

    /// <summary>Delete key (see OnKeyDown) - same incrementing-counter reasoning as CopyRequest/
    /// PasteRequest/DuplicateRequest above.</summary>
    public static readonly StyledProperty<int> DeleteRequestProperty =
        AvaloniaProperty.Register<FarmMapControl, int>(nameof(DeleteRequest), defaultBindingMode: Avalonia.Data.BindingMode.OneWayToSource);

    /// <summary>Whether a draw tool (Object/Building) is armed in the side panel. When true, a
    /// click-and-drag paints one placement per tile crossed instead of the normal marquee
    /// range-select - see OnPointerMoved.</summary>
    public static readonly StyledProperty<bool> IsPlacementToolActiveProperty =
        AvaloniaProperty.Register<FarmMapControl, bool>(nameof(IsPlacementToolActive));

    /// <summary>Screen pixels per native texture pixel - 4 matches the game's own pixel-art
    /// scale (16px/tile source art x4 = 64px/tile on screen), i.e. "native resolution".</summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<FarmMapControl, double>(nameof(Zoom), 4.0);

    /// <summary>The tile coordinate at the viewport's top-left corner - panning moves this,
    /// not any pixel offset, so it stays meaningful across zoom changes.</summary>
    public static readonly StyledProperty<double> PanOffsetTileXProperty =
        AvaloniaProperty.Register<FarmMapControl, double>(nameof(PanOffsetTileX));

    public static readonly StyledProperty<double> PanOffsetTileYProperty =
        AvaloniaProperty.Register<FarmMapControl, double>(nameof(PanOffsetTileY));

    /// <summary>How many tiles square Object/Till/Plant Crop/Plant Tree stamp per click -
    /// TwoWay so the [ / ] keyboard shortcut here (see OnKeyDown) pushes changes back to the
    /// ViewModel, which is what actually stamps with it.</summary>
    public static readonly StyledProperty<int> BrushSizeProperty =
        AvaloniaProperty.Register<FarmMapControl, int>(nameof(BrushSize), 1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Freehand/Line/Rectangle - see OnPointerPressed/Moved/Released and DrawShapePreview.
    /// TwoWay so a future keyboard shortcut could cycle it the same way BrushSize's [ / ] does,
    /// though today it's only ever set from the toolbar toggle.</summary>
    public static readonly StyledProperty<DrawShape> DrawShapeProperty =
        AvaloniaProperty.Register<FarmMapControl, DrawShape>(nameof(DrawShape), DrawShape.Freehand, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Fired once, on pointer release, with the whole computed Line/Rectangle shape's
    /// tiles - see MapTabViewModel.OnShapeStrokeTilesChanged. Freehand never sets this; it keeps
    /// using ClickedTile (fired per tile crossed) exactly as before Line/Rectangle existed.</summary>
    public static readonly StyledProperty<IReadOnlyList<TilePosition>?> ShapeStrokeTilesProperty =
        AvaloniaProperty.Register<FarmMapControl, IReadOnlyList<TilePosition>?>(nameof(ShapeStrokeTiles), defaultBindingMode: Avalonia.Data.BindingMode.OneWayToSource);

    /// <summary>The exact entities a pending placement/move is blocked by (MapTabViewModel.
    /// BlockedEntities, straight from PendingPlacement.Blocking) - highlighted on the map so the
    /// blocked-placement message isn't the only signal of what's actually in the way.</summary>
    public static readonly StyledProperty<IReadOnlyList<MapEntitySummary>> BlockedEntitiesProperty =
        AvaloniaProperty.Register<FarmMapControl, IReadOnlyList<MapEntitySummary>>(nameof(BlockedEntities), Array.Empty<MapEntitySummary>());

    /// <summary>The raw tile(s) a placement/move was rejected at with no entity to blame (water/
    /// unbuildable ground - MapTabViewModel.PlacementBlockedTiles).</summary>
    public static readonly StyledProperty<IReadOnlyList<TilePosition>> BlockedTilesProperty =
        AvaloniaProperty.Register<FarmMapControl, IReadOnlyList<TilePosition>>(nameof(BlockedTiles), Array.Empty<TilePosition>());

    /// <summary>Size of the hover-preview outline drawn under the cursor while a draw tool is
    /// armed (see DrawHoverPreview) - the ViewModel sets this to the Building tool's real
    /// footprint or BrushSize for everything else (see MapTabViewModel.HoverFootprintWidth).
    /// Purely a rendering hint; not what OnClickedTileChanged/ApplyBrushTool actually stamp with.</summary>
    public static readonly StyledProperty<int> HoverFootprintWidthProperty =
        AvaloniaProperty.Register<FarmMapControl, int>(nameof(HoverFootprintWidth), 1);

    public static readonly StyledProperty<int> HoverFootprintHeightProperty =
        AvaloniaProperty.Register<FarmMapControl, int>(nameof(HoverFootprintHeight), 1);

    /// <summary>Player.Stats.DaysPlayed - only used for a tea bush's age-based growth-stage
    /// sprite (Bush.getAge()). Zero elsewhere has no visible effect.</summary>
    public static readonly StyledProperty<int> CurrentDaysPlayedProperty =
        AvaloniaProperty.Register<FarmMapControl, int>(nameof(CurrentDaysPlayed));

    public IEnumerable<MapEntitySummary>? Entities
    {
        get => GetValue(EntitiesProperty);
        set => SetValue(EntitiesProperty, value);
    }

    public MapEntitySummary? Selected
    {
        get => GetValue(SelectedProperty);
        set => SetValue(SelectedProperty, value);
    }

    public string Season
    {
        get => GetValue(SeasonProperty);
        set => SetValue(SeasonProperty, value);
    }

    /// <summary>Path to a folder unpacked by StardewXnbHack (containing a "Maps" subfolder). Null/empty = abstract view.</summary>
    public string? ContentFolder
    {
        get => GetValue(ContentFolderProperty);
        set => SetValue(ContentFolderProperty, value);
    }

    public string Status
    {
        get => GetValue(StatusProperty);
        private set => SetValue(StatusProperty, value);
    }

    /// <summary>
    /// Human-readable dump of every layer's tile (and its TMX properties, e.g. Diggable/
    /// Buildable/Type) at the last-clicked position that didn't hit an entity. Read-only -
    /// these are base map properties (fixed game design), not save-tracked data, so there's
    /// nothing here to write back; what IS save-tracked at a tile (a planted crop, tilled
    /// soil) goes through the entity system instead once that schema is verified.
    /// </summary>
    public string? SelectedTileInfo
    {
        get => GetValue(SelectedTileInfoProperty);
        private set => SetValue(SelectedTileInfoProperty, value);
    }

    /// <summary>
    /// Which location's map to render, by its save xsi:type name (e.g. "Farm", "Town",
    /// "Beach"). Only "Farm" has placed-entity data bound to it today - other locations
    /// render real tile art with no entity overlay.
    /// </summary>
    public string LocationName
    {
        get => GetValue(LocationNameProperty);
        set => SetValue(LocationNameProperty, value);
    }

    /// <summary>0-3 - drives which exterior the farmhouse overlay uses (see FarmhouseSprite).
    /// Not part of Entities: the farmhouse isn't a placed Building at all.</summary>
    public int HouseUpgradeLevel
    {
        get => GetValue(HouseUpgradeLevelProperty);
        set => SetValue(HouseUpgradeLevelProperty, value);
    }

    /// <summary>The tile under the last click, regardless of whether it hit an entity - lets
    /// the Map tab offer "place item here" targeting wherever the user last pointed, not just
    /// empty tiles.</summary>
    public TilePosition? ClickedTile
    {
        get => GetValue(ClickedTileProperty);
        private set => SetValue(ClickedTileProperty, value);
    }

    /// <summary>Every entity whose footprint intersects the last click-and-drag rectangle -
    /// empty when nothing's drag-selected (including after a plain, non-drag click, which
    /// goes through Selected instead). Set once on release, not live during the drag itself.</summary>
    public IReadOnlyList<MapEntitySummary> SelectedRange
    {
        get => GetValue(SelectedRangeProperty);
        private set => SetValue(SelectedRangeProperty, value);
    }

    public bool IsPlacementToolActive
    {
        get => GetValue(IsPlacementToolActiveProperty);
        set => SetValue(IsPlacementToolActiveProperty, value);
    }

    public EntityMoveRequest? MoveRequest
    {
        get => GetValue(MoveRequestProperty);
        private set => SetValue(MoveRequestProperty, value);
    }

    public int CopyRequest
    {
        get => GetValue(CopyRequestProperty);
        private set => SetValue(CopyRequestProperty, value);
    }

    public int PasteRequest
    {
        get => GetValue(PasteRequestProperty);
        private set => SetValue(PasteRequestProperty, value);
    }

    public int DuplicateRequest
    {
        get => GetValue(DuplicateRequestProperty);
        private set => SetValue(DuplicateRequestProperty, value);
    }

    public int DeleteRequest
    {
        get => GetValue(DeleteRequestProperty);
        private set => SetValue(DeleteRequestProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double PanOffsetTileX
    {
        get => GetValue(PanOffsetTileXProperty);
        set => SetValue(PanOffsetTileXProperty, value);
    }

    public double PanOffsetTileY
    {
        get => GetValue(PanOffsetTileYProperty);
        set => SetValue(PanOffsetTileYProperty, value);
    }

    public int BrushSize
    {
        get => GetValue(BrushSizeProperty);
        set => SetValue(BrushSizeProperty, value);
    }

    public DrawShape DrawShape
    {
        get => GetValue(DrawShapeProperty);
        set => SetValue(DrawShapeProperty, value);
    }

    public IReadOnlyList<TilePosition>? ShapeStrokeTiles
    {
        get => GetValue(ShapeStrokeTilesProperty);
        set => SetValue(ShapeStrokeTilesProperty, value);
    }

    public IReadOnlyList<MapEntitySummary> BlockedEntities
    {
        get => GetValue(BlockedEntitiesProperty);
        set => SetValue(BlockedEntitiesProperty, value);
    }

    public IReadOnlyList<TilePosition> BlockedTiles
    {
        get => GetValue(BlockedTilesProperty);
        set => SetValue(BlockedTilesProperty, value);
    }

    public int HoverFootprintWidth
    {
        get => GetValue(HoverFootprintWidthProperty);
        set => SetValue(HoverFootprintWidthProperty, value);
    }

    public int HoverFootprintHeight
    {
        get => GetValue(HoverFootprintHeightProperty);
        set => SetValue(HoverFootprintHeightProperty, value);
    }

    public int CurrentDaysPlayed
    {
        get => GetValue(CurrentDaysPlayedProperty);
        set => SetValue(CurrentDaysPlayedProperty, value);
    }

    static FarmMapControl()
    {
        AffectsRender<FarmMapControl>(EntitiesProperty, SelectedProperty, SeasonProperty, ContentFolderProperty, LocationNameProperty, HouseUpgradeLevelProperty,
            ZoomProperty, PanOffsetTileXProperty, PanOffsetTileYProperty, IsPlacementToolActiveProperty, HoverFootprintWidthProperty, HoverFootprintHeightProperty,
            CurrentDaysPlayedProperty, BlockedEntitiesProperty, BlockedTilesProperty);
        FocusableProperty.OverrideDefaultValue<FarmMapControl>(true); // needed to receive the KeyDown/KeyUp events spacebar-pan depends on
        ClipToBoundsProperty.OverrideDefaultValue<FarmMapControl>(true); // at native zoom the map is almost always bigger than the viewport - without this it draws straight over the side panel instead of being cropped to its own column
    }

    public FarmMapControl()
    {
        // macOS trackpad pinch arrives as this gesture (Delta.X == Delta.Y == the raw magnification
        // delta from NSEvent, NOT a touch-pointer Pinch gesture - PinchGestureRecognizer only reacts
        // to PointerType.Touch/Pen, which a laptop trackpad never reports as), confirmed against the
        // Avalonia.Native source (AvnView.mm magnifyWithEvent: -> MouseDevice.GestureMagnify).
        this.AddHandler(Gestures.PointerTouchPadGestureMagnifyEvent, OnTouchPadMagnify);
    }

    private static readonly IReadOnlyDictionary<string, Color> SeasonBackgrounds = new Dictionary<string, Color>
    {
        ["spring"] = Color.Parse("#C9E4B0"),
        ["summer"] = Color.Parse("#A8D98B"),
        ["fall"] = Color.Parse("#E0C185"),
        ["winter"] = Color.Parse("#DDE6EC"),
    };

    private MapAssetLoader? _loader;
    private TmxMap? _map;
    private string? _loadedFolder;
    private string? _loadedLocation;
    private (double MinX, double MinY, double Scale)? _lastLayout;

    /// <summary>Each entity's actual on-screen bounds from the last real-map render - populated
    /// fresh every RenderRealMap pass (cleared up front, refilled as each entity is drawn).
    /// Used for pixel-accurate hit-testing instead of a plain tile-footprint check, since many
    /// sprites (a tree canopy, a tall building) are drawn far outside their logical footprint -
    /// hit-testing only the footprint meant most of what's actually visible wasn't clickable.</summary>
    private readonly Dictionary<MapEntitySummary, Rect> _entityScreenBounds = new();

    /// <summary>Same entities as _entityScreenBounds, in the order they were actually drawn -
    /// FindEntityAt walks this backwards so an overlap between two sprites resolves to whichever
    /// was drawn LAST (visually on top, matching the row-interleaved Y-sort draw order), not an
    /// arbitrary or size-based pick.</summary>
    private readonly List<MapEntitySummary> _entityDrawOrder = new();

    /// <summary>Position -> WhichFloor for every placed Flooring tile, rebuilt fresh at the top
    /// of every RenderRealMap pass - lets TryDrawFlooringSprite compute each tile's live 8-
    /// direction neighbor bitmask in O(1) per direction instead of scanning all entities per
    /// tile. Matches the real game's own "only same-WhichFloor Flooring neighbors connect" rule
    /// (Flooring.gatherNeighbors()) - recomputed from scratch every render rather than
    /// incrementally maintained (as the real game does via OnNeighborAdded/Removed) since this is
    /// a stateless snapshot renderer, not a live simulation.</summary>
    private readonly Dictionary<(int X, int Y), string> _flooringLookup = new();

    private TilePosition? _dragStartTile;
    private TilePosition? _dragCurrentTile;
    private bool _isDragging;
    private MapEntitySummary? _dragEntity; // non-null => this drag (once it becomes one) moves an entity instead of marqueeing
    private IReadOnlyList<MapEntitySummary>? _dragGroup; // non-null => the press landed on a SelectedRange member, so the whole group moves together
    private bool _isPainting;
    private TilePosition? _lastPaintedTile;

    /// <summary>Where a Line/Rectangle paint-drag started - Freehand doesn't use this (it only
    /// ever needs _lastPaintedTile, the most recent tile crossed). Set at press, read at release
    /// to compute the whole shape's tiles (see ComputeLineTiles/ComputeRectangleTiles).</summary>
    private TilePosition? _shapeStartTile;
    private bool _needsViewCentering;
    private bool _spaceHeld;
    private Point? _panStartPoint;
    private (double X, double Y)? _panStartOffset;
    private Point? _lastPointerPosition;
    private TilePosition? _hoverTile;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == EntitiesProperty)
        {
            // Entities is the same ObservableCollection instance for the whole session - Add/
            // Remove/Replace mutate it in place rather than rebinding a new collection, so the
            // AffectsRender registration above (which only fires on a StyledProperty value
            // *change*, i.e. a different collection reference) never sees most edits. Listening
            // to CollectionChanged directly is what makes remove/collect/confirm-placement
            // repaint immediately instead of only on the next click.
            if (change.OldValue is INotifyCollectionChanged oldIncc)
                oldIncc.CollectionChanged -= OnEntitiesCollectionChanged;
            if (change.NewValue is INotifyCollectionChanged newIncc)
                newIncc.CollectionChanged += OnEntitiesCollectionChanged;
        }

        if ((change.Property == ContentFolderProperty || change.Property == LocationNameProperty)
            && (ContentFolder != _loadedFolder || LocationName != _loadedLocation))
        {
            TryLoadMap();
        }

        if (change.Property == IsPlacementToolActiveProperty)
            UpdateCursor();
    }

    private void OnEntitiesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    /// <summary>Cursor communicates interaction mode at a glance: Hand while panning (spacebar
    /// held or actively panning) or hovering a selectable/draggable entity, Cross while a draw
    /// tool is armed or hovering empty ground (marquee-select-ready) - so there's always a clear
    /// "what happens if I click/drag here" signal instead of a generic arrow throughout.</summary>
    private void UpdateCursor()
    {
        if (_spaceHeld || _panStartPoint is not null)
        {
            Cursor = new Cursor(StandardCursorType.Hand);
            return;
        }

        if (IsPlacementToolActive)
        {
            Cursor = new Cursor(StandardCursorType.Cross);
            return;
        }

        if (_lastPointerPosition is { } position && _lastLayout is { } layout && FindEntityAt(position, layout) is not null)
        {
            Cursor = new Cursor(StandardCursorType.Hand);
            return;
        }

        Cursor = new Cursor(StandardCursorType.Cross);
    }

    private void TryLoadMap()
    {
        _loadedFolder = ContentFolder;
        _loadedLocation = LocationName;
        _map = null;
        _loader = null;
        _needsViewCentering = true; // a genuinely different map just loaded - re-center pan/zoom on it once, then leave the user's view alone

        if (string.IsNullOrWhiteSpace(ContentFolder))
        {
            Status = "Abstract view (no tile art loaded).";
            return;
        }

        try
        {
            _loader = new MapAssetLoader(ContentFolder);
            _map = _loader.LoadMap(LocationName);
            Status = $"Real tile art loaded for {LocationName} from {ContentFolder}.";
        }
        catch (Exception ex)
        {
            _loader = null;
            _map = null;
            Status = $"Couldn't load tile art ({ex.Message}) - showing abstract view instead.";
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_map is not null && _loader is not null)
            RenderRealMap(context, _map, _loader);
        else
            RenderAbstract(context);

        DrawMarquee(context);
        DrawMoveGhost(context);
        DrawRangeHighlights(context);
        DrawHoverPreview(context);
        DrawShapePreview(context);
        DrawBlockedHighlights(context);
    }

    /// <summary>Highlights exactly what a blocked placement/move is blocked BY, on the map
    /// itself - BlockedEntities (a PendingPlacement's own real Blocking list - the same entities
    /// Confirm would remove) get a red outline around their actual drawn bounds; BlockedTiles
    /// (no entity to blame - water/unbuildable ground) get a plain red tile outline instead.
    /// Both clear back to empty the moment a placement/move actually succeeds or the tool/tile
    /// changes (see MapTabViewModel's PlacementBlockedTiles/BlockedEntities remarks) - this never
    /// lingers stale.</summary>
    private void DrawBlockedHighlights(DrawingContext context)
    {
        if (_lastLayout is not { } layout)
            return;

        if (BlockedEntities.Count > 0)
        {
            var pen = new Pen(Brushes.Red, 2);
            foreach (var entity in BlockedEntities)
            {
                if (_entityScreenBounds.TryGetValue(entity, out var bounds))
                    context.DrawRectangle(pen, new Rect(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4));
            }
        }

        if (BlockedTiles.Count > 0)
        {
            var pen = new Pen(Brushes.Red, 2);
            var fill = new SolidColorBrush(Colors.Red, 0.2);
            foreach (var t in BlockedTiles)
            {
                var rect = new Rect((t.X - layout.MinX) * layout.Scale, (t.Y - layout.MinY) * layout.Scale, layout.Scale, layout.Scale);
                context.FillRectangle(fill, rect);
                context.DrawRectangle(pen, rect);
            }
        }
    }

    /// <summary>The "effected area" outline under the cursor while a draw tool is armed -
    /// HoverFootprintWidth/Height (set by the ViewModel: BrushSize for Object/Till/Plant Crop/
    /// Plant Tree, the real footprint for Building) centered on the hovered tile the same way
    /// ApplyBrushTool centers its brush, so what's outlined here is exactly what a click would
    /// affect. Not shown mid-drag (paint/move/marquee already have their own visual feedback).</summary>
    private void DrawHoverPreview(DrawingContext context)
    {
        if (!IsPlacementToolActive || _isPainting || _isDragging || _panStartPoint is not null
            || _hoverTile is not { } hover || _lastLayout is not { } layout)
        {
            return;
        }

        var width = Math.Max(1, HoverFootprintWidth);
        var height = Math.Max(1, HoverFootprintHeight);
        var startX = hover.X - (width - 1) / 2;
        var startY = hover.Y - (height - 1) / 2;

        var rect = new Rect(
            (startX - layout.MinX) * layout.Scale,
            (startY - layout.MinY) * layout.Scale,
            width * layout.Scale,
            height * layout.Scale);

        context.FillRectangle(new SolidColorBrush(Color.Parse("#4034D058")), rect);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#34D058")), 2), rect);
    }

    /// <summary>Live preview of the whole Line/Rectangle shape while dragging - same green as
    /// DrawHoverPreview (this replaces it for the duration of the drag, see DrawHoverPreview's
    /// own _isPainting guard), just covering every tile the final stroke would actually commit
    /// instead of a single BrushSize footprint.</summary>
    private void DrawShapePreview(DrawingContext context)
    {
        if (!_isPainting || DrawShape == DrawShape.Freehand
            || _shapeStartTile is not { } start || _lastPaintedTile is not { } current
            || _lastLayout is not { } layout)
        {
            return;
        }

        var tiles = DrawShape == DrawShape.Line ? ComputeLineTiles(start, current) : ComputeRectangleTiles(start, current);
        var fill = new SolidColorBrush(Color.Parse("#4034D058"));
        var pen = new Pen(new SolidColorBrush(Color.Parse("#34D058")), 2);
        foreach (var t in tiles)
        {
            var rect = new Rect((t.X - layout.MinX) * layout.Scale, (t.Y - layout.MinY) * layout.Scale, layout.Scale, layout.Scale);
            context.FillRectangle(fill, rect);
            context.DrawRectangle(pen, rect);
        }
    }

    /// <summary>Every tile on the grid line between two tiles (inclusive of both ends) - standard
    /// Bresenham, the same algorithm most paint/map editors (Tiled included) use for a
    /// shift-drag/line tool. Single-tile-wide regardless of how steep the line is.</summary>
    private static List<TilePosition> ComputeLineTiles(TilePosition start, TilePosition end)
    {
        var tiles = new List<TilePosition>();
        var x0 = start.X; var y0 = start.Y;
        var x1 = end.X; var y1 = end.Y;
        var dx = Math.Abs(x1 - x0);
        var dy = -Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            tiles.Add(new TilePosition(x0, y0));
            if (x0 == x1 && y0 == y1)
                break;

            var e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }

        return tiles;
    }

    /// <summary>Every tile in the filled rectangle with the two given tiles as opposite corners
    /// (inclusive) - order-independent, so dragging in any of the 4 diagonal directions works.</summary>
    private static List<TilePosition> ComputeRectangleTiles(TilePosition start, TilePosition end)
    {
        var tiles = new List<TilePosition>();
        var minX = Math.Min(start.X, end.X);
        var maxX = Math.Max(start.X, end.X);
        var minY = Math.Min(start.Y, end.Y);
        var maxY = Math.Max(start.Y, end.Y);
        for (var x = minX; x <= maxX; x++)
            for (var y = minY; y <= maxY; y++)
                tiles.Add(new TilePosition(x, y));

        return tiles;
    }

    /// <summary>Outlines every entity in SelectedRange so a marquee (or bulk) selection stays
    /// visible on the map itself, not just as a count in the side panel - amber, distinct from
    /// the single-selection red box and the marquee-drag's blue rectangle. Uses the pixel
    /// bounds captured for hit-testing (see _entityScreenBounds) so the outline hugs each
    /// entity's actual sprite, same as what you'd click to select/drag it.</summary>
    private void DrawRangeHighlights(DrawingContext context)
    {
        if (SelectedRange.Count == 0)
            return;

        var pen = new Pen(new SolidColorBrush(Color.Parse("#FFC107")), 2);
        foreach (var entity in SelectedRange)
        {
            if (_entityScreenBounds.TryGetValue(entity, out var bounds))
                context.DrawRectangle(pen, new Rect(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4));
        }
    }

    /// <summary>The live rectangle overlay while a drag-select is in progress - drawn after
    /// either render path so it works the same in real-map and abstract-view modes, since
    /// both populate _lastLayout the same way. Never shown for an entity-move drag (see
    /// DrawMoveGhost) - the two are mutually exclusive outcomes of the same drag gesture.</summary>
    private void DrawMarquee(DrawingContext context)
    {
        if (_dragEntity is not null || !_isDragging || _lastLayout is not { } layout || _dragStartTile is not { } start || _dragCurrentTile is not { } current)
            return;

        var minX = Math.Min(start.X, current.X);
        var maxX = Math.Max(start.X, current.X) + 1;
        var minY = Math.Min(start.Y, current.Y);
        var maxY = Math.Max(start.Y, current.Y) + 1;

        var rect = new Rect(
            (minX - layout.MinX) * layout.Scale,
            (minY - layout.MinY) * layout.Scale,
            (maxX - minX) * layout.Scale,
            (maxY - minY) * layout.Scale);

        context.FillRectangle(new SolidColorBrush(Color.Parse("#503B82F6")), rect);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#3B82F6")), 2), rect);
    }

    /// <summary>Translucent preview of the dragged entity (or, for a group drag, every member)
    /// at its would-land tile, following the cursor - the original(s) stay drawn normally at
    /// their current (not-yet-moved) position via the regular entity loop, so a drag shows both
    /// "where it is" and "where it'll go" like a normal drag-and-drop. Every ghost moves by the
    /// same tile delta (current - start), which is what keeps a dragged group rigid - applying
    /// one uniform delta to each member's own original position is equivalent to (and simpler
    /// than) tracking a per-entity grab offset.</summary>
    private void DrawMoveGhost(DrawingContext context)
    {
        if (_dragEntity is null || !_isDragging || _lastLayout is not { } layout
            || _dragStartTile is not { } start || _dragCurrentTile is not { } current)
            return;

        var deltaX = current.X - start.X;
        var deltaY = current.Y - start.Y;
        var pixelOffsetX = -layout.MinX * layout.Scale;
        var pixelOffsetY = -layout.MinY * layout.Scale;

        foreach (var entity in _dragGroup ?? (IReadOnlyList<MapEntitySummary>)[_dragEntity])
        {
            var target = ClampFootprint(new TilePosition(entity.Position.X + deltaX, entity.Position.Y + deltaY), entity.Width, entity.Height);

            var ghost = new MapEntitySummary
            {
                Position = target,
                Kind = entity.Kind,
                Label = entity.Label,
                ColorHex = entity.ColorHex,
                Source = entity.Source,
                Width = entity.Width,
                Height = entity.Height,
            };

            DrawSingleEntity(context, ghost, pixelOffsetX, pixelOffsetY, layout.Scale, opacity: 0.55, recordBounds: false);
        }
    }

    private TilePosition ClampFootprint(TilePosition position, int width, int height)
    {
        if (_map is null)
            return position;

        var x = Math.Clamp(position.X, 0, Math.Max(0, _map.Width - width));
        var y = Math.Clamp(position.Y, 0, Math.Max(0, _map.Height - height));
        return new TilePosition(x, y);
    }

    private void RenderRealMap(DrawingContext context, TmxMap map, MapAssetLoader loader)
    {
        // Repopulated fresh below as each entity is actually drawn - see _entityScreenBounds.
        _entityScreenBounds.Clear();
        _entityDrawOrder.Clear();

        var tileScale = 16.0 * Zoom; // 16 = native texture px/tile; Zoom=4 (the default) matches the game's own 4x pixel-art scale, i.e. "native resolution"

        // A freshly-loaded map gets centered once (same math the old fit-to-bounds path used,
        // just written into the pan properties instead of recomputed every frame) - after that,
        // the user's own pan/zoom is left alone until the next map/location change.
        double panOffsetTileX, panOffsetTileY;
        if (_needsViewCentering)
        {
            _needsViewCentering = false;
            var mapPixelWidth = map.Width * map.TileWidth;
            var mapPixelHeight = map.Height * map.TileHeight;
            panOffsetTileX = (mapPixelWidth - Bounds.Width / Zoom) / 2 / map.TileWidth;
            panOffsetTileY = (mapPixelHeight - Bounds.Height / Zoom) / 2 / map.TileHeight;

            // PanOffsetTileX/Y are AffectsRender StyledProperties - setting them here, mid-Render,
            // throws "Visual was invalidated during the render pass". Use the computed values for
            // this frame directly and defer persisting them until after the render pass completes.
            var centeredX = panOffsetTileX;
            var centeredY = panOffsetTileY;
            Dispatcher.UIThread.Post(() =>
            {
                PanOffsetTileX = centeredX;
                PanOffsetTileY = centeredY;
            });
        }
        else
        {
            panOffsetTileX = PanOffsetTileX;
            panOffsetTileY = PanOffsetTileY;
        }

        var offsetX = -panOffsetTileX * tileScale;
        var offsetY = -panOffsetTileY * tileScale;
        _lastLayout = (panOffsetTileX, panOffsetTileY, tileScale); // Scale is per-tile here, not per-pixel - see hit-testing below.

        context.FillRectangle(new SolidColorBrush(Color.Parse("#1a1a1a")), new Rect(Bounds.Size));

        void DrawLayerRow(TmxLayer layer, int y)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var gid = layer.Tiles[y * map.Width + x];
                var tileset = map.TilesetFor(gid);
                if (tileset is null)
                    continue;

                var bitmap = loader.GetTilesetBitmap(tileset.ImageSource, Season);
                var (col, row) = tileset.TilePosition(gid);
                var source = new Rect(col * tileset.TileWidth, row * tileset.TileHeight, tileset.TileWidth, tileset.TileHeight);
                var dest = new Rect(offsetX + x * tileScale, offsetY + y * tileScale, tileScale, tileScale);
                context.DrawImage(bitmap, source, dest);
            }
        }

        void DrawLayerFull(TmxLayer layer)
        {
            for (var y = 0; y < map.Height; y++)
                DrawLayerRow(layer, y);
        }

        // Maps vary in exactly which layers they declare - Town/Mountain/Railroad etc. add
        // Back2/Back3/Buildings2/Buildings3 for extra detail the base Back/Buildings layers
        // don't carry. A fixed 6-name layer list left those un-rendered, showing the plain
        // background fill through as gaps wherever only an extra layer had a tile ("weird
        // squares"). Drawing every layer the map actually declares, split only by whether
        // entities should draw on top of it, covers any map's real layer set.
        //
        // "Paths" is deliberately excluded - it isn't ground art at all. Its tileset
        // (Maps/paths.png) is a flat gray sheet of numbered debug glyphs; the game reads tile
        // indices from this layer to drive daily forage/stone/twig/stump spawn logic and never
        // draws it. Rendering it painted debug icons across every tile with a spawn point.
        var visibleLayers = map.Layers.Where(l => !string.Equals(l.Name, "Paths", StringComparison.OrdinalIgnoreCase)).ToList();
        var afterEntityLayers = visibleLayers
            .Where(l => l.Name.StartsWith("Front", StringComparison.OrdinalIgnoreCase) || l.Name.StartsWith("AlwaysFront", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var beforeEntityLayers = visibleLayers.Except(afterEntityLayers).ToList();

        // Keyed by each entity's BOTTOM row (Position.Y + Height - 1), not its top row - for
        // 1-tall entities (everything except Buildings and multi-tile ResourceClumps) that's
        // the same row either way, but a multi-row-tall entity drawn at its top row would get
        // its lower rows painted over when the loop reached that next row's own terrain layers
        // afterward (terrain-for-row-Y+1 is drawn after entity-for-row-Y, but a 2-tall entity
        // triggered at row Y already occupies row Y+1 too). Drawing it once its bottom-most
        // row's terrain is already down avoids that.
        var entitiesByRow = (Entities ?? Enumerable.Empty<MapEntitySummary>()).ToLookup(e => e.Position.Y + e.Height - 1);
        var allEntities = entitiesByRow.SelectMany(g => g).ToList();

        _flooringLookup.Clear();
        foreach (var e in allEntities)
        {
            if (e.Kind == MapEntityKind.Flooring && e.Source is FlooringEditor flooringSource)
                _flooringLookup[(e.Position.X, e.Position.Y)] = flooringSource.WhichFloor;
        }

        // Row-by-row interleaving approximates the game's Y-sorted draw order: a tall sprite
        // (like a tree canopy) drawn in its own row still visually overlaps rows above it
        // (already drawn), while anything in a row below draws over it in turn - so two trees
        // stacked vertically occlude each other in the right order without a full scene graph.
        for (var y = 0; y < map.Height; y++)
        {
            foreach (var layer in beforeEntityLayers)
                DrawLayerRow(layer, y);

            foreach (var entity in entitiesByRow[y])
            {
                var opacity = Selected is { } sel && !ReferenceEquals(entity, sel) && Occludes(entity, sel) ? 0.35 : 1.0;
                DrawSingleEntity(context, entity, offsetX, offsetY, tileScale, opacity);
            }
        }

        // A "Farmhouse" Building (see TryDrawBuildingSprite's Farmhouse case) draws through the
        // normal per-row entity loop above like everything else, and is what makes it
        // selectable/draggable. This is a fallback ONLY for the case that Building doesn't
        // exist yet - MapTabViewModel.Bind materializes one for every save on load (see
        // FarmMapEditor.AddFarmhouse), so in practice this rarely fires, but a location bound
        // without going through that Bind() (a render-only consumer, a test harness) still
        // shouldn't render an empty lot where the player's house obviously stands.
        var hasFarmhouseBuilding = allEntities.Any(e => e.Kind == MapEntityKind.Building && e.Source is BuildingEditor { BuildingType: "Farmhouse" });
        if (!hasFarmhouseBuilding && LocationName == "Farm" && !string.IsNullOrEmpty(ContentFolder)
            && FarmhouseSprite.TryGetSprite(ContentFolder, HouseUpgradeLevel, out var houseBitmap, out var houseSource))
        {
            var (houseTileX, houseTileY) = FarmhouseSprite.TopLeftTile(FarmhouseSprite.DefaultEntryTile);
            var houseScale = tileScale / 16.0;
            var houseWidth = houseSource.Width * houseScale;
            var houseHeight = houseSource.Height * houseScale;
            var houseDest = new Rect(offsetX + houseTileX * tileScale, offsetY + houseTileY * tileScale, houseWidth, houseHeight);
            context.DrawImage(houseBitmap, houseSource, houseDest);
        }

        foreach (var layer in afterEntityLayers)
            DrawLayerFull(layer);

        // Guarantee the selection stays visible even if something drawn after it (a taller
        // sprite in a later row, or a Front-layer tile) would otherwise cover it - the actual
        // occluder was already drawn translucent above, but redrawing the selection itself on
        // top handles cases the opacity pass doesn't catch (e.g. Front-layer tiles).
        if (Selected is { } selected && allEntities.Contains(selected))
            DrawSingleEntity(context, selected, offsetX, offsetY, tileScale, 1.0);
    }

    /// <summary>
    /// Best-effort check for whether <paramref name="candidate"/>, drawn at or after
    /// <paramref name="selected"/>'s row, could visually cover it - only trees are tall
    /// enough to reach back into an earlier row's space, so this only flags nearby trees
    /// drawn in the same or a following row within canopy-height range.
    /// </summary>
    private static bool Occludes(MapEntitySummary candidate, MapEntitySummary selected)
    {
        if (candidate.Kind != MapEntityKind.Tree)
            return false;

        var dy = candidate.Position.Y - selected.Position.Y;
        if (dy < 0 || dy > 4)
            return false;

        return Math.Abs(candidate.Position.X - selected.Position.X) <= 2;
    }

    private void RenderAbstract(DrawingContext context)
    {
        var background = SeasonBackgrounds.GetValueOrDefault(Season.ToLowerInvariant(), Color.Parse("#C9E4B0"));
        context.FillRectangle(new SolidColorBrush(background), new Rect(Bounds.Size));

        var entities = Entities?.ToList();
        if (entities is null || entities.Count == 0)
        {
            _lastLayout = null;
            return;
        }

        var minX = entities.Min(e => e.Position.X);
        var maxX = entities.Max(e => e.Position.X);
        var minY = entities.Min(e => e.Position.Y);
        var maxY = entities.Max(e => e.Position.Y);

        var spanX = Math.Max(1, maxX - minX + 1);
        var spanY = Math.Max(1, maxY - minY + 1);
        var scale = Math.Max(1.0, Math.Min(Bounds.Width / spanX, Bounds.Height / spanY));
        _lastLayout = (minX, minY, scale);

        var offsetX = -minX * scale;
        var offsetY = -minY * scale;
        foreach (var entity in entities)
            DrawSingleEntity(context, entity, offsetX, offsetY, scale);
    }

    /// <summary>
    /// Draws one entity at its real tile position. <paramref name="pixelOffset"/> converts
    /// tile coordinates to screen pixels (already includes any letterbox centering);
    /// <paramref name="scale"/> is screen pixels per tile. <paramref name="opacity"/> lets an
    /// occluding entity be drawn translucent so a selection behind it stays visible.
    /// <paramref name="recordBounds"/> is false for the throwaway ghost summaries DrawMoveGhost
    /// draws during a drag - real entities record their drawn Rect into _entityScreenBounds for
    /// hit-testing (see FindEntityAt); a ghost isn't a key anything looks up.
    /// </summary>
    private void DrawSingleEntity(DrawingContext context, MapEntitySummary entity, double pixelOffsetX, double pixelOffsetY, double scale, double opacity = 1.0, bool recordBounds = true)
    {
        using var opacityScope = context.PushOpacity(opacity);

        void Record(Rect bounds)
        {
            if (!recordBounds)
                return;

            _entityScreenBounds[entity] = bounds;
            _entityDrawOrder.Add(entity);
        }

        if (entity.Kind == MapEntityKind.Tree && entity.Source is TreeEditor tree
            && !string.IsNullOrEmpty(ContentFolder)
            && TryDrawTreeSprite(context, tree, entity.Position, pixelOffsetX, pixelOffsetY, scale, out var treeBounds))
        {
            Record(treeBounds);
            return;
        }

        if (entity.Kind == MapEntityKind.Object && entity.Source is PlacedObjectEditor craftable
            && !string.IsNullOrEmpty(ContentFolder) && craftable.Item.BigCraftable && craftable.Item.ParentSheetIndex is int craftableIndex
            && BigCraftableSprites.TryGetSprite(ContentFolder, craftableIndex, out var craftableBitmap, out var craftableSource))
        {
            // Bottom-anchored like trees/stumps - a bigCraftable's sprite is always taller
            // (2 tiles) than its 1-tile footprint (BigCraftableSprites remarks).
            var pixelsPerSourcePixel = scale / 16.0;
            var cw = craftableSource.Width * pixelsPerSourcePixel;
            var ch = craftableSource.Height * pixelsPerSourcePixel;
            var cx = pixelOffsetX + entity.Position.X * scale + scale / 2 - cw / 2;
            var cy = pixelOffsetY + entity.Position.Y * scale + scale - ch;
            var craftableRect = new Rect(cx, cy, cw, ch);
            context.DrawImage(craftableBitmap, craftableSource, craftableRect);
            Record(craftableRect);
            return;
        }

        if (entity.Kind == MapEntityKind.Object && entity.Source is PlacedObjectEditor placed
            && !string.IsNullOrEmpty(ContentFolder) && placed.Item.ParentSheetIndex is int index
            && ObjectSprites.TryGetSprite(ContentFolder, index, out var objBitmap, out var objSource))
        {
            var ox = pixelOffsetX + entity.Position.X * scale;
            var oy = pixelOffsetY + entity.Position.Y * scale;
            var objRect = new Rect(ox, oy, scale, scale);
            context.DrawImage(objBitmap, objSource, objRect);
            Record(objRect);
            return;
        }

        if (entity.Kind == MapEntityKind.ResourceClump && entity.Source is ResourceClumpEditor clump
            && !string.IsNullOrEmpty(ContentFolder)
            && ObjectSprites.TryGetClumpSprite(ContentFolder, clump.ParentSheetIndex, clump.Width, clump.Height, out var clumpBitmap, out var clumpSource))
        {
            var cx = pixelOffsetX + entity.Position.X * scale;
            var cy = pixelOffsetY + entity.Position.Y * scale;
            var clumpRect = new Rect(cx, cy, clump.Width * scale, clump.Height * scale);
            context.DrawImage(clumpBitmap, clumpSource, clumpRect);
            Record(clumpRect);
            return;
        }

        if (entity.Kind == MapEntityKind.Building && entity.Source is BuildingEditor building
            && !string.IsNullOrEmpty(ContentFolder)
            && TryDrawBuildingSprite(context, building, entity.Position, entity.Width, entity.Height, pixelOffsetX, pixelOffsetY, scale, out var buildingBounds))
        {
            Record(buildingBounds);
            return;
        }

        if (entity.Kind == MapEntityKind.Grass && entity.Source is GrassEditor grass
            && !string.IsNullOrEmpty(ContentFolder) && GrassSprites.TryGetBitmap(ContentFolder, out var grassBitmap))
        {
            // Real grass is NumberOfWeeds (1-4) independently-scattered tufts, not one graphic -
            // see GrassSprites.GetTufts. Each tuft is bottom-anchored off the tile's own bottom-
            // center (same convention as tree canopies) plus its own small scatter offset.
            var tufts = GrassSprites.GetTufts(grass.GrassType, Season, entity.Position.X, entity.Position.Y, grass.NumberOfWeeds);
            var pixelsPerSourcePixel = scale / 16.0;
            var tileCenterX = pixelOffsetX + entity.Position.X * scale + scale / 2;
            var tileBottom = pixelOffsetY + entity.Position.Y * scale + scale;

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var tuft in tufts)
            {
                var tw = tuft.Source.Width * pixelsPerSourcePixel;
                var th = tuft.Source.Height * pixelsPerSourcePixel;
                var tx = tileCenterX + tuft.OffsetTileX * scale - tw / 2;
                var ty = tileBottom + tuft.OffsetTileY * scale - th;
                var tuftRect = new Rect(tx, ty, tw, th);

                if (tuft.Flip)
                {
                    using (context.PushTransform(FlipHorizontalAround(tuftRect)))
                        context.DrawImage(grassBitmap, tuft.Source, tuftRect);
                }
                else
                {
                    context.DrawImage(grassBitmap, tuft.Source, tuftRect);
                }

                minX = Math.Min(minX, tuftRect.X);
                minY = Math.Min(minY, tuftRect.Y);
                maxX = Math.Max(maxX, tuftRect.Right);
                maxY = Math.Max(maxY, tuftRect.Bottom);
            }

            Record(new Rect(minX, minY, maxX - minX, maxY - minY));
            return;
        }

        if (entity.Kind == MapEntityKind.Bush && entity.Source is BushEditor bush
            && !string.IsNullOrEmpty(ContentFolder)
            && TryDrawBushSprite(context, bush, entity.Position, pixelOffsetX, pixelOffsetY, scale, out var bushBounds))
        {
            Record(bushBounds);
            return;
        }

        if (entity.Kind == MapEntityKind.Flooring && entity.Source is FlooringEditor flooring
            && !string.IsNullOrEmpty(ContentFolder)
            && TryDrawFlooringSprite(context, flooring, entity.Position, pixelOffsetX, pixelOffsetY, scale, out var flooringBounds))
        {
            Record(flooringBounds);
            return;
        }

        if (entity.Kind == MapEntityKind.HoeDirt && entity.Source is HoeDirtEditor dirt
            && !string.IsNullOrEmpty(ContentFolder)
            && HoeDirtSprites.TryGetBitmap(ContentFolder, out var dirtBitmap))
        {
            var dx = pixelOffsetX + entity.Position.X * scale;
            var dy = pixelOffsetY + entity.Position.Y * scale;
            var dirtRect = new Rect(dx, dy, scale, scale);
            context.DrawImage(dirtBitmap, HoeDirtSprites.DrySource, dirtRect);
            if (dirt.State is 1 or 2)
            {
                var overlaySource = dirt.State == 2 ? HoeDirtSprites.PaddyOverlaySource : HoeDirtSprites.WateredOverlaySource;
                context.DrawImage(dirtBitmap, overlaySource, dirtRect);
            }

            var dirtBounds = dirtRect;
            if (dirt.Crop is { } crop
                && CropSprites.TryGetSprite(ContentFolder, crop.RowInSpriteSheet, crop.CurrentPhase, crop.Dead, crop.FullGrown, crop.DayOfCurrentPhase, entity.Position.X, entity.Position.Y, out var cropBitmap, out var cropSource))
            {
                // Anchor exactly like the real game (Crop.draw(): draw origin (8,24) in the
                // 16x32 source, scale 4, positioned at the tile's top-left) - not bottom-anchored
                // like a tree/bush, since a crop's "root" sits higher in its sprite than that.
                var pixelsPerSourcePixel = scale / 16.0;
                var cropWidth = cropSource.Width * pixelsPerSourcePixel;
                var cropHeight = cropSource.Height * pixelsPerSourcePixel;
                var cropRect = new Rect(dx - 8 * pixelsPerSourcePixel, dy - 24 * pixelsPerSourcePixel, cropWidth, cropHeight);

                if (crop.Flip)
                {
                    using (context.PushTransform(FlipHorizontalAround(cropRect)))
                        context.DrawImage(cropBitmap, cropSource, cropRect);
                }
                else
                {
                    context.DrawImage(cropBitmap, cropSource, cropRect);
                }

                dirtBounds = dirtBounds.Union(cropRect);
            }

            Record(dirtBounds);
            return;
        }

        // Multi-tile entities (buildings) fill their real footprint; everything else gets a
        // marker slightly smaller than one tile.
        var isFootprint = entity.Width > 1 || entity.Height > 1;
        var width = isFootprint ? entity.Width * scale : Math.Max(2.5, scale * 0.8);
        var height = isFootprint ? entity.Height * scale : Math.Max(2.5, scale * 0.8);
        var x = pixelOffsetX + entity.Position.X * scale;
        var y = pixelOffsetY + entity.Position.Y * scale;
        var brush = new SolidColorBrush(Color.Parse(entity.ColorHex)) { Opacity = isFootprint ? 0.6 : 1.0 };
        var rect = new Rect(x, y, width, height);
        // An outline keeps markers visible regardless of what's underneath - without one,
        // e.g. an orange rock marker on orange tilled dirt is nearly invisible.
        var outline = new Pen(Brushes.Black, Math.Max(0.5, scale * 0.08));
        context.FillRectangle(brush, rect);
        context.DrawRectangle(outline, rect);
        Record(rect);

        if (ReferenceEquals(entity, Selected))
        {
            var pen = new Pen(Brushes.Red, 2);
            context.DrawRectangle(pen, new Rect(x - 2, y - 2, width + 4, height + 4));
        }
    }

    /// <summary>
    /// Draws a tree's current-growth-stage sprite - reproduces Tree.draw()'s real layering, not
    /// just its adult canopy rect. Stages 0-4 are a single sprite (TryGetGrowthStageSprite),
    /// bottom-anchored to the tile - confirmed against Tree.draw()'s position/origin math to
    /// reduce to exactly that for these stages. Stage 5 (adult) is genuinely TWO layered
    /// sprites in the real game, not one: a trunk/stump graphic (Tree.cs's stumpSourceRect,
    /// (32,96,16,32) - the SAME sprite a chopped-down stump uses) drawn first, positioned one
    /// tile ABOVE the tree's own tile with no bottom-anchoring, THEN the canopy
    /// (treeTopSourceRect, (X,0,48,96) - not the cropped (X,4,48,80) this used to use) drawn on
    /// top, bottom-anchored one tile BELOW. Skipping the trunk layer (the previous version of
    /// this method did) is exactly why a real user report called planted/existing trees "missing
    /// stumps" - every living tree has a visible trunk peeking out below its canopy in the real
    /// game, not just chopped ones. An actual chopped stump (tree.Stump) draws ONLY the trunk
    /// layer, no canopy - also using the corrected position (previously it reused the adult
    /// canopy's bottom-anchor formula, which is real-game-wrong for the trunk sprite's own
    /// origin/position). Returns false (caller falls back to the marker) for tree types we
    /// haven't mapped to a real sprite sheet.
    /// </summary>
    private bool TryDrawTreeSprite(DrawingContext context, TreeEditor tree, TilePosition position, double pixelOffsetX, double pixelOffsetY, double scale, out Rect drawnBounds)
    {
        var pixelsPerSourcePixel = scale / 16.0;
        var tileLeft = pixelOffsetX + position.X * scale;
        var tileTop = pixelOffsetY + position.Y * scale;
        var tileBottom = tileTop + scale;

        void DrawLayer(Bitmap bitmap, Rect source, Rect dest)
        {
            if (tree.Flipped)
            {
                using (context.PushTransform(FlipHorizontalAround(dest)))
                    context.DrawImage(bitmap, source, dest);
            }
            else
            {
                context.DrawImage(bitmap, source, dest);
            }
        }

        if (tree.GrowthStage < 5 && !tree.Stump)
        {
            if (!TreeSprites.TryGetGrowthStageSprite(ContentFolder!, tree.TreeType, Season, tree.GrowthStage, out var youngBitmap, out var youngSource))
            {
                drawnBounds = default;
                return false;
            }

            var width = youngSource.Width * pixelsPerSourcePixel;
            var height = youngSource.Height * pixelsPerSourcePixel;
            var dest = new Rect(tileLeft + scale / 2 - width / 2, tileBottom - height, width, height);
            DrawLayer(youngBitmap, youngSource, dest);
            drawnBounds = dest;
            return true;
        }

        if (tree.Stump)
        {
            if (!TreeSprites.TryGetStumpSprite(ContentFolder!, tree.TreeType, Season, tree.HasMoss, out var stumpBitmap, out var stumpSource))
            {
                drawnBounds = default;
                return false;
            }

            // Real position: Vector2(tileX*64, tileY*64-64), origin Vector2.Zero - one tile
            // above the tree's own tile, top-left anchored (not bottom-anchored).
            var stumpWidth = stumpSource.Width * pixelsPerSourcePixel;
            var stumpHeight = stumpSource.Height * pixelsPerSourcePixel;
            var stumpDest = new Rect(tileLeft, tileTop - scale, stumpWidth, stumpHeight);
            DrawLayer(stumpBitmap, stumpSource, stumpDest);
            drawnBounds = stumpDest;
            return true;
        }

        // Living adult tree: trunk first (mostly hidden behind the canopy, visible only where
        // the canopy art has transparent gaps near its base), canopy on top.
        if (!TreeSprites.TryGetStumpSprite(ContentFolder!, tree.TreeType, Season, tree.HasMoss, out var trunkBitmap, out var trunkSource)
            || !TreeSprites.TryGetAdultSprite(ContentFolder!, tree.TreeType, Season, tree.HasMoss, out var canopyBitmap, out var canopySource))
        {
            drawnBounds = default;
            return false;
        }

        var trunkWidth = trunkSource.Width * pixelsPerSourcePixel;
        var trunkHeight = trunkSource.Height * pixelsPerSourcePixel;
        var trunkDest = new Rect(tileLeft, tileTop - scale, trunkWidth, trunkHeight);
        DrawLayer(trunkBitmap, trunkSource, trunkDest);

        // Real position: Vector2(tileX*64+32, tileY*64+64), origin (24,96) of a 48x96 source -
        // works out to bottom-center-anchored exactly at the tree's own tile bottom (the tall
        // canopy - 6 source-tile-cells - extends upward from there, well past the trunk above).
        var canopyWidth = canopySource.Width * pixelsPerSourcePixel;
        var canopyHeight = canopySource.Height * pixelsPerSourcePixel;
        var canopyDest = new Rect(tileLeft + scale / 2 - canopyWidth / 2, tileBottom - canopyHeight, canopyWidth, canopyHeight);
        DrawLayer(canopyBitmap, canopySource, canopyDest);

        drawnBounds = trunkDest.Union(canopyDest);
        return true;
    }

    /// <summary>Mirrors whatever's drawn within <paramref name="rect"/> around the rect's own
    /// vertical centerline - Avalonia's DrawingContext has no SpriteEffects.FlipHorizontally
    /// equivalent, so this is the matrix-transform substitute: negate X, then translate back so
    /// the rect's own footprint doesn't move (only its contents mirror in place).</summary>
    private static Matrix FlipHorizontalAround(Rect rect)
    {
        var centerX = rect.X + rect.Width / 2;
        return new Matrix(-1, 0, 0, 1, 2 * centerX, 0);
    }

    /// <summary>
    /// Draws a bush anchored bottom-left of its placement tile, growing rightward and upward -
    /// transcribed from the decompiled Bush.draw()'s own anchor math (Vector2 position/origin
    /// passed to SpriteBatch.Draw), not guessed. Working through that formula algebraically
    /// (position - origin*scale, for every size/townBush/size==4 combination) shows the sprite's
    /// left edge always lands exactly on the tile's left edge and its bottom edge always lands
    /// exactly on the tile's bottom edge, regardless of size - the game's own per-size vertical
    /// "raise by one tile" logic (getEffectiveSize() > 0 && ...) exists purely to compensate for
    /// the taller sizes' taller source rects and cancels out to the same bottom-anchor every
    /// time. So this needs none of that branching - it's the same bottom-anchored, left-aligned
    /// rect math as everything else here, just without horizontal centering (a medium/large bush
    /// spans 2-3 tiles starting at its own placement tile, not centered on it).
    /// </summary>
    private bool TryDrawBushSprite(DrawingContext context, BushEditor bush, TilePosition position, double pixelOffsetX, double pixelOffsetY, double scale, out Rect drawnBounds)
    {
        var season = bush.GreenhouseBush ? "spring" : Season;
        var ageInDays = Math.Max(0, CurrentDaysPlayed - bush.DatePlanted);

        if (!BushSprites.TryGetSprite(ContentFolder!, bush.Size, season, bush.TileSheetOffset, bush.TownBush, ageInDays, out var bitmap, out var source))
        {
            drawnBounds = default;
            return false;
        }

        var pixelsPerSourcePixel = scale / 16.0;
        var width = source.Width * pixelsPerSourcePixel;
        var height = source.Height * pixelsPerSourcePixel;

        var tileLeft = pixelOffsetX + position.X * scale;
        var tileBottom = pixelOffsetY + position.Y * scale + scale;
        var dest = new Rect(tileLeft, tileBottom - height, width, height);

        if (bush.Flipped)
        {
            using (context.PushTransform(FlipHorizontalAround(dest)))
                context.DrawImage(bitmap, source, dest);
        }
        else
        {
            context.DrawImage(bitmap, source, dest);
        }

        drawnBounds = dest;
        return true;
    }

    /// <summary>
    /// Draws a floor/path tile with the real live 8-direction neighbor connectivity - verbatim
    /// port of the decompiled Flooring.draw()/gatherNeighbors(): a same-WhichFloor Flooring tile
    /// in each of the 8 surrounding positions sets that direction's bit in a byte mask; the 4
    /// cardinal bits select the base 16x16 sprite cell (FlooringSprites.DrawGuide, e.g. "north
    /// and south neighbor but no east/west" draws a vertical-run cell, "all 4 cardinals" draws a
    /// fully-interior cell), and Default/CornerDecorated connect types additionally draw up to 4
    /// small inner-corner pieces on top wherever the mask shows an actual inward corner (two
    /// adjacent cardinals present, the diagonal between them absent). This - not anything
    /// per-tile-type-specific - is what makes real paths/floors join into a connected shape
    /// instead of rendering as 21 disconnected identical stamps.
    /// </summary>
    private bool TryDrawFlooringSprite(DrawingContext context, FlooringEditor flooring, TilePosition position, double pixelOffsetX, double pixelOffsetY, double scale, out Rect drawnBounds)
    {
        if (!FlooringSprites.Data.TryGetValue(flooring.WhichFloor, out var data))
        {
            drawnBounds = default;
            return false;
        }

        var useWinter = data.WinterTexture is not null && string.Equals(Season, "winter", StringComparison.OrdinalIgnoreCase);
        var textureFile = useWinter ? data.WinterTexture! : data.Texture;
        var corner = useWinter ? data.WinterCorner : data.Corner;
        if (!FlooringSprites.TryGetBitmap(ContentFolder!, textureFile, out var bitmap))
        {
            drawnBounds = default;
            return false;
        }

        byte neighborMask = 0;
        foreach (var (dx, dy, bit) in FlooringSprites.NeighborOffsets)
        {
            if (_flooringLookup.TryGetValue((position.X + dx, position.Y + dy), out var neighborFloor) && neighborFloor == flooring.WhichFloor)
                neighborMask |= bit;
        }

        var pixelsPerSourcePixel = scale / 16.0;
        var tileLeft = pixelOffsetX + position.X * scale;
        var tileTop = pixelOffsetY + position.Y * scale;

        if (data.ShadowType == FloorPathShadowType.Square)
        {
            var shadowRect = new Rect(tileLeft - 4 * pixelsPerSourcePixel, tileTop + 4 * pixelsPerSourcePixel, scale, scale);
            context.FillRectangle(new SolidColorBrush(Colors.Black, 0.33), shadowRect);
        }
        else if (data.ShadowType == FloorPathShadowType.Contoured)
        {
            var shadowSource = FlooringSprites.BaseSourceRect(data, corner, neighborMask, flooring.WhichView);
            var shadowDest = new Rect(tileLeft - 4 * pixelsPerSourcePixel, tileTop + 4 * pixelsPerSourcePixel, scale, scale);
            using (context.PushOpacity(0.33))
                context.DrawImage(bitmap, shadowSource, shadowDest);
        }

        var baseSource = FlooringSprites.BaseSourceRect(data, corner, neighborMask, flooring.WhichView);
        var baseDest = new Rect(tileLeft, tileTop, scale, scale);
        context.DrawImage(bitmap, baseSource, baseDest);

        foreach (var (overlaySource, destOffsetX, destOffsetY) in FlooringSprites.CornerOverlays(data, corner, neighborMask))
        {
            var overlayDest = new Rect(tileLeft + destOffsetX * pixelsPerSourcePixel, tileTop + destOffsetY * pixelsPerSourcePixel,
                overlaySource.Width * pixelsPerSourcePixel, overlaySource.Height * pixelsPerSourcePixel);
            context.DrawImage(bitmap, overlaySource, overlayDest);
        }

        drawnBounds = baseDest;
        return true;
    }

    /// <summary>
    /// Draws a placed building's real sprite (Buildings/{BuildingType}.png - one full image
    /// per building, not a shared spritesheet) anchored at the bottom-center of its tile
    /// footprint, same reasoning as trees: building art extends upward from its base (walls +
    /// roof) taller than the footprint itself.
    /// </summary>
    private bool TryDrawBuildingSprite(DrawingContext context, BuildingEditor building, TilePosition position, int width, int height, double pixelOffsetX, double pixelOffsetY, double scale, out Rect drawnBounds)
    {
        if (building.BuildingType == "Fish Pond")
            return TryDrawFishPondSprite(context, position, width, height, pixelOffsetX, pixelOffsetY, scale, out drawnBounds);

        Bitmap bitmap;
        Rect source;
        string? paintTextureFileName;
        if (building.BuildingType == "Farmhouse")
        {
            // The exterior varies by the player's house upgrade level (a Player field, not a
            // building field), not season - verified against the decompiled
            // Building.getSourceRect(): for a Building whose interior is a FarmHouse, the source
            // rect's row is upgradeLevel (capped at 2, so level 3 reuses level 2's exterior),
            // never SeasonOffset. FarmhouseSprite already ports that exact formula.
            if (!FarmhouseSprite.TryGetSprite(ContentFolder!, HouseUpgradeLevel, out bitmap, out source))
            {
                drawnBounds = default;
                return false;
            }

            // Real Building.GetPaintDataKey() maps buildingType "Farmhouse" -> Data/PaintData's
            // "House" entry, which paints Buildings/houses.png (confirmed: houses_PaintMask.png
            // is a real bundled asset) - the Farmhouse IS paintable in the real game, this was
            // just missed the first time since it draws through FarmhouseSprite, not the generic
            // BuildingSprites path the recolor branch below was originally added to.
            paintTextureFileName = "houses";
        }
        else if (!BuildingSprites.TryGetSprite(ContentFolder!, building.BuildingType, building.SkinId, Season, out bitmap, out source))
        {
            drawnBounds = default;
            return false;
        }
        else
        {
            paintTextureFileName = BuildingSprites.TextureFileNameFor(building.BuildingType, building.SkinId);
        }

        var paint = building.PaintColor;
        if (!(paint.Color1Default && paint.Color2Default && paint.Color3Default)
            && BuildingPainter.TryGetPaintedBitmap(ContentFolder!, paintTextureFileName, bitmap, paint, out var painted))
        {
            bitmap = painted;
        }

        var pixelsPerSourcePixel = scale / 16.0;
        var destWidth = source.Width * pixelsPerSourcePixel;
        var destHeight = source.Height * pixelsPerSourcePixel;

        var footprintLeft = pixelOffsetX + position.X * scale;
        var footprintBottom = pixelOffsetY + position.Y * scale + height * scale;
        var dest = new Rect(footprintLeft + width * scale / 2 - destWidth / 2, footprintBottom - destHeight, destWidth, destHeight);

        context.DrawImage(bitmap, source, dest);
        drawnBounds = dest;
        return true;
    }

    /// <summary>
    /// Fish Pond's own sheet (Buildings/Fish Pond.png) only holds the stone rim and netting -
    /// unlike every other building here, the game composites the water in separately at draw
    /// time, so drawing just the rim (the generic single-image path above) leaves the pond's
    /// interior see-through. Verified against the decompiled FishPond.draw(): it draws a grid of
    /// real water tiles (Game1.mouseCursors, Rectangle(0,2064,64,64) - confirmed real cell, used
    /// here as a single static frame rather than the live-animation-state-dependent version -
    /// waterPosition bobbing/4-frame ripple cycle/per-tile flip aren't meaningful for a static
    /// render) FIRST, unclipped/unmasked, THEN the rim (Rectangle(0,0,80,80) - confirmed via
    /// direct pixel inspection to have a transparent hollow center, not a solid fill) on top,
    /// letting the rim's own real transparency naturally crop the water to the pond's rounded
    /// silhouette - exactly the real game's own technique (layered draw order, not a mask).
    /// An earlier version of this used Avalonia's PushOpacityMask + a tiled ImageBrush instead,
    /// which rendered correctly in this project's own headless RenderTargetBitmap test harness
    /// but never actually showed water in the real, live GPU-composited app window per a real
    /// user report (confirmed not a stale-build/stale-process issue - reproduced after a full
    /// IDE + app restart) - a real divergence between offscreen and live rendering for that
    /// specific Avalonia API combination on this platform. This version avoids PushOpacityMask
    /// entirely, using only plain sequential DrawImage calls (the same primitive every other
    /// sprite in this file already uses successfully) plus a plain rectangular PushClip (just to
    /// keep the water grid from bleeding past the pond's own footprint - the rounded-corner crop
    /// comes from the rim's layering, not this clip).
    /// THEN the netting (Rectangle(80, nettingStyle*48, 80, 48), origin (0,80) - the same anchor
    /// point as the rim/water despite its own shorter 48px height, positioned 2 tiles above the
    /// pond's bottom edge) draped over the top edge (nettingStyle isn't tracked as an editable
    /// field yet - no real save has a Fish Pond to confirm it against - so this always draws
    /// style 0, the real field's own default). Fish/sign are deeper, unverified-field-dependent
    /// layers and are still skipped.
    /// </summary>
    private bool TryDrawFishPondSprite(DrawingContext context, TilePosition position, int width, int height, double pixelOffsetX, double pixelOffsetY, double scale, out Rect drawnBounds)
    {
        if (!BuildingSprites.TryGetBitmap(ContentFolder!, "Fish Pond", out var bitmap))
        {
            drawnBounds = default;
            return false;
        }

        var pixelsPerSourcePixel = scale / 16.0;
        const double frameSize = 80;
        var destSize = frameSize * pixelsPerSourcePixel;

        var footprintLeft = pixelOffsetX + position.X * scale;
        var footprintBottom = pixelOffsetY + position.Y * scale + height * scale;
        var dest = new Rect(footprintLeft + width * scale / 2 - destSize / 2, footprintBottom - destSize, destSize, destSize);

        // Flat tint + real animated water texture, both confined to a rect safely INSET from the
        // full 80x80 box - not the full box itself. Confirmed by direct pixel inspection of Fish
        // Pond.png (not assumed): the rim's transparent "hollow" area doesn't start right at the
        // (0,0,80,80) box's own edges - there's a real ~16px transparent MARGIN around the rim
        // sprite's own outline first (most visible at the top, where the netting posts attach).
        // An earlier version used the FULL box for the water layers, relying only on the rim's
        // hollow-center transparency (drawn after) to crop it - which left that margin's worth
        // of water visibly peeking out past the actual stone border (a real user report: "water
        // ... overlapping the pond edges"). This inset keeps both water layers safely within the
        // confirmed-hollow interior instead.
        // (The OTHER real asset here, Rectangle(0,80,80,80) - "a pre-shaped water silhouette" -
        // was considered as a guaranteed-correct-shape base layer instead of this inset
        // approach, but direct pixel inspection found it's pure grayscale (R=G=B on every
        // opaque pixel, no blue at all) - the real game tints it via a SpriteBatch color
        // multiply, which Avalonia's DrawingContext has no equivalent for without reaching for
        // the same kind of mask/effect API already found unreliable in the live app. Skipped
        // rather than guessed at.)
        // Measured directly (row-by-row alpha scan of the real sprite, not a single guessed
        // margin): the hollow's stable interior spans source Y 15-66, with the left/right edges
        // varying roughly 14-18 and 63-66 across those rows - and the transition from "narrow"
        // (row 13, ~20px wide) to "full width" (row 15, ~48px wide) happens over just those 2
        // rows, i.e. the real corner curve is tight/sharp, not a gentle arc. A 12px clip radius
        // (this method's second fix) was rounder than that real corner, cutting away more than
        // the rim actually does and reading as "water doesn't reach the corners" (a real user
        // report). Tighter inset + a much smaller radius here instead.
        var waterDest = new Rect(dest.X + 10 * pixelsPerSourcePixel, dest.Y + 11 * pixelsPerSourcePixel,
            dest.Width - 20 * pixelsPerSourcePixel, dest.Height - 21 * pixelsPerSourcePixel);
        var waterClip = new RoundedRect(waterDest, 5 * pixelsPerSourcePixel);
        using (context.PushClip(waterClip))
        {
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(60, 126, 150)), waterDest);
            if (MenuChrome.Cursors is { } cursors)
            {
                var waterSource = new Rect(0, 2064, 64, 64);
                for (var ty = waterDest.Y; ty < waterDest.Bottom; ty += scale)
                    for (var tx = waterDest.X; tx < waterDest.Right; tx += scale)
                        context.DrawImage(cursors, waterSource, new Rect(tx, ty, scale, scale));
            }
        }

        // Rim on top, full size - crops the water layers above to the pond's exact rounded
        // shape (its real transparent hollow center, confirmed by direct pixel inspection).
        context.DrawImage(bitmap, new Rect(0, 0, frameSize, frameSize), dest);

        const int nettingStyle = 0;
        var nettingHeight = 48 * pixelsPerSourcePixel;
        var nettingSource = new Rect(80, nettingStyle * 48, 80, 48);
        var nettingDest = new Rect(dest.X, footprintBottom - 2 * scale - destSize, destSize, nettingHeight);
        context.DrawImage(bitmap, nettingSource, nettingDest);

        drawnBounds = dest.Union(nettingDest);
        return true;
    }

    private TilePosition FloorTile(Point point, (double MinX, double MinY, double Scale) layout)
        => new((int)Math.Floor(point.X / layout.Scale + layout.MinX), (int)Math.Floor(point.Y / layout.Scale + layout.MinY));

    /// <summary>Resolution is deferred to release (see OnPointerReleased) so a click can turn
    /// into a drag - this just records where the gesture started and captures the pointer so
    /// drags that leave the control's bounds are still tracked. Spacebar-held always means
    /// "pan" regardless of anything else (checked first, before click/drag-select or any
    /// placement tool), matching the standard space+drag convention.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus(); // so this control (not whatever was focused before) receives the space-key events panning depends on

        if (_lastLayout is not { } layout)
            return;

        if (_spaceHeld)
        {
            _panStartPoint = e.GetPosition(this);
            _panStartOffset = (PanOffsetTileX, PanOffsetTileY);
            e.Pointer.Capture(this);
            UpdateCursor();
            return;
        }

        var tile = FloorTile(e.GetPosition(this), layout);

        // A draw tool being armed turns click-and-drag into "paint one placement per tile
        // crossed" instead of marquee range-select - see OnPointerMoved. A plain click (no
        // drag) behaves the same either way: ClickedTile fires once for the pressed tile.
        // Line/Rectangle differ here: they don't fire anything at press (or during the drag) -
        // only a live preview - and commit the whole computed shape once, at release.
        if (IsPlacementToolActive)
        {
            _isPainting = true;
            _lastPaintedTile = tile;
            _shapeStartTile = tile;
            if (DrawShape == DrawShape.Freehand)
                ClickedTile = tile;
            e.Pointer.Capture(this);
            return;
        }

        ClickedTile = tile;
        _dragStartTile = tile;
        _dragCurrentTile = tile;
        _isDragging = false;

        // Whether THIS drag (if it turns into one) moves an entity or marquee-selects is
        // decided right here, from what's under the initial press - not re-evaluated as the
        // drag continues. Pressing on empty space always marquees even if the drag later
        // crosses over an entity; pressing on an entity always (potentially) moves it - the
        // whole SelectedRange together if the press landed on one of its members, that entity
        // alone otherwise.
        _dragEntity = FindEntityAt(e.GetPosition(this), layout);
        _dragGroup = _dragEntity is { } hit && SelectedRange.Contains(hit) ? SelectedRange : null;

        e.Pointer.Capture(this);
    }

    /// <summary>Crossing tile boundaries while the button is held is what turns a click into a
    /// drag - a plain click never triggers this (press and release land in the same tile).</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_lastLayout is not { } layout)
            return;

        var position = e.GetPosition(this);
        _lastPointerPosition = position;

        if (_panStartPoint is { } panStart && _panStartOffset is { } panOffset)
        {
            PanOffsetTileX = panOffset.X - (position.X - panStart.X) / layout.Scale;
            PanOffsetTileY = panOffset.Y - (position.Y - panStart.Y) / layout.Scale;
            return;
        }

        if (_isPainting)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            var paintTile = FloorTile(position, layout);
            if (paintTile == _lastPaintedTile)
                return;

            _lastPaintedTile = paintTile;

            if (DrawShape == DrawShape.Freehand)
            {
                ClickedTile = paintTile; // each new tile crossed re-fires OnClickedTileChanged, placing (or queuing a confirmation) there
                return;
            }

            // Line/Rectangle: no placement yet - just redraw the live shape preview (see
            // DrawShapePreview), committed as one batch only on release.
            InvalidateVisual();
            return;
        }

        if (_dragStartTile is { } start)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            var tile = FloorTile(position, layout);
            if (!_isDragging && tile != start)
                _isDragging = true;

            if (!_isDragging)
                return;

            _dragCurrentTile = tile;
            InvalidateVisual(); // redraw the live marquee rectangle
            return;
        }

        // Idle hover - not panning/painting/dragging. Drives the draw-tool footprint preview
        // (DrawHoverPreview) and the marquee-vs-selectable-item cursor (UpdateCursor).
        var hoverTile = FloorTile(position, layout);
        if (_hoverTile != hoverTile)
        {
            _hoverTile = hoverTile;
            InvalidateVisual();
        }

        UpdateCursor();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);

        if (_panStartPoint is not null)
        {
            _panStartPoint = null;
            _panStartOffset = null;
            UpdateCursor();
            return;
        }

        if (_isPainting)
        {
            if (DrawShape != DrawShape.Freehand && _shapeStartTile is { } shapeStart && _lastPaintedTile is { } shapeEnd)
            {
                ShapeStrokeTiles = DrawShape == DrawShape.Line
                    ? ComputeLineTiles(shapeStart, shapeEnd)
                    : ComputeRectangleTiles(shapeStart, shapeEnd);
            }

            _isPainting = false;
            _lastPaintedTile = null;
            _shapeStartTile = null;
            InvalidateVisual();
            return;
        }

        if (_dragStartTile is not { } start)
            return;

        if (_isDragging && _dragCurrentTile is { } end)
        {
            if (_dragEntity is not null)
            {
                // Dragging a single entity that ISN'T part of the current marquee selection
                // switches focus to just that entity, same as a plain click would - only a
                // group drag (press landed on an existing SelectedRange member) keeps it.
                if (_dragGroup is null)
                    SelectedRange = Array.Empty<MapEntitySummary>();

                var deltaX = end.X - start.X;
                var deltaY = end.Y - start.Y;
                var moves = (_dragGroup ?? new[] { _dragEntity })
                    .Select(entity => (Entity: entity, NewPosition: ClampFootprint(new TilePosition(entity.Position.X + deltaX, entity.Position.Y + deltaY), entity.Width, entity.Height)))
                    .Where(m => m.NewPosition != m.Entity.Position)
                    .ToList();

                if (moves.Count > 0)
                    MoveRequest = new EntityMoveRequest(moves);
            }
            else
            {
                ResolveRangeSelection(start, end);
            }
        }
        else
        {
            ResolveSingleClick(e.GetPosition(this));
        }

        _dragStartTile = null;
        _dragCurrentTile = null;
        _dragEntity = null;
        _dragGroup = null;
        _isDragging = false;
        InvalidateVisual();
    }

    /// <summary>Plain scroll pans (vertical wheel/trackpad delta -> Y pan, horizontal/shift
    /// -> X pan - standard convention); Ctrl or Cmd+scroll zooms instead, anchored so the tile
    /// under the cursor stays under the cursor.</summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (_lastLayout is not { } layout)
            return;

        var zooming = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!zooming)
        {
            PanOffsetTileX -= e.Delta.X * 3;
            PanOffsetTileY -= e.Delta.Y * 3;
            e.Handled = true;
            return;
        }

        ApplyZoom(Zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1), e.GetPosition(this), layout);
        e.Handled = true;
    }

    /// <summary>macOS trackpad pinch. Delta.X/.Y are both the same raw per-callback
    /// magnification value straight from NSEvent (see the constructor remarks) - small and
    /// signed (positive = spreading fingers = zoom in), so it's applied as a multiplicative
    /// factor around 1.0 each callback, same shape as the ctrl+scroll step above just driven by
    /// a real gesture instead of a fixed 1.1 increment.</summary>
    private void OnTouchPadMagnify(object? sender, PointerDeltaEventArgs e)
    {
        if (_lastLayout is not { } layout)
            return;

        ApplyZoom(Zoom * (1 + e.Delta.X), e.GetPosition(this), layout);
        e.Handled = true;
    }

    /// <summary>Shared cursor-anchored zoom math: solves the new pan offset so the same map
    /// tile stays under the cursor/pinch-center before and after the zoom change.</summary>
    private void ApplyZoom(double requestedZoom, Point cursor, (double MinX, double MinY, double Scale) layout)
    {
        var tileUnderCursorX = cursor.X / layout.Scale + layout.MinX;
        var tileUnderCursorY = cursor.Y / layout.Scale + layout.MinY;

        var newZoom = Math.Clamp(requestedZoom, 1.0, 12.0);
        var newTileScale = 16.0 * newZoom;

        Zoom = newZoom;
        PanOffsetTileX = tileUnderCursorX - cursor.X / newTileScale;
        PanOffsetTileY = tileUnderCursorY - cursor.Y / newTileScale;
    }

    /// <summary>Space arms panning (see OnPointerPressed); [ and ] (with the friendlier - and =
    /// as aliases, since brackets require a modifier key on some layouts) resize the draw
    /// tools' brush - the standard shortcut in most paint tools, kept working even without a
    /// tool armed so it's discoverable/harmless either way.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Space)
        {
            _spaceHeld = true;
            UpdateCursor();
        }
        else if (e.Key is Key.OemCloseBrackets or Key.OemPlus)
        {
            BrushSize = Math.Clamp(BrushSize + 1, 1, MapTabViewModel.MaxBrushSize);
            InvalidateVisual();
        }
        else if (e.Key is Key.OemOpenBrackets or Key.OemMinus)
        {
            BrushSize = Math.Clamp(BrushSize - 1, 1, MapTabViewModel.MaxBrushSize);
            InvalidateVisual();
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.C)
        {
            CopyRequest++;
            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.V)
        {
            PasteRequest++;
            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.D)
        {
            DuplicateRequest++;
            e.Handled = true;
        }
        else if (e.Key is Key.Delete or Key.Back)
        {
            DeleteRequest++;
            e.Handled = true;
        }
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            NudgeSelection(e.Key);
            e.Handled = true;
        }
    }

    /// <summary>Arrow-key nudge - reuses the exact same MoveRequest pipeline a drag-move already
    /// produces (see MapTabViewModel.OnMoveRequestChanged), just computed from a 1-tile delta
    /// instead of a drag. Nudges the whole SelectedRange as one batch if a marquee selection is
    /// active, otherwise just Selected - same single/multi split as Delete/Copy/Duplicate.</summary>
    private void NudgeSelection(Key key)
    {
        var (dx, dy) = key switch
        {
            Key.Left => (-1, 0),
            Key.Right => (1, 0),
            Key.Up => (0, -1),
            _ => (0, 1),
        };

        var toMove = SelectedRange.Count > 0 ? SelectedRange : Selected is { } s ? new[] { s } : Array.Empty<MapEntitySummary>();
        if (toMove.Count == 0)
            return;

        var moves = toMove.Select(entity => (entity, new TilePosition(entity.Position.X + dx, entity.Position.Y + dy))).ToList();
        MoveRequest = new EntityMoveRequest(moves);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space)
        {
            _spaceHeld = false;
            UpdateCursor();
        }
    }

    /// <summary>Entity under a screen point - shared by OnPointerPressed (deciding move-drag vs
    /// marquee-drag) and ResolveSingleClick (plain-click selection). Checks each entity's real
    /// drawn bounds first (_entityScreenBounds/_entityDrawOrder, populated by the last real-map
    /// render) - many sprites (a tree canopy, a tall building) are drawn well outside their
    /// logical tile footprint, so hit-testing only the footprint meant most of what's actually
    /// visible wasn't clickable/draggable at all. Walked in reverse draw order, so an overlap
    /// between two sprites resolves to whichever was drawn LAST - visually on top, matching the
    /// row-interleaved Y-sort draw order, not an arbitrary or size-based pick. Falls back to the
    /// old nearest-footprint-tile check (within one tile) for anything not in that cache yet -
    /// abstract view, or a frame that hasn't rendered once since load.</summary>
    private MapEntitySummary? FindEntityAt(Point screenPosition, (double MinX, double MinY, double Scale) layout)
    {
        if (Entities is null)
            return null;

        for (var i = _entityDrawOrder.Count - 1; i >= 0; i--)
        {
            var candidate = _entityDrawOrder[i];
            if (_entityScreenBounds.TryGetValue(candidate, out var candidateBounds) && candidateBounds.Contains(screenPosition))
                return candidate;
        }

        var tileX = screenPosition.X / layout.Scale + layout.MinX;
        var tileY = screenPosition.Y / layout.Scale + layout.MinY;

        double DistanceSq(MapEntitySummary entity)
        {
            var dx = Math.Max(0, Math.Max(entity.Position.X - tileX, tileX - (entity.Position.X + entity.Width - 1)));
            var dy = Math.Max(0, Math.Max(entity.Position.Y - tileY, tileY - (entity.Position.Y + entity.Height - 1)));
            return dx * dx + dy * dy;
        }

        var nearest = Entities
            .Select(entity => (entity, distSq: DistanceSq(entity)))
            .OrderBy(e => e.distSq)
            .FirstOrDefault();

        return nearest.entity is not null && nearest.distSq <= 1.0 ? nearest.entity : null;
    }

    /// <summary>Exactly today's single-click behavior (nearest-entity hit test, falling back
    /// to a tile-info dump) - recomputed from the release position rather than the original
    /// press position, but for a non-drag click those are the same tile anyway.</summary>
    private void ResolveSingleClick(Point releasePosition)
    {
        SelectedRange = Array.Empty<MapEntitySummary>();

        if (_lastLayout is not { } layout)
            return;

        if (FindEntityAt(releasePosition, layout) is { } hit)
        {
            Selected = hit;
            SelectedTileInfo = null;
            return;
        }

        // Nothing dynamic there - report the base map's tile(s), if we have real tile art loaded.
        Selected = null;
        var tileX = releasePosition.X / layout.Scale + layout.MinX;
        var tileY = releasePosition.Y / layout.Scale + layout.MinY;
        SelectedTileInfo = _map is null ? null : DescribeTile((int)Math.Floor(tileX), (int)Math.Floor(tileY));
    }

    /// <summary>Every entity whose footprint intersects the dragged tile rectangle (inclusive,
    /// in either drag direction) becomes the new SelectedRange; clears the single Selected -
    /// the two are mutually exclusive views in the side panel.</summary>
    private void ResolveRangeSelection(TilePosition start, TilePosition end)
    {
        Selected = null;
        SelectedTileInfo = null;

        if (Entities is null)
        {
            SelectedRange = Array.Empty<MapEntitySummary>();
            return;
        }

        var minX = Math.Min(start.X, end.X);
        var maxX = Math.Max(start.X, end.X);
        var minY = Math.Min(start.Y, end.Y);
        var maxY = Math.Max(start.Y, end.Y);

        SelectedRange = Entities
            .Where(entity => entity.Position.X + entity.Width - 1 >= minX && entity.Position.X <= maxX
                           && entity.Position.Y + entity.Height - 1 >= minY && entity.Position.Y <= maxY)
            .ToList();
    }

    private string DescribeTile(int x, int y)
    {
        var layers = _map!.GetTileInfo(x, y);
        if (layers.Count == 0)
            return $"Tile ({x},{y}): nothing here on any layer.";

        var lines = layers.Select(l =>
        {
            var props = l.Properties.Count == 0
                ? ""
                : " - " + string.Join(", ", l.Properties.Select(p => $"{p.Key}={p.Value}"));
            return $"{l.LayerName}: gid {l.Gid} ({l.TilesetImage}){props}";
        });

        return $"Tile ({x},{y}):\n" + string.Join("\n", lines);
    }
}
