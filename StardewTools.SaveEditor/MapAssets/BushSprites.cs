using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Real bush sprites from TileSheets/bushes.png - source rect math transcribed directly from
/// the decompiled Bush.setUpSourceRect() (StardewValley.TerrainFeatures/Bush.cs), not guessed.
/// Season is only meaningful for size 0 (small) and, indirectly via getAge(), size 3 (tea bush);
/// other sizes are season-independent (medium/large only vary by townBush + season for their
/// spring/summer/fall/winter row, tea bush's age-based growth stage is handled by the caller
/// passing an already-resolved ageInDays; walnut bush only varies by tileSheetOffset).
/// </summary>
public static class BushSprites
{
    private static Bitmap? _cached;
    private static string? _cachedFolder;

    public static bool TryGetBitmap(string contentFolder, out Bitmap bitmap)
    {
        if (_cachedFolder != contentFolder)
        {
            var path = Path.Combine(contentFolder, "TileSheets", "bushes.png");
            _cached = File.Exists(path) ? new Bitmap(path) : null;
            _cachedFolder = contentFolder;
        }

        bitmap = _cached!;
        return _cached is not null;
    }

    /// <summary>0=small, 1=medium, 2=large, 3=tea bush, 4=walnut bush - matches Bush.cs's own
    /// size constants. season: spring/summer/fall/winter (Bush.setUpSourceRect uses Game1's
    /// Season enum in that order: 0=spring, 1=summer, 2=fall, 3=winter). ageInDays only matters
    /// for size 3 (tea bush) - pass Bush.getAge() (Game1.stats.DaysPlayed - datePlanted), 0 for
    /// anything else.</summary>
    public static bool TryGetSprite(string contentFolder, int size, string season, int tileSheetOffset, bool townBush, int ageInDays, out Bitmap bitmap, out Rect source)
    {
        if (!TryGetBitmap(contentFolder, out bitmap))
        {
            source = default;
            return false;
        }

        var seasonIndex = season.ToLowerInvariant() switch
        {
            "summer" => 1,
            "fall" => 2,
            "winter" => 3,
            _ => 0, // spring
        };

        source = size switch
        {
            0 => new Rect(seasonIndex * 32 + tileSheetOffset * 16, 224, 16, 32),
            1 when townBush => new Rect(seasonIndex * 32, 96, 32, 32),
            1 => MediumBushRect(bitmap, seasonIndex, tileSheetOffset),
            2 when townBush && seasonIndex is 0 or 1 => new Rect(48, 176, 48, 48),
            2 => seasonIndex switch
            {
                0 or 1 => new Rect(0, 128, 48, 48), // spring/summer
                2 => new Rect(48, 128, 48, 48), // fall
                _ => new Rect(0, 176, 48, 48), // winter
            },
            3 => TeaBushRect(seasonIndex, tileSheetOffset, ageInDays),
            4 => new Rect(tileSheetOffset * 32, 320, 32, 32),
            _ => default,
        };

        return true;
    }

    private static Rect MediumBushRect(Bitmap bitmap, int seasonIndex, int tileSheetOffset)
    {
        var xOffset = seasonIndex * 64 + tileSheetOffset * 32;
        var textureWidth = (int)bitmap.PixelSize.Width;
        return new Rect(xOffset % textureWidth, xOffset / textureWidth * 48, 32, 48);
    }

    private static Rect TeaBushRect(int seasonIndex, int tileSheetOffset, int ageInDays)
    {
        var ageColumn = Math.Min(2, ageInDays / 10) * 16 + tileSheetOffset * 16;
        return seasonIndex switch
        {
            0 => new Rect(ageColumn, 256, 16, 32), // spring
            1 => new Rect(64 + ageColumn, 256, 16, 32), // summer
            2 => new Rect(ageColumn, 288, 16, 32), // fall
            _ => new Rect(64 + ageColumn, 288, 16, 32), // winter
        };
    }
}
