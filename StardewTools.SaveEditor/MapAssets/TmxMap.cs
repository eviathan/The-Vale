using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>One &lt;tileset&gt; entry: a range of tile GIDs backed by one image.</summary>
public sealed class TmxTileset
{
    public required int FirstGid { get; init; }
    public required int TileCount { get; init; }
    public required int Columns { get; init; }
    public required int TileWidth { get; init; }
    public required int TileHeight { get; init; }
    public required string ImageSource { get; init; }

    /// <summary>Per-tile &lt;properties&gt; (e.g. Diggable/Buildable/Type), keyed by local tile id (gid - firstgid).</summary>
    public required IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> TileProperties { get; init; }

    public bool Contains(int gid) => gid >= FirstGid && gid < FirstGid + TileCount;

    public (int Col, int Row) TilePosition(int gid)
    {
        var localId = gid - FirstGid;
        return (localId % Columns, localId / Columns);
    }

    public IReadOnlyDictionary<string, string>? PropertiesFor(int gid)
        => TileProperties.GetValueOrDefault(gid - FirstGid);
}

public sealed class TmxLayer
{
    public required string Name { get; init; }

    /// <summary>Row-major tile GIDs, 0 = no tile.</summary>
    public required int[] Tiles { get; init; }
}

/// <summary>One layer's tile at a clicked position, with whatever properties the tileset defines for it.</summary>
public sealed record TileLayerInfo(string LayerName, int Gid, string TilesetImage, IReadOnlyDictionary<string, string> Properties);

