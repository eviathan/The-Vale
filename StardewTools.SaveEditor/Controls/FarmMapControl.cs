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

    static FarmMapControl()
    {
        AffectsRender<FarmMapControl>(EntitiesProperty, SelectedProperty, SeasonProperty, ContentFolderProperty, LocationNameProperty, HouseUpgradeLevelProperty,
            ZoomProperty, PanOffsetTileXProperty, PanOffsetTileYProperty);
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
    private TilePosition? _dragStartTile;
    private TilePosition? _dragCurrentTile;
    private bool _isDragging;
    private bool _isPainting;
    private TilePosition? _lastPaintedTile;
    private bool _needsViewCentering;
    private bool _spaceHeld;
    private Point? _panStartPoint;
    private (double X, double Y)? _panStartOffset;

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
    }

    private void OnEntitiesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

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
    }

    /// <summary>The live rectangle overlay while a drag-select is in progress - drawn after
    /// either render path so it works the same in real-map and abstract-view modes, since
    /// both populate _lastLayout the same way.</summary>
    private void DrawMarquee(DrawingContext context)
    {
        if (!_isDragging || _lastLayout is not { } layout || _dragStartTile is not { } start || _dragCurrentTile is not { } current)
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

    private void RenderRealMap(DrawingContext context, TmxMap map, MapAssetLoader loader)
    {
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

        // The farmhouse isn't a placed Building - it's not in Entities at all, see
        // FarmhouseSprite - so it needs its own draw call rather than going through the
        // per-row entity loop above.
        if (LocationName == "Farm" && !string.IsNullOrEmpty(ContentFolder)
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
    /// </summary>
    private void DrawSingleEntity(DrawingContext context, MapEntitySummary entity, double pixelOffsetX, double pixelOffsetY, double scale, double opacity = 1.0)
    {
        using var opacityScope = context.PushOpacity(opacity);

        if (entity.Kind == MapEntityKind.Tree && entity.Source is TreeEditor tree
            && !string.IsNullOrEmpty(ContentFolder)
            && TryDrawTreeSprite(context, tree, entity.Position, pixelOffsetX, pixelOffsetY, scale))
        {
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
            context.DrawImage(craftableBitmap, craftableSource, new Rect(cx, cy, cw, ch));
            return;
        }

        if (entity.Kind == MapEntityKind.Object && entity.Source is PlacedObjectEditor placed
            && !string.IsNullOrEmpty(ContentFolder) && placed.Item.ParentSheetIndex is int index
            && ObjectSprites.TryGetSprite(ContentFolder, index, out var objBitmap, out var objSource))
        {
            var ox = pixelOffsetX + entity.Position.X * scale;
            var oy = pixelOffsetY + entity.Position.Y * scale;
            context.DrawImage(objBitmap, objSource, new Rect(ox, oy, scale, scale));
            return;
        }

        if (entity.Kind == MapEntityKind.ResourceClump && entity.Source is ResourceClumpEditor clump
            && !string.IsNullOrEmpty(ContentFolder)
            && ObjectSprites.TryGetClumpSprite(ContentFolder, clump.ParentSheetIndex, clump.Width, clump.Height, out var clumpBitmap, out var clumpSource))
        {
            var cx = pixelOffsetX + entity.Position.X * scale;
            var cy = pixelOffsetY + entity.Position.Y * scale;
            context.DrawImage(clumpBitmap, clumpSource, new Rect(cx, cy, clump.Width * scale, clump.Height * scale));
            return;
        }

        if (entity.Kind == MapEntityKind.Building && entity.Source is BuildingEditor building
            && !string.IsNullOrEmpty(ContentFolder)
            && TryDrawBuildingSprite(context, building, entity.Position, entity.Width, entity.Height, pixelOffsetX, pixelOffsetY, scale))
        {
            return;
        }

        if (entity.Kind == MapEntityKind.Grass && entity.Source is GrassEditor grass
            && !string.IsNullOrEmpty(ContentFolder)
            && GrassSprites.TryGetSprite(ContentFolder, grass.GrassType, Season, entity.Position.X, entity.Position.Y, out var grassBitmap, out var grassSource))
        {
            // Grass tufts are taller than one tile and rooted at the ground, same anchoring as
            // tree canopies (TryDrawTreeSprite) - bottom-center of the tile, not top-left.
            var pixelsPerSourcePixel = scale / 16.0;
            var gw = grassSource.Width * pixelsPerSourcePixel;
            var gh = grassSource.Height * pixelsPerSourcePixel;
            var gx = pixelOffsetX + entity.Position.X * scale + scale / 2 - gw / 2;
            var gy = pixelOffsetY + entity.Position.Y * scale + scale - gh;
            context.DrawImage(grassBitmap, grassSource, new Rect(gx, gy, gw, gh));
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

        if (ReferenceEquals(entity, Selected))
        {
            var pen = new Pen(Brushes.Red, 2);
            context.DrawRectangle(pen, new Rect(x - 2, y - 2, width + 4, height + 4));
        }
    }

    /// <summary>
    /// Draws a real adult tree sprite anchored at the tile's bottom-center (matching how the
    /// game positions trees - trunk base on the tile, canopy extending up and to both sides).
    /// Always uses the adult sprite regardless of the tree's actual growth stage - we don't
    /// have verified sapling/bush-stage frame coordinates yet, so a grown tree in the right
    /// place beats an accurate-stage square marker. Returns false (caller falls back to the
    /// marker) for tree types we haven't mapped to a real sprite sheet.
    /// </summary>
    /// <summary>A tree is a two-part sprite (canopy on top of a trunk) when standing, or just
    /// the stump alone once chopped (tree.Stump) - never both, and never the full tree while
    /// stumped. See TreeSprites.TryGetStumpSprite for where the stump rect comes from.</summary>
    private bool TryDrawTreeSprite(DrawingContext context, TreeEditor tree, TilePosition position, double pixelOffsetX, double pixelOffsetY, double scale)
    {
        bool found;
        Bitmap bitmap;
        Rect source;

        if (tree.Stump)
        {
            found = TreeSprites.TryGetStumpSprite(ContentFolder!, tree.TreeType, Season, out bitmap, out source);
        }
        else
        {
            var variant = (position.X * 3 + position.Y * 7) % 2;
            found = TreeSprites.TryGetAdultSprite(ContentFolder!, tree.TreeType, Season, variant, out bitmap, out source);
        }

        if (!found)
            return false;

        var pixelsPerSourcePixel = scale / 16.0;
        var width = source.Width * pixelsPerSourcePixel;
        var height = source.Height * pixelsPerSourcePixel;

        var tileLeft = pixelOffsetX + position.X * scale;
        var tileBottom = pixelOffsetY + position.Y * scale + scale;
        var dest = new Rect(tileLeft + scale / 2 - width / 2, tileBottom - height, width, height);

        context.DrawImage(bitmap, source, dest);
        return true;
    }

    /// <summary>
    /// Draws a placed building's real sprite (Buildings/{BuildingType}.png - one full image
    /// per building, not a shared spritesheet) anchored at the bottom-center of its tile
    /// footprint, same reasoning as trees: building art extends upward from its base (walls +
    /// roof) taller than the footprint itself.
    /// </summary>
    private bool TryDrawBuildingSprite(DrawingContext context, BuildingEditor building, TilePosition position, int width, int height, double pixelOffsetX, double pixelOffsetY, double scale)
    {
        if (building.BuildingType == "Fish Pond")
            return TryDrawFishPondSprite(context, position, width, height, pixelOffsetX, pixelOffsetY, scale);

        if (!BuildingSprites.TryGetSprite(ContentFolder!, building.BuildingType, out var bitmap, out var source))
            return false;

        var pixelsPerSourcePixel = scale / 16.0;
        var destWidth = source.Width * pixelsPerSourcePixel;
        var destHeight = source.Height * pixelsPerSourcePixel;

        var footprintLeft = pixelOffsetX + position.X * scale;
        var footprintBottom = pixelOffsetY + position.Y * scale + height * scale;
        var dest = new Rect(footprintLeft + width * scale / 2 - destWidth / 2, footprintBottom - destHeight, destWidth, destHeight);

        context.DrawImage(bitmap, source, dest);
        return true;
    }

    /// <summary>
    /// Fish Pond's own sheet (Buildings/Fish Pond.png) only holds the stone rim - unlike every
    /// other building here, the game composites the water in separately at draw time, so
    /// drawing just the rim (the generic single-image path above) leaves the pond's interior
    /// see-through. Verified against the decompiled FishPond.draw(): it fills Rectangle(0, 80,
    /// 80, 80) from its own sheet - a pre-shaped water silhouette, drawn as an opacity mask here
    /// since Avalonia has no SpriteBatch-style color tint - with Color(60, 126, 150) (the
    /// game's own default water tint, used whenever a pond has no special override color), THEN
    /// the rim itself (Rectangle(0, 0, 80, 80)) on top. The animated ripple overlay and per-pond
    /// netting style the real game also draws are cosmetic layers on top of this and are
    /// skipped - BuildingEditor doesn't track a netting style field yet.
    /// </summary>
    private bool TryDrawFishPondSprite(DrawingContext context, TilePosition position, int width, int height, double pixelOffsetX, double pixelOffsetY, double scale)
    {
        if (!BuildingSprites.TryGetBitmap(ContentFolder!, "Fish Pond", out var bitmap))
            return false;

        var pixelsPerSourcePixel = scale / 16.0;
        const double frameSize = 80;
        var destSize = frameSize * pixelsPerSourcePixel;

        var footprintLeft = pixelOffsetX + position.X * scale;
        var footprintBottom = pixelOffsetY + position.Y * scale + height * scale;
        var dest = new Rect(footprintLeft + width * scale / 2 - destSize / 2, footprintBottom - destSize, destSize, destSize);

        var waterMask = new ImageBrush(bitmap)
        {
            SourceRect = new RelativeRect(0, 80, frameSize, frameSize, RelativeUnit.Absolute),
            Stretch = Stretch.Fill,
        };
        using (context.PushOpacityMask(waterMask, dest))
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(60, 126, 150)), dest);

        context.DrawImage(bitmap, new Rect(0, 0, frameSize, frameSize), dest);
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
            return;
        }

        var tile = FloorTile(e.GetPosition(this), layout);

        // A draw tool being armed turns click-and-drag into "paint one placement per tile
        // crossed" instead of marquee range-select - see OnPointerMoved. A plain click (no
        // drag) behaves the same either way: ClickedTile fires once for the pressed tile.
        if (IsPlacementToolActive)
        {
            _isPainting = true;
            _lastPaintedTile = tile;
            ClickedTile = tile;
            e.Pointer.Capture(this);
            return;
        }

        ClickedTile = tile;
        _dragStartTile = tile;
        _dragCurrentTile = tile;
        _isDragging = false;
        e.Pointer.Capture(this);
    }

    /// <summary>Crossing tile boundaries while the button is held is what turns a click into a
    /// drag - a plain click never triggers this (press and release land in the same tile).</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_lastLayout is not { } layout)
            return;

        if (_panStartPoint is { } panStart && _panStartOffset is { } panOffset)
        {
            var current = e.GetPosition(this);
            PanOffsetTileX = panOffset.X - (current.X - panStart.X) / layout.Scale;
            PanOffsetTileY = panOffset.Y - (current.Y - panStart.Y) / layout.Scale;
            return;
        }

        if (_isPainting)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            var paintTile = FloorTile(e.GetPosition(this), layout);
            if (paintTile == _lastPaintedTile)
                return;

            _lastPaintedTile = paintTile;
            ClickedTile = paintTile; // each new tile crossed re-fires OnClickedTileChanged, placing (or queuing a confirmation) there
            return;
        }

        if (_dragStartTile is not { } start)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var tile = FloorTile(e.GetPosition(this), layout);
        if (!_isDragging && tile != start)
            _isDragging = true;

        if (!_isDragging)
            return;

        _dragCurrentTile = tile;
        InvalidateVisual(); // redraw the live marquee rectangle
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);

        if (_panStartPoint is not null)
        {
            _panStartPoint = null;
            _panStartOffset = null;
            return;
        }

        if (_isPainting)
        {
            _isPainting = false;
            _lastPaintedTile = null;
            return;
        }

        if (_dragStartTile is not { } start)
            return;

        if (_isDragging && _dragCurrentTile is { } end)
            ResolveRangeSelection(start, end);
        else
            ResolveSingleClick(e.GetPosition(this));

        _dragStartTile = null;
        _dragCurrentTile = null;
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space)
            _spaceHeld = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space)
            _spaceHeld = false;
    }

    /// <summary>Exactly today's single-click behavior (nearest-entity hit test, falling back
    /// to a tile-info dump) - recomputed from the release position rather than the original
    /// press position, but for a non-drag click those are the same tile anyway.</summary>
    private void ResolveSingleClick(Point releasePosition)
    {
        SelectedRange = Array.Empty<MapEntitySummary>();

        if (_lastLayout is not { } layout || Entities is null)
            return;

        var tileX = releasePosition.X / layout.Scale + layout.MinX;
        var tileY = releasePosition.Y / layout.Scale + layout.MinY;

        // Distance to the nearest point of the entity's footprint, not just its top-left tile -
        // otherwise a multi-tile building is only clickable right at one corner.
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

        if (nearest.entity is not null && nearest.distSq <= 1.0)
        {
            Selected = nearest.entity;
            SelectedTileInfo = null;
            return;
        }

        // Nothing dynamic there - report the base map's tile(s), if we have real tile art loaded.
        Selected = null;
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
