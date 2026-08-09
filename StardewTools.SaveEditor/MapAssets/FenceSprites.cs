using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// A placed Fence's real sprite - each material has its own texture file under LooseSprites
/// (confirmed via Data/Fences.json's own Texture field: Wood/322="Fence1", Stone/323="Fence2",
/// Iron/324="Fence3", Hardwood/298="Fence5", Gate/325="Fence1" (same sheet as Wood) - note the
/// real game skips "Fence4"), each a 48x352px sheet = 3 columns x 11 rows of 16x32px cells
/// (confirmed on disk). The real game picks a source cell via a 4-neighbor connectivity SUM (not
/// a bitmask - Fence.cs's own getDrawSum(): Left=10, Right=100, Down=500, Up=1000) - a neighbor
/// counts per Fence.countsForDrawing(): same material AND not itself a gate, OR the fence whose
/// sum is being computed is itself a gate (a gate connects to any adjacent non-gate fence
/// material; a gate is never counted as a neighbor of a REGULAR fence, so gates never join into a
/// neighboring post's own connectivity). Gates (item id 325) additionally draw from different,
/// non-uniform pixel rects per connectivity case (Fence.draw()'s own isGate switch) instead of the
/// uniform 16x32-cell grid regular fences use - GateSourceFor below ports that switch verbatim.
/// </summary>
public static class FenceSprites
{
    private static readonly Dictionary<string, Bitmap> Cache = new();

    public const int GateItemId = 325;

    private static readonly Dictionary<int, string> TextureFileByItemId = new()
    {
        [322] = "Fence1",
        [323] = "Fence2",
        [324] = "Fence3",
        [298] = "Fence5",
        [325] = "Fence1",
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
        var index = DrawGuide.TryGetValue(connectivitySum, out var i) ? i : 5;
        return SourceForIndex(index);
    }

    /// <summary>The uniform 16x32-cell rect for a raw sheet index, same math SourceFor uses -
    /// exposed separately for the gate solo-post case (real cell index 17, not reachable through
    /// DrawGuide's connectivity-sum lookup since that table is keyed by sum, not index).</summary>
    public static Rect SourceForIndex(int index)
    {
        const int pieceWidth = 16, pieceHeight = 32, sheetWidth = 48;
        var x = index * pieceWidth % sheetWidth;
        var y = index * pieceWidth / sheetWidth * pieceHeight;
        return new Rect(x, y, pieceWidth, pieceHeight);
    }

    /// <summary>The real cell index for an isolated (no real neighbors) gate - Fence.draw()'s own
    /// `sourceRectPosition = 17` fallback when none of the special isGate switch cases match.</summary>
    public const int SoloGateIndex = 17;

    /// <summary>A gate's own non-uniform draw geometry for a connectivity case (Fence.draw()'s
    /// isGate switch, ported verbatim) - unlike the uniform 16x32 grid regular fences use, a gate's
    /// cell size/position varies per case, and open/closed state (gatePosition == 88) picks between
    /// two columns within each cell. Returns null for any sum the switch doesn't special-case (0,
    /// or an isolated gate) - callers should fall back to SourceForIndex(SoloGateIndex) then.
    /// OffsetTilesX/Y are relative to the gate's own tile origin, in whole tile units - converted
    /// from the real game's fixed-4x-scale screen-pixel offsets (1 tile = 64 dest px = the same
    /// unit "pixelOffset + tile * scale" callers already work in, so just add OffsetTiles * scale).</summary>
    public static IReadOnlyList<GateDraw>? GateDrawsFor(int connectivitySum, bool isOpen)
    {
        var openX24 = isOpen ? 24 : 0;
        var openX16 = isOpen ? 16 : 0;
        return connectivitySum switch
        {
            10 => new[] { new GateDraw(new Rect(openX24, 192, 24, 48), -0.25, -2.0) },
            100 => new[] { new GateDraw(new Rect(openX24, 240, 24, 48), -0.25, -2.0) },
            1000 => new[] { new GateDraw(new Rect(openX24, 288, 24, 32), 0.3125, -1.3125) },
            500 => new[] { new GateDraw(new Rect(openX24, 320, 24, 32), 0.3125, -1.3125) },
            110 => new[] { new GateDraw(new Rect(openX24, 128, 24, 32), -0.25, -1.0) },
            1500 => new[]
            {
                new GateDraw(new Rect(openX16, 160, 16, 16), 0.3125, -1.3125),
                new GateDraw(new Rect(openX16, 176, 16, 16), 0.3125, -0.3125),
            },
            _ => null,
        };
    }
}

/// <summary>One draw call of a gate's special (non-uniform) sprite geometry - see
/// FenceSprites.GateDrawsFor.</summary>
public readonly record struct GateDraw(Rect Source, double OffsetTilesX, double OffsetTilesY);
