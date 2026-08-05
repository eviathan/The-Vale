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
/// extraction, this renders the game's actual tile art (terrain, paths, buildings) with
/// placed entities (trees, grass, resource clumps, objects) overlaid as colored dots at
/// their real tile position. Without a content folder, it falls back to the same abstract
/// flat-color dot grid as before, scaled to the entities' own bounding box.
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

    static FarmMapControl()
    {
        AffectsRender<FarmMapControl>(EntitiesProperty, SelectedProperty, SeasonProperty, ContentFolderProperty);
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
    private (double MinX, double MinY, double Scale)? _lastLayout;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ContentFolderProperty && ContentFolder != _loadedFolder)
            TryLoadMap();
    }

    private void TryLoadMap()
    {
        _loadedFolder = ContentFolder;
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
            _map = _loader.LoadFarmMap();
            Status = $"Real tile art loaded from {ContentFolder}.";
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
        _lastLayout = (0, 0, scale * map.TileWidth); // Scale is per-tile here, not per-pixel - see hit-testing below.

        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));

        void DrawLayer(string name)
        {
            var layer = map.Layers.FirstOrDefault(l => l.Name == name);
            if (layer is null)
                return;

            for (var y = 0; y < map.Height; y++)
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
                    var dest = new Rect(x * map.TileWidth * scale, y * map.TileHeight * scale, map.TileWidth * scale, map.TileHeight * scale);
                    context.DrawImage(bitmap, source, dest);
                }
            }
        }

        DrawLayer("Back");
        DrawLayer("Buildings");
        DrawLayer("Paths");

        // Grass is already visible in the real terrain art (it's baked into the Back layer
        // as ground texture) - drawing a marker per grass tile on top just adds noise, unlike
        // trees/objects/clumps which aren't part of the base map and need one to be visible/selectable.
        DrawEntities(context, 0, 0, scale * map.TileWidth, skipGrass: true);

        DrawLayer("Front");
        DrawLayer("AlwaysFront");
        DrawLayer("AlwaysFront2");
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

        DrawEntities(context, minX, minY, scale);
    }

    private void DrawEntities(DrawingContext context, double originX, double originY, double scale, bool skipGrass = false)
    {
        var entities = Entities?.ToList();
        if (entities is null || entities.Count == 0)
        {
            _lastLayout = (originX, originY, scale);
            return;
        }

        _lastLayout = (originX, originY, scale);
        var dotSize = Math.Max(2.5, scale * 0.8);
        var outline = new Pen(Brushes.Black, Math.Max(0.5, scale * 0.08));

        foreach (var entity in entities)
        {
            if (skipGrass && entity.Kind == MapEntityKind.Grass)
                continue;

            if (entity.Kind == MapEntityKind.Tree && entity.Source is TreeEditor tree
                && !string.IsNullOrEmpty(ContentFolder)
                && TryDrawTreeSprite(context, tree, entity.Position, originX, originY, scale))
            {
                continue;
            }

            if (entity.Kind == MapEntityKind.Object && entity.Source is PlacedObjectEditor placed
                && !string.IsNullOrEmpty(ContentFolder) && placed.Item.ParentSheetIndex is int index
                && ObjectSprites.TryGetSprite(ContentFolder, index, out var objBitmap, out var objSource))
            {
                var ox = (entity.Position.X - originX) * scale;
                var oy = (entity.Position.Y - originY) * scale;
                context.DrawImage(objBitmap, objSource, new Rect(ox, oy, scale, scale));
                continue;
            }

            var x = (entity.Position.X - originX) * scale;
            var y = (entity.Position.Y - originY) * scale;
            var brush = new SolidColorBrush(Color.Parse(entity.ColorHex));
            var rect = new Rect(x, y, dotSize, dotSize);
            // An outline keeps markers visible regardless of what's underneath - without one,
            // e.g. an orange rock marker on orange tilled dirt is nearly invisible.
            context.FillRectangle(brush, rect);
            context.DrawRectangle(outline, rect);
        }

        if (Selected is { } selected)
        {
            var x = (selected.Position.X - originX) * scale;
            var y = (selected.Position.Y - originY) * scale;
            var pen = new Pen(Brushes.Red, 2);
            context.DrawRectangle(pen, new Rect(x - 2, y - 2, dotSize + 4, dotSize + 4));
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
    private bool TryDrawTreeSprite(DrawingContext context, TreeEditor tree, TilePosition position, double originX, double originY, double scale)
    {
        var variant = (position.X * 3 + position.Y * 7) % 2;
        if (!TreeSprites.TryGetAdultSprite(ContentFolder!, tree.TreeType, Season, variant, out var bitmap, out var source))
            return false;

        var pixelsPerSourcePixel = scale / 16.0;
        var width = source.Width * pixelsPerSourcePixel;
        var height = source.Height * pixelsPerSourcePixel;

        var tileLeft = (position.X - originX) * scale;
        var tileBottom = (position.Y - originY) * scale + scale;
        var dest = new Rect(tileLeft + scale / 2 - width / 2, tileBottom - height, width, height);

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

        var nearest = Entities
            .Select(entity => (entity, distSq: Math.Pow(entity.Position.X - tileX, 2) + Math.Pow(entity.Position.Y - tileY, 2)))
            .OrderBy(e => e.distSq)
            .FirstOrDefault();

        if (nearest.entity is not null && nearest.distSq <= 1.0)
            Selected = nearest.entity;
    }
}
