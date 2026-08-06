using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Real sprites for placed farm buildings (barn, coop, silo, etc.) - one full image per
/// building type under Buildings/{BuildingType}.png, not a shared spritesheet like objects or
/// trees. Recolorable buildings (cabins) also have a "..._PaintMask" tint overlay in the same
/// folder that isn't applied here - the player's actual paint choice isn't tracked on
/// BuildingEditor yet, so the base skin's colors are used regardless.
/// </summary>
public static class BuildingSprites
{
    private static readonly Dictionary<string, Bitmap?> Cache = new();

    public static bool TryGetSprite(string contentFolder, string buildingType, out Bitmap bitmap, out Rect source)
    {
        if (!TryGetBitmap(contentFolder, buildingType, out bitmap))
        {
            source = default;
            return false;
        }

        source = new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        return true;
    }

    /// <summary>The raw, uncropped sheet for a building type. Most buildings are one plain
    /// image and TryGetSprite (the whole bitmap as its own source rect) is all they need, but a
    /// few - Fish Pond confirmed so far - pack multiple composited layers into one sheet (see
    /// FarmMapControl.TryDrawBuildingSprite's Fish Pond special case) and need the bitmap
    /// without an assumed single source rect.</summary>
    public static bool TryGetBitmap(string contentFolder, string buildingType, out Bitmap bitmap)
    {
        var key = contentFolder + "|" + buildingType;
        if (!Cache.TryGetValue(key, out var cached))
        {
            var path = Path.Combine(contentFolder, "Buildings", buildingType + ".png");
            cached = File.Exists(path) ? new Bitmap(path) : null;
            Cache[key] = cached;
        }

        bitmap = cached!;
        return cached is not null;
    }
}