/// <summary>
/// A parsed Tiled TMX map, as produced by StardewXnbHack unpacking one of the game's .xnb
/// map files. This is the standard, well-documented Tiled format - CSV-encoded tile layers
/// plus tileset definitions with a firstgid range each - not Stardew-specific in any way,
/// which is what makes it tractable to parse directly rather than needing xTile itself.
/// </summary>
public sealed class TmxMap
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int TileWidth { get; init; }
    public required int TileHeight { get; init; }
    public required IReadOnlyList<TmxTileset> Tilesets { get; init; }
    public required IReadOnlyList<TmxLayer> Layers { get; init; }

    public static TmxMap Load(string path)
    {
        var doc = XDocument.Load(path);
        var root = doc.Root!;

        var tilesets = root.Elements("tileset").Select(t =>
        {
            var tileProps = t.Elements("tile").ToDictionary(
                tileEl => (int)tileEl.Attribute("id")!,
                tileEl => (IReadOnlyDictionary<string, string>)(tileEl.Element("properties")?.Elements("property")
                    .ToDictionary(p => (string)p.Attribute("name")!, p => (string)p.Attribute("value")!)
                    ?? new Dictionary<string, string>()));

            return new TmxTileset
            {
                FirstGid = (int)t.Attribute("firstgid")!,
                TileCount = (int)t.Attribute("tilecount")!,
                Columns = (int)t.Attribute("columns")!,
                TileWidth = (int)t.Attribute("tilewidth")!,
                TileHeight = (int)t.Attribute("tileheight")!,
                ImageSource = (string)t.Element("image")!.Attribute("source")!,
                TileProperties = tileProps,
            };
        }).ToList();

        var layers = root.Elements("layer").Select(l =>
        {
            var csv = l.Element("data")!.Value;
            var tiles = csv
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToArray();

            return new TmxLayer { Name = (string)l.Attribute("name")!, Tiles = tiles };
        }).ToList();

        return new TmxMap
        {
            Width = (int)root.Attribute("width")!,
            Height = (int)root.Attribute("height")!,
            TileWidth = (int)root.Attribute("tilewidth")!,
            TileHeight = (int)root.Attribute("tileheight")!,
            Tilesets = tilesets,
            Layers = layers,
        };
    }

    public TmxTileset? TilesetFor(int gid) => gid == 0 ? null : Tilesets.LastOrDefault(t => t.Contains(gid));

    /// <summary>Every layer's tile at (x,y) that isn't empty, with whatever properties its tileset defines for it.</summary>
    public IReadOnlyList<TileLayerInfo> GetTileInfo(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return Array.Empty<TileLayerInfo>();

        var results = new List<TileLayerInfo>();
        foreach (var layer in Layers)
        {
            var gid = layer.Tiles[y * Width + x];
            if (gid == 0)
                continue;

            var tileset = TilesetFor(gid);
            if (tileset is null)
                continue;

            results.Add(new TileLayerInfo(layer.Name, gid, tileset.ImageSource, tileset.PropertiesFor(gid) ?? new Dictionary<string, string>()));
        }

        return results;
    }

    /// <summary>The named layer's tile property at (x,y), or null if out of bounds/no
    /// tile/property not set - mirrors GameLocation.doesTileHaveProperty's tileset-properties
    /// path (the per-instance tile.Properties override path doesn't apply here: real Stardew
    /// maps only use per-instance overrides for Warp-style string lists, never Water/Buildable/
    /// Diggable/NoFurniture, which are always plain tileset properties).</summary>
    private string? GetTileProperty(int x, int y, string layerName, string propertyName)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return null;

        var layer = Layers.FirstOrDefault(l => l.Name == layerName);
        var gid = layer?.Tiles[y * Width + x] ?? 0;
        if (gid == 0)
            return null;

        return TilesetFor(gid)?.PropertiesFor(gid)?.GetValueOrDefault(propertyName);
    }

    /// <summary>Mirrors GameLocation.isWaterTile.</summary>
    public bool IsWaterTile(int x, int y) => GetTileProperty(x, y, "Back", "Water") != null;

    /// <summary>
    /// Mirrors GameLocation.isTilePlaceable - what the game checks before letting a plain
    /// Object be set down (verified against the decompiled source: no Back tile at all, a
    /// water tile, or a "NoFurniture" tile property all block it). Doesn't check "already
    /// occupied by another placed entity" or farm-type-specific extras (Farm doesn't override
    /// IsLocationSpecificPlacementRestriction, confirmed - it's a no-op) - the map tab's own
    /// overlap/PendingPlacement logic already covers the former.
    /// </summary>
    public bool IsTilePlaceable(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return false;

        var backLayer = Layers.FirstOrDefault(l => l.Name == "Back");
        if (backLayer is null || backLayer.Tiles[y * Width + x] == 0)
            return false;

        if (IsWaterTile(x, y))
            return false;

        if (GetTileProperty(x, y, "Back", "NoFurniture") != null)
            return false;

        return true;
    }

    /// <summary>
    /// Mirrors GameLocation.isBuildable's per-tile logic (verified against the decompiled
    /// source): placeable, and either explicitly marked "Buildable" or "Diggable" without being
    /// explicitly marked un-buildable. Skips the map-wide ValidBuildRect/LooserBuildRestrictions
    /// properties - the bundled Farm.tmx sets neither, so the per-tile check is what actually
    /// governs buildability on a real farm - and the "already occupied by a building" check,
    /// which the map tab's own overlap/PendingPlacement logic already covers.
    /// </summary>
    public bool IsTileBuildable(int x, int y)
    {
        if (!IsTilePlaceable(x, y))
            return false;

        var buildable = GetTileProperty(x, y, "Back", "Buildable");
        if (buildable is not null && (buildable.Equals("t", StringComparison.OrdinalIgnoreCase) || buildable.Equals("true", StringComparison.OrdinalIgnoreCase)))
            return true;

        var explicitlyNotBuildable = buildable is not null && buildable.Equals("f", StringComparison.OrdinalIgnoreCase);
        return !explicitlyNotBuildable && GetTileProperty(x, y, "Back", "Diggable") != null;
    }

    /// <summary>Mirrors Hoe.cs's own tillability check exactly: a Back-layer tile is diggable
    /// purely by having the "Diggable" property set, nothing else (no separate water/placeable
    /// check in the real tool code - in practice a water tile never also carries "Diggable", so
    /// this doesn't need to layer IsTilePlaceable on top).</summary>
    public bool IsTileDiggable(int x, int y) => GetTileProperty(x, y, "Back", "Diggable") != null;
}
