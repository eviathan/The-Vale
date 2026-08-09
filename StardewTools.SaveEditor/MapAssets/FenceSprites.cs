using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// A placed Fence's real sprite - each material has its own texture file under LooseSprites
/// (confirmed via Data/Fences.json's own Texture field: Wood/322="Fence1", Stone/323="Fence2",
/// Iron/324="Fence3", Hardwood/298="Fence5" - note the real game skips "Fence4"), each a 48x352px
/// sheet = 3 columns x 11 rows of 16x32px cells (confirmed on disk). The real game picks a source
/// cell via a 4-neighbor connectivity SUM (not a bitmask - Fence.cs's own getDrawSum(): Left=10,
/// Right=100, Down=500, Up=1000, only counting neighbors that are the SAME fence material -
/// ExoticObjectCatalog/FenceXmlBuilder already write the real xsi:type="Fence" shape this needs
/// (ParentSheetIndex identifies the material), this class only adds the missing render-time
/// connectivity, same scope split as HoeDirtSprites. Gates (item id 325) aren't placeable via
/// this tool yet (not in ExoticObjectCatalog) and are deliberately not modeled here either - a
/// gate immediately next to a real fence would show as a dead-end cap instead of connecting,
/// same honestly-scoped gap as everywhere else in this codebase.
/// </summary>
public static class FenceSprites
{
    private static readonly Dictionary<string, Bitmap> Cache = new();

    private static readonly Dictionary<int, string> TextureFileByItemId = new()
    {
        [322] = "Fence1",
        [323] = "Fence2",
        [324] = "Fence3",
        [298] = "Fence5",
    };

    /// <summary>Connectivity SUM (see class remarks) -> tile index in the 3-column sheet -
    /// verbatim copy of Fence.cs's populateFenceDrawGuide().</summary>
    private static readonly Dictionary<int, int> DrawGuide = new()
    {
        [0] = 5,
        [10] = 9,
        [100] = 10,
        [1000] = 3,
        [500] = 5,
        [1010] = 8,
        [1100] = 6,
        [1500] = 3,
        [600] = 0,
        [510] = 2,
        [110] = 7,
        [1600] = 0,
        [1610] = 4,
        [1510] = 2,
        [1110] = 7,
        [610] = 4,
    };

    public static bool IsFenceItemId(int parentSheetIndex) => TextureFileByItemId.ContainsKey(parentSheetIndex);

    public static bool TryGetBitmap(string contentFolder, int parentSheetIndex, out Bitmap bitmap)
    {
        bitmap = null!;
        if (!TextureFileByItemId.TryGetValue(parentSheetIndex, out var fileName))
            return false;

        var key = contentFolder + "|" + fileName;
        if (!Cache.TryGetValue(key, out var cached))
        {
            var path = Path.Combine(contentFolder, "LooseSprites", fileName + ".png");
            if (!File.Exists(path))
                return false;

            cached = new Bitmap(path);
            Cache[key] = cached;
        }

        bitmap = cached;
        return true;
    }

    /// <summary>The source rect for a given connectivity sum (see class remarks) - falls back to
    /// the isolated-tile index (5) for any sum the real game's own table doesn't cover (can't
    /// happen for the 4-neighbor same-material sums this class computes, but matches this
    /// codebase's established defensive-default convention elsewhere).</summary>
    public static Rect SourceFor(int connectivitySum)
    {
        const int pieceWidth = 16, pieceHeight = 32, sheetWidth = 48;
        var index = DrawGuide.TryGetValue(connectivitySum, out var i) ? i : 5;
        var x = index * pieceWidth % sheetWidth;
        var y = index * pieceWidth / sheetWidth * pieceHeight;
        return new Rect(x, y, pieceWidth, pieceHeight);
    }
}
