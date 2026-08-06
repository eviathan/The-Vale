using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>One drawable grass blade within a patch - see GrassSprites.GetTufts.</summary>
public readonly record struct GrassTuft(double OffsetTileX, double OffsetTileY, Rect Source, bool Flip);

/// <summary>
/// Real grass tufts from TerrainFeatures/grass.png - a sheet of 15x20 cells, one row per
/// grassType (season-dependent for type 1, the normal outdoor grass) and three horizontal
/// variants per row. Source rect math mirrors the game's own Grass.draw(): X = variant * 15,
/// Y = an offset selected by grassType (and season, for type 1 only) - see SourceOffsetY.
/// </summary>
public static class GrassSprites
{
    private static Bitmap? _cached;
    private static string? _cachedFolder;

    public static bool TryGetBitmap(string contentFolder, out Bitmap bitmap)
    {
        if (_cachedFolder != contentFolder)
        {
            var path = Path.Combine(contentFolder, "TerrainFeatures", "grass.png");
            _cached = File.Exists(path) ? new Bitmap(path) : null;
            _cachedFolder = contentFolder;
        }

        bitmap = _cached!;
        return _cached is not null;
    }

    /// <summary>
    /// Every tuft this grass patch should draw, scattered within the tile - mirrors the game's
    /// own Grass.draw() (StardewValley.TerrainFeatures/Grass.cs): up to 4 independent tufts
    /// (NumberOfWeeds, a real tracked field - previously read but never used for rendering,
    /// always showing exactly one centered tuft regardless), each a 15x20 cell, positioned in
    /// the same 2x2 quadrant grid the game uses (`i % 2`, `i / 2`), each with its own blade
    /// variant and horizontal flip. The game re-rolls its per-tuft variant/flip/jitter at
    /// runtime rather than persisting them (they aren't save fields), so exact values can't be
    /// replicated here - these use a deterministic hash of (tile position, tuft index) instead,
    /// purely so a given patch renders consistently across frames rather than genuine game RNG.
    /// OffsetTileX/Y are fractions of a tile from its center, in the game's own relative
    /// quadrant arrangement (left/right x top/bottom), not literal game pixel coordinates -
    /// this renderer's tile-to-screen math is architected differently from the game's own.
    /// </summary>
    public static IReadOnlyList<GrassTuft> GetTufts(int grassType, string season, int tileX, int tileY, int numberOfWeeds)
    {
        var count = Math.Clamp(numberOfWeeds, 1, 4);
        var offsetY = SourceOffsetY(grassType, season);
        var tufts = new List<GrassTuft>(count);

        for (var i = 0; i < count; i++)
        {
            var hash = HashTuft(tileX, tileY, i);
            var variant = hash % 3;
            var flip = hash / 3 % 2 == 0;
            var jitterX = (hash / 6 % 5 - 2) * 0.05;
            var jitterY = (hash / 30 % 5 - 2) * 0.05;

            var quadrantX = (i % 2 == 0 ? -0.2 : 0.2) + jitterX;
            var quadrantY = (i / 2 == 0 ? -0.12 : 0.12) + jitterY;

            tufts.Add(new GrassTuft(quadrantX, quadrantY, new Rect(variant * 15, offsetY, 15, 20), flip));
        }

        return tufts;
    }

    private static int HashTuft(int tileX, int tileY, int index)
    {
        unchecked
        {
            var h = tileX * 92821 + tileY * 68917 + index * 12923 + 7;
            return Math.Abs(h);
        }
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
