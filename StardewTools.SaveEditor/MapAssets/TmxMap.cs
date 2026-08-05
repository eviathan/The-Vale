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

    public bool Contains(int gid) => gid >= FirstGid && gid < FirstGid + TileCount;

    public (int Col, int Row) TilePosition(int gid)
    {
        var localId = gid - FirstGid;
        return (localId % Columns, localId / Columns);
    }
}

public sealed class TmxLayer
{
    public required string Name { get; init; }

    /// <summary>Row-major tile GIDs, 0 = no tile.</summary>
    public required int[] Tiles { get; init; }
}

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

        var tilesets = root.Elements("tileset").Select(t => new TmxTileset
        {
            FirstGid = (int)t.Attribute("firstgid")!,
            TileCount = (int)t.Attribute("tilecount")!,
            Columns = (int)t.Attribute("columns")!,
            TileWidth = (int)t.Attribute("tilewidth")!,
            TileHeight = (int)t.Attribute("tileheight")!,
            ImageSource = (string)t.Element("image")!.Attribute("source")!,
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
}
