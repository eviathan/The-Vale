using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Tilled soil's real sprite (TerrainFeatures/hoeDirt.png, confirmed 192x64px = 4 columns x 4
/// rows of 16px cells, on disk). The real game picks a source rect via a 4-neighbor connectivity
/// bitmask (HoeDirt.cs's `drawGuide` dictionary, keyed 0-15, N=1/E=2/S=4/W=8 - `Cardinals`/`N`/
/// `E`/`S`/`W` constants) so tilled patches join into a seamless field - now modeled (see
/// NeighborOffsets/DrawGuide below, ported verbatim from HoeDirt.cs's populateDrawGuide()).
/// Watered/paddy still draws a flat overlay regardless of neighbors (HoeDirt.cs's own
/// `wateredRectPosition` neighbor-joining additionally requires the neighbor's own
/// paddyWaterCheck() - proximity to a water tile - to match, which pulls in a big, mostly-
/// orthogonal proximity-to-water subsystem for a purely cosmetic overlay-shape nuance; scoped
/// out, same "acknowledge the honest gap" approach as everywhere else in this codebase - a flat
/// non-joined watered overlay on top of correctly-joined dry dirt is a reasonable approximation).
/// Dry base at (0,0,16,16) for drawGuide[0]; watered overlay at (64,0,16,16) (or (128,0,16,16)
/// for a paddy tile) - both confirmed against HoeDirt.cs's DrawOptimized, which draws them as two
/// separate layered Draw calls, not one combined frame.
/// </summary>
public static class HoeDirtSprites
{
    private static Bitmap? _cached;
    private static string? _cachedFolder;

    public static readonly Rect DrySource = new(0, 0, 16, 16);
    public static readonly Rect WateredOverlaySource = new(64, 0, 16, 16);
    public static readonly Rect PaddyOverlaySource = new(128, 0, 16, 16);

    /// <summary>(dx, dy, bit) for the 4 cardinal neighbors - N=1/E=2/S=4/W=8, verbatim from
    /// HoeDirt.cs's own N/E/S/W constants and _offsets array.</summary>
    public static readonly (int Dx, int Dy, byte Bit)[] NeighborOffsets =
    {
        (0, -1, 1), // N
        (1, 0, 2),  // E
        (0, 1, 4),  // S
        (-1, 0, 8), // W
    };

    /// <summary>bitmask (0-15) -> tile index in the 4-column sprite sheet - verbatim copy of
    /// HoeDirt.cs's populateDrawGuide().</summary>
    public static readonly IReadOnlyDictionary<byte, int> DrawGuide = new Dictionary<byte, int>
    {
        [0] = 0,
        [8] = 15,
        [2] = 13,
        [1] = 12,
        [4] = 4,
        [9] = 11,
        [3] = 9,
        [5] = 8,
        [6] = 1,
        [12] = 3,
        [10] = 14,
        [7] = 5,
        [15] = 6,
        [13] = 7,
        [11] = 10,
        [14] = 2,
    };

    /// <summary>The dry-tile source rect for a given 4-neighbor connectivity bitmask (see
    /// NeighborOffsets/DrawGuide).</summary>
    public static Rect SourceFor(byte neighborMask)
    {
        var tileIndex = DrawGuide[neighborMask];
        return new Rect(tileIndex % 4 * 16, tileIndex / 4 * 16, 16, 16);
    }

    public static bool TryGetBitmap(string contentFolder, out Bitmap bitmap)
    {
        bitmap = null!;

        if (_cachedFolder != contentFolder)
        {
            var path = Path.Combine(contentFolder, "TerrainFeatures", "hoeDirt.png");
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
}
