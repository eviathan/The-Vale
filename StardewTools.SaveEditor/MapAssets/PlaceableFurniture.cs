using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// One real, placeable piece of furniture from Data/Furniture.json - everything
/// FarmMapEditor.AddFurniture needs already resolved (sourceRect/boundingBox in pixel units,
/// furniture_type, sprite index), so placement is a direct write with no further lookup.
///
/// Scope: only the "default sheet" (TileSheets/furniture.png, confirmed 512px wide against the
/// bundled asset) subset with a plain numeric item id is covered here - of Data/Furniture.json's
/// 645 real entries, 298 use a texture override (a second, different-width sheet - Joja/Wizard/
/// Junimo/Retro themed sets plus furniture_2/furniture_3/etc - not modeled, since each would need
/// its own confirmed sheet width before the same X%width/Y/width*16 math could be trusted), and of
/// the 347 remaining, Bed/Bed Double/Bed Child (14) and FishTank (5) are real BedFurniture/
/// FishTankFurniture subclasses with their own bounding-box/source-rect overrides this class
/// doesn't model (unverified against real examples - see FurnitureEditor remarks). That leaves
/// 328 real pieces (chairs, tables, couches, armchairs, dressers, paintings, lamps, decor,
/// bookcases, rugs, windows, fireplaces, benches, sconces, torches, long tables) fully covered.
/// </summary>
public sealed record PlaceableFurniture(
    string ItemId, string Name, string TypeName, int FurnitureType, int SpriteIndex,
    int Rotations, int Price, int SourceX, int SourceY, int SourceWidth, int SourceHeight,
    int BoundingWidth, int BoundingHeight, bool DrawHeldObjectLow)
{
    public override string ToString() => Name;
}

public static class PlaceableFurnitureCatalog
{
    /// <summary>TileSheets/furniture.png's real pixel width (confirmed against the bundled
    /// asset) - decompiled Furniture.getDefaultSourceRect's own `SpriteIndex * 16 % texture.Width`
    /// formula needs this to place each 16x16 cell correctly; only valid for entries using this
    /// default sheet (see class remarks - anything with a texture override is excluded).</summary>
    private const int SheetWidth = 512;

    private static readonly HashSet<string> ExcludedTypeNames = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "bed", "bed double", "bed child", "fishtank",
    };

    /// <summary>Decompiled Furniture.getTypeNumberFromName's own table.</summary>
    private static int TypeNumberFromName(string typeName)
    {
        var lower = typeName.ToLowerInvariant();
        if (lower.StartsWith("bed")) return 15;
        return lower switch
        {
            "chair" => 0,
            "bench" => 1,
            "couch" => 2,
            "armchair" => 3,
            "dresser" => 4,
            "long table" => 5,
            "painting" => 6,
            "lamp" => 7,
            "decor" => 8,
            "bookcase" => 10,
            "table" => 11,
            "rug" => 12,
            "window" => 13,
            "fireplace" => 14,
            "torch" => 16,
            "sconce" => 17,
            _ => 9,
        };
    }

    /// <summary>Decompiled Furniture.getDefaultSourceRectForType's own per-type cell size (in
    /// 16px cells), used when Data/Furniture.json's own tilesheet-size field is "-1".</summary>
    private static readonly Dictionary<int, (int W, int H)> DefaultSourceCellsByType = new()
    {
        [0] = (1, 2), [1] = (2, 2), [2] = (3, 2), [3] = (2, 2), [4] = (2, 2), [5] = (5, 3),
        [6] = (2, 2), [17] = (1, 2), [7] = (1, 3), [8] = (1, 2), [10] = (2, 3), [11] = (2, 3),
        [12] = (3, 2), [13] = (1, 2), [14] = (2, 5), [16] = (1, 2),
    };

    /// <summary>Decompiled Furniture.getDefaultBoundingBoxForType's own per-type tile size, used
    /// when Data/Furniture.json's own bounding-box-size field is "-1".</summary>
    private static readonly Dictionary<int, (int W, int H)> DefaultBoundingTilesByType = new()
    {
        [0] = (1, 1), [1] = (2, 1), [2] = (3, 1), [3] = (2, 1), [4] = (2, 1), [5] = (5, 2),
        [6] = (2, 2), [17] = (1, 2), [7] = (1, 1), [8] = (1, 1), [10] = (2, 1), [11] = (2, 2),
        [12] = (3, 2), [13] = (1, 2), [14] = (2, 1), [16] = (1, 1),
    };

    private static IReadOnlyList<PlaceableFurniture>? _all;

    public static IReadOnlyList<PlaceableFurniture> All => _all ??= Load();

    public static string NameFor(string itemId) => All.FirstOrDefault(f => f.ItemId == itemId)?.Name ?? $"Furniture {itemId}";

    private static List<PlaceableFurniture> Load()
    {
        var result = new List<PlaceableFurniture>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Furniture.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (!int.TryParse(prop.Name, out var spriteIndex))
                continue; // no numeric sprite index to derive from and no override present (see below) - skip

            var fields = prop.Value.GetString()?.Split('/');
            if (fields is null || fields.Length < 6)
                continue;

            var textureOverride = fields.Length > 9 ? fields[9] : "";
            if (!string.IsNullOrEmpty(textureOverride))
                continue; // different sheet, unverified width - see class remarks

            var typeName = fields[1];
            if (ExcludedTypeNames.Contains(typeName))
                continue;

            var name = fields[0];
            var furnitureType = TypeNumberFromName(typeName);
            var rotations = int.TryParse(fields[4], out var r) ? r : 1;
            var price = int.TryParse(fields[5], out var p) ? p : 0;

            var (sourceCellsW, sourceCellsH) = fields[2] == "-1"
                ? DefaultSourceCellsByType.GetValueOrDefault(furnitureType, (1, 2))
                : ParseSize(fields[2], (1, 2));
            var (boundingTilesW, boundingTilesH) = fields[3] == "-1"
                ? DefaultBoundingTilesByType.GetValueOrDefault(furnitureType, (1, 1))
                : ParseSize(fields[3], (1, 1));

            var sourceX = spriteIndex * 16 % SheetWidth;
            var sourceY = spriteIndex * 16 / SheetWidth * 16;

            result.Add(new PlaceableFurniture(
                prop.Name, name, typeName, furnitureType, spriteIndex, rotations, price,
                sourceX, sourceY, sourceCellsW * 16, sourceCellsH * 16,
                boundingTilesW * 64, boundingTilesH * 64,
                name.ToLowerInvariant().Contains("tea")));
        }

        return result.OrderBy(f => f.TypeName).ThenBy(f => f.Name).ToList();
    }

    private static (int, int) ParseSize(string raw, (int, int) fallback)
    {
        var parts = raw.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
            return (w, h);
        return fallback;
    }
}
