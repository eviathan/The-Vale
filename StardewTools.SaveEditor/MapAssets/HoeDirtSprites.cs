using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Tilled soil's real sprite (TerrainFeatures/hoeDirt.png, confirmed 192x64px = 4 columns x 4
/// rows of 16px cells, on disk). The real game picks a source rect via a 4-neighbor connectivity
/// bitmask (HoeDirt.cs's `drawGuide` dictionary, keyed 0-15) so tilled patches join into a
/// seamless field - not modeled here, scoped out same as the original plan called for. This
/// always uses `drawGuide[0] = 0` - the REAL, verified value for "no tilled neighbors on any
/// side", i.e. an isolated tile - which is exactly correct for a single hand-placed tile and a
/// reasonable, honestly-scoped approximation (no visible edge-joining) for a tilled patch with
/// neighbors. Dry base at (0,0,16,16); watered draws an overlay on top at (64,0,16,16) (or
/// (128,0,16,16) for a paddy tile) - both confirmed against HoeDirt.cs's DrawOptimized, which
/// draws them as two separate layered Draw calls, not one combined frame.
/// </summary>
public static class HoeDirtSprites
{
    private static Bitmap? _cached;
    private static string? _cachedFolder;

    public static readonly Rect DrySource = new(0, 0, 16, 16);
    public static readonly Rect WateredOverlaySource = new(64, 0, 16, 16);
    public static readonly Rect PaddyOverlaySource = new(128, 0, 16, 16);

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
