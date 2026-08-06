using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Real grass tufts from TerrainFeatures/grass.png - a sheet of 15x20 cells, one row per
/// grassType (season-dependent for type 1, the normal outdoor grass) and three horizontal
/// variants per row. Source rect math mirrors the game's own Grass.draw(): X = variant * 15,
/// Y = an offset selected by grassType (and season, for type 1 only) - see SourceOffsetY.
/// The per-tile variant isn't save data (the game re-rolls it at runtime via r.Next(3)), so
/// it's derived deterministically from tile position instead, the same approach
/// FarmMapControl already uses to vary tree sprites.
/// </summary>
public static class GrassSprites
{
    private static Bitmap? _cached;
    private static string? _cachedFolder;

    public static bool TryGetSprite(string contentFolder, int grassType, string season, int tileX, int tileY, out Bitmap bitmap, out Rect source)
    {
        if (_cachedFolder != contentFolder)
        {
            var path = Path.Combine(contentFolder, "TerrainFeatures", "grass.png");
            _cached = File.Exists(path) ? new Bitmap(path) : null;
            _cachedFolder = contentFolder;
        }

        if (_cached is null)
        {
            bitmap = null!;
            source = default;
            return false;
        }

        var variant = (tileX * 3 + tileY * 7) % 3;
        bitmap = _cached;
        source = new Rect(variant * 15, SourceOffsetY(grassType, season), 15, 20);
        return true;
    }

    private static int SourceOffsetY(int grassType, string season) => grassType switch
    {
        1 => season.ToLowerInvariant() switch
        {
            "summer" => 20,
            "fall" => 40,
            "winter" => 80,
            _ => 0, // spring
        },
        2 => 60,  // cave grass
        3 => 80,  // frost grass
        4 => 100, // lava grass
        5 => 120, // cave grass 2
        6 => 140, // cobweb
        _ => 0,
    };
}
