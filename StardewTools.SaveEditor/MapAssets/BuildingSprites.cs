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
        var key = contentFolder + "|" + buildingType;
        if (!Cache.TryGetValue(key, out var cached))
        {
            var path = Path.Combine(contentFolder, "Buildings", buildingType + ".png");
            cached = File.Exists(path) ? new Bitmap(path) : null;
            Cache[key] = cached;
        }

        if (cached is null)
        {
            bitmap = null!;
            source = default;
            return false;
        }

        bitmap = cached;
        source = new Rect(0, 0, cached.PixelSize.Width, cached.PixelSize.Height);
        return true;
    }
}
