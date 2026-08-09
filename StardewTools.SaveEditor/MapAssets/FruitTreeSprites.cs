using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// A planted fruit tree's real sprite (TileSheets/fruitTrees.png, confirmed 432x720px = 9 rows
/// of 80px, one row per fruit tree type per Data/FruitTrees.json's own TextureSpriteRow - all 8
/// real types share this one sheet). Ported from decompiled FruitTree.draw(): growth stages 0-3
/// (seed/sprout/sapling/bush) are a single 48x80 sprite at column stage*48 within the tree's row;
/// stage 4+ (fully grown) is a two-layer composite instead - a season-tinted 48x64 canopy (column
/// (12 + seasonIndex*3)*16, same row) drawn over a fixed, non-seasonal 48x32 trunk/base (column
/// 384, row+48). The real game positions these two layers with fiddly, asymmetric SpriteBatch
/// origin offsets for a specific artistic overlap; reproduced here as a simpler bottom-anchored
/// stack (trunk at the tile's own bottom, canopy overlapping its top by one native-pixel-row *
/// pixelsPerSourcePixel) - visually a correct, recognizable tree of the right type/season, not
/// pixel-identical to the original's exact overlap. Floating fruit-on-branches (a separate,
/// per-instance decorative overlay once a mature tree has grown some) isn't modeled - the data
/// model's own `fruit` list is always empty for a freshly-planted tree regardless.
/// </summary>
public static class FruitTreeSprites
{
    private static Bitmap? _cached;
    private static string? _cachedFolder;

    private const int SheetWidth = 432;
    private const int RowHeight = 80;

    public static bool TryGetBitmap(string contentFolder, out Bitmap bitmap)
    {
        bitmap = null!;

        if (_cachedFolder != contentFolder)
        {
            var path = Path.Combine(contentFolder, "TileSheets", "fruitTrees.png");
            if (!File.Exists(path))
                return false;

            _cached = new Bitmap(path);
            _cachedFolder = contentFolder;
        }

        if (_cached is null)
            return false;

        bitmap = _cached;
        return true;
    }

    /// <summary>Growth stages 0-3's single-sprite source rect.</summary>
    public static Rect SaplingSource(int textureSpriteRow, int growthStage)
    {
        var column = growthStage switch { 0 => 0, 1 => 48, 2 => 96, _ => 144 };
        return new Rect(column, textureSpriteRow * RowHeight, 48, 80);
    }

    /// <summary>Stage 4+'s seasonal canopy layer - seasonIndex 0=spring/1=summer/2=fall/3=winter,
    /// same convention as everywhere else in this codebase (Season string -> index).</summary>
    public static Rect CanopySource(int textureSpriteRow, int seasonIndex)
    {
        var column = (12 + seasonIndex * 3) * 16 % SheetWidth;
        return new Rect(column, textureSpriteRow * RowHeight, 48, 64);
    }

    /// <summary>Stage 4+'s fixed, non-seasonal trunk/base layer.</summary>
    public static Rect TrunkSource(int textureSpriteRow) => new(384, textureSpriteRow * RowHeight + 48, 48, 32);

    public static int SeasonIndex(string season) => season switch
    {
        "summer" => 1,
        "fall" => 2,
        "winter" => 3,
        _ => 0,
    };
}
