using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

public enum FloorPathConnectType { Default, Path, CornerDecorated, Random }
public enum FloorPathShadowType { None, Square, Contoured }

public sealed record FloorPathData(
    string Key, string Name, int ItemId, string Texture, string? WinterTexture,
    PixelPoint Corner, PixelPoint WinterCorner, FloorPathConnectType ConnectType,
    FloorPathShadowType ShadowType, int CornerSize);

/// <summary>
/// Real floor/path tile data (Data/FloorsAndPaths.json) plus the exact neighbor-connectivity
/// sprite-selection table - ported verbatim from the decompiled StardewValley.TerrainFeatures.
/// Flooring: `populateDrawGuide()` (the byte-neighbor-mask -> sprite-sheet-cell-index table) and
/// `gatherNeighbors()`/`draw()` (which neighbors count - only same-WhichFloor Flooring tiles,
/// 8-directional - and how the mask maps to both the base tile and the CornerDecorated/Default
/// connect types' extra inner-corner overlay pieces). No display-name field exists in
/// FloorsAndPaths.json itself - Name here is the matching Data/Objects.json item's own real name
/// (Wood Path, Cobblestone Path, ...), read directly rather than guessed.
/// </summary>
public static class FlooringSprites
{
    private static readonly Dictionary<byte, int> DrawGuide = new()
    {
        [0] = 0, [6] = 1, [14] = 2, [12] = 3,
        [4] = 16, [7] = 17, [15] = 18, [13] = 19,
        [5] = 32, [3] = 33, [11] = 34, [9] = 35,
        [1] = 48, [2] = 49, [10] = 50, [8] = 51,
    };

    private static readonly int[] DrawGuideList = DrawGuide.Values.ToArray();

    public const byte N = 1, E = 2, S = 4, W = 8, NE = 16, NW = 32, SE = 64, SW = 128;

    /// <summary>8-direction offsets paired with their own bit and the neighbor's opposite bit -
    /// verbatim from Flooring's private `_offsets` table.</summary>
    public static readonly (int Dx, int Dy, byte Bit)[] NeighborOffsets =
    {
        (0, -1, N), (0, 1, S), (1, 0, E), (-1, 0, W),
        (1, -1, NE), (-1, -1, NW), (1, 1, SE), (-1, 1, SW),
    };

    private static readonly Dictionary<string, Bitmap?> BitmapCache = new();
    private static IReadOnlyDictionary<string, FloorPathData>? _data;

    public static IReadOnlyDictionary<string, FloorPathData> Data => _data ??= Load();

    private static Dictionary<string, FloorPathData> Load()
    {
        var result = new Dictionary<string, FloorPathData>();
        var floorsPath = Path.Combine(BundledContent.FolderPath, "Data", "FloorsAndPaths.json");
        var objectsPath = Path.Combine(BundledContent.FolderPath, "Data", "Objects.json");
        if (!File.Exists(floorsPath) || !File.Exists(objectsPath))
            return result;

        using var objectsDoc = JsonDocument.Parse(File.ReadAllText(objectsPath));
        var itemNames = objectsDoc.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.TryGetProperty("Name", out var n) ? n.GetString() ?? p.Name : p.Name);

        using var doc = JsonDocument.Parse(File.ReadAllText(floorsPath));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var el = prop.Value;
            var itemId = el.TryGetProperty("ItemId", out var idEl) && int.TryParse(idEl.GetString(), out var parsedId) ? parsedId : 0;
            var texture = TextureFileName(el.TryGetProperty("Texture", out var t) ? t.GetString() : null) ?? "Flooring";
            var winterTexture = TextureFileName(el.TryGetProperty("WinterTexture", out var wt) ? wt.GetString() : null);
            var corner = ReadPoint(el, "Corner");
            var winterCorner = ReadPoint(el, "WinterCorner");
            var connectType = el.TryGetProperty("ConnectType", out var ct) && Enum.TryParse<FloorPathConnectType>(ct.GetString(), out var parsedCt) ? parsedCt : FloorPathConnectType.Default;
            var shadowType = el.TryGetProperty("ShadowType", out var st) && Enum.TryParse<FloorPathShadowType>(st.GetString(), out var parsedSt) ? parsedSt : FloorPathShadowType.None;
            var cornerSize = el.TryGetProperty("CornerSize", out var cs) ? cs.GetInt32() : 4;
            var name = itemId != 0 && itemNames.TryGetValue(itemId.ToString(), out var n2) ? n2 : prop.Name;

