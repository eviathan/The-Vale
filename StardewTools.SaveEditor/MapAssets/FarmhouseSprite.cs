using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// The farmhouse exterior isn't a placed Building at all - unlike every other farm structure,
/// it's drawn by the game as a fixed overlay from Buildings/houses.png, keyed to the player's
/// house upgrade level, anchored off the map's front-door entry tile. Source rect and offset
/// math ported from the decompiled Farm.draw(): each upgrade level is a 160x144 row (level 3
/// reuses level 2's exterior), and the house's top-left sits 6 tiles left / 6.875 tiles above
/// the entry tile (native units: 16px = 1 tile, matching MapAssetLoader's convention).
/// </summary>
public static class FarmhouseSprite
{
    /// <summary>No map-property parsing yet (TmxMap only reads tileset/layer data) - this
    /// matches the game's own fallback when a map has no FarmHouseEntry property.</summary>
    public static readonly TilePosition DefaultEntryTile = new(64, 15);

    private const int SourceWidth = 160;
    private const int SourceHeight = 144;

    private static Bitmap? _cached;
    private static string? _cachedFolder;

    public static bool TryGetSprite(string contentFolder, int houseUpgradeLevel, out Bitmap bitmap, out Rect source)
    {
        if (_cachedFolder != contentFolder)
        {
            var path = Path.Combine(contentFolder, "Buildings", "houses.png");
            _cached = File.Exists(path) ? new Bitmap(path) : null;
            _cachedFolder = contentFolder;
        }

        if (_cached is null)
        {
            bitmap = null!;
            source = default;
            return false;
        }

        var row = houseUpgradeLevel == 3 ? 2 : houseUpgradeLevel;
        bitmap = _cached;
        source = new Rect(0, row * SourceHeight, SourceWidth, SourceHeight);
        return true;
    }

    /// <summary>Top-left tile of the house footprint (10x9 tiles) given its front-door entry tile.</summary>
    public static (double X, double Y) TopLeftTile(TilePosition entryTile) => (entryTile.X - 6, entryTile.Y - 6.875);
}
