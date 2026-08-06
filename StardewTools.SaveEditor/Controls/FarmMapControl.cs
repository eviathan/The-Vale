using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
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

    static FarmMapControl()
    {
        AffectsRender<FarmMapControl>(EntitiesProperty, SelectedProperty, SeasonProperty, ContentFolderProperty, LocationNameProperty);
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if ((change.Property == ContentFolderProperty || change.Property == LocationNameProperty)
            && (ContentFolder != _loadedFolder || LocationName != _loadedLocation))
        {
            TryLoadMap();
        }
    }

    private void TryLoadMap()
    {
        _loadedFolder = ContentFolder;
        _loadedLocation = LocationName;
        _map = null;
        _loader = null;

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
        {
            RenderRealMap(context, _map, _loader);
            return;
        }

        RenderAbstract(context);
    }

    private void RenderRealMap(DrawingContext context, TmxMap map, MapAssetLoader loader)
    {
        var mapPixelWidth = map.Width * map.TileWidth;
        var mapPixelHeight = map.Height * map.TileHeight;
        var scale = Math.Max(0.01, Math.Min(Bounds.Width / mapPixelWidth, Bounds.Height / mapPixelHeight));

        // Center the map instead of anchoring at (0,0) - the map's aspect ratio rarely matches
        // the control's exactly, so one dimension always has leftover space. That space used to
        // be a flat black fill reaching the control edge, which read as a rendering bug rather
        // than a letterboxing bar.
        var offsetX = (Bounds.Width - mapPixelWidth * scale) / 2;
        var offsetY = (Bounds.Height - mapPixelHeight * scale) / 2;
        var tileScale = scale * map.TileWidth;
        _lastLayout = (-offsetX / tileScale, -offsetY / tileScale, tileScale); // Scale is per-tile here, not per-pixel - see hit-testing below.

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
                var dest = new Rect(offsetX + x * map.TileWidth * scale, offsetY + y * map.TileHeight * scale, map.TileWidth * scale, map.TileHeight * scale);
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

        var entitiesByRow = (Entities ?? Enumerable.Empty<MapEntitySummary>()).ToLookup(e => e.Position.Y);
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

        if (entity.Kind == MapEntityKind.Object && entity.Source is PlacedObjectEditor placed
            && !string.IsNullOrEmpty(ContentFolder) && placed.Item.ParentSheetIndex is int index
            && ObjectSprites.TryGetSprite(ContentFolder, index, out var objBitmap, out var objSource))
        {
            var ox = pixelOffsetX + entity.Position.X * scale;
            var oy = pixelOffsetY + entity.Position.Y * scale;
            context.DrawImage(objBitmap, objSource, new Rect(ox, oy, scale, scale));
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
    private bool TryDrawTreeSprite(DrawingContext context, TreeEditor tree, TilePosition position, double pixelOffsetX, double pixelOffsetY, double scale)
    {
        var variant = (position.X * 3 + position.Y * 7) % 2;
        if (!TreeSprites.TryGetAdultSprite(ContentFolder!, tree.TreeType, Season, variant, out var bitmap, out var source))
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

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (_lastLayout is not { } layout || Entities is null)
            return;

        var point = e.GetPosition(this);
        var tileX = point.X / layout.Scale + layout.MinX;
        var tileY = point.Y / layout.Scale + layout.MinY;

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