            result[prop.Name] = new FloorPathData(prop.Name, name, itemId, texture, winterTexture, corner, winterCorner, connectType, shadowType, cornerSize);
        }

        return result;
    }

    private static PixelPoint ReadPoint(JsonElement parent, string propName)
    {
        if (!parent.TryGetProperty(propName, out var el) || el.ValueKind != JsonValueKind.Object)
            return default;
        var x = el.TryGetProperty("X", out var xEl) ? xEl.GetInt32() : 0;
        var y = el.TryGetProperty("Y", out var yEl) ? yEl.GetInt32() : 0;
        return new PixelPoint(x, y);
    }

    private static string? TextureFileName(string? contentPath)
        => contentPath?.Split('\\', '/').LastOrDefault();

    public static bool TryGetBitmap(string contentFolder, string textureFileName, out Bitmap bitmap)
    {
        var key = contentFolder + "|" + textureFileName;
        if (!BitmapCache.TryGetValue(key, out var cached))
        {
            var path = Path.Combine(contentFolder, "TerrainFeatures", textureFileName + ".png");
            cached = File.Exists(path) ? new Bitmap(path) : null;
            BitmapCache[key] = cached;
        }

        bitmap = cached!;
        return cached is not null;
    }

    /// <summary>The 16x16 source cell for a given live 8-direction neighbor bitmask - verbatim
    /// port of Flooring.draw()'s own `drawGuide[neighborMask & 0xF]` (only the 4 cardinal bits
    /// select the base tile; diagonals only matter for the corner-overlay pieces below).</summary>
    public static Rect BaseSourceRect(FloorPathData data, PixelPoint corner, byte neighborMask, int whichView)
    {
        var sourceRectPosition = data.ConnectType == FloorPathConnectType.Random
            ? DrawGuideList[Math.Clamp(whichView, 0, DrawGuideList.Length - 1)]
            : DrawGuide[(byte)(neighborMask & 0xF)];

        return new Rect(corner.X + sourceRectPosition * 16 % 256, sourceRectPosition / 16 * 16 + corner.Y, 16, 16);
    }

    /// <summary>The Default/CornerDecorated connect types draw up to 4 extra small inner-corner
    /// pieces on top of the base tile - verbatim port of Flooring.draw()'s 4 corner `if` blocks
    /// (each: two adjacent cardinal neighbors present AND the diagonal between them absent, i.e.
    /// an actual inward-facing corner of the flooring region, not a straight run or an already-
    /// filled square). Source/dest offsets are relative to the tile's own top-left in 16px sheet
    /// units / 4x-scaled screen units respectively - callers scale by their own tile pixel size.
    /// Returns nothing for Path/Random connect types (verbatim: Flooring.draw()'s switch has no
    /// case for them - Path-type floors, i.e. the actual "path" items like Wood/Gravel/Cobblestone
    /// Path, never draw this overlay at all).</summary>
    public static IEnumerable<(Rect Source, double DestOffsetXInSheetPx, double DestOffsetYInSheetPx)> CornerOverlays(FloorPathData data, PixelPoint corner, byte neighborMask)
    {
        if (data.ConnectType is not (FloorPathConnectType.Default or FloorPathConnectType.CornerDecorated))
            yield break;

        var b = data.CornerSize;
        if ((neighborMask & 9) == 9 && (neighborMask & NW) == 0)
            yield return (new Rect(64 - b + corner.X, 48 - b + corner.Y, b, b), 0, 0);
        if ((neighborMask & 3) == 3 && (neighborMask & NE) == 0)
            yield return (new Rect(16 + corner.X, 48 - b + corner.Y, b, b), 64 - b * 4, 0);
        if ((neighborMask & 6) == 6 && (neighborMask & SE) == 0)
            yield return (new Rect(16 + corner.X, corner.Y, b, b), 64 - b * 4, data.ConnectType == FloorPathConnectType.CornerDecorated ? 64 - b * 4 : 48);
        if ((neighborMask & 0xC) == 12 && (neighborMask & SW) == 0)
            yield return (new Rect(64 - b + corner.X, corner.Y, b, b), 0, 64 - b * 4);
    }
}
