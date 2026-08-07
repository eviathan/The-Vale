using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// A planted crop's real sprite (TileSheets/crops.png, confirmed 256x1024px on disk - matches
/// the game's own `Math.Min(240, ...)` clamp on the X coordinate). Source-rect formula is
/// Crop.getSourceRect(number) (decompiled Crop.cs) reproduced verbatim, minus two scoped-out
/// edge cases: the indexOfHarvest=="771" seasonal row-shift (wild seed packets only) and the
/// Location.IsGreenhouse override in DrawnCropTexture (a custom-texture path, vanishingly rare
/// in Data/Crops.json). `number` is deterministic, derived from tile position exactly like the
/// real game (`getSourceRect((int)tileLocation.X * 7 + (int)tileLocation.Y * 11)`), not random -
/// so a given crop at a given tile always renders the same frame, matching real gameplay.
/// </summary>
public static class CropSprites
{
    private static Bitmap? _cached;
    private static string? _cachedFolder;

    public static bool TryGetSprite(string contentFolder, int rowInSpriteSheet, int currentPhase, bool dead, bool fullGrown, int dayOfCurrentPhase, int tileX, int tileY, out Bitmap bitmap, out Rect source)
    {
        bitmap = null!;
        source = default;

        if (!TryGetBitmap(contentFolder, out bitmap))
            return false;

        var number = tileX * 7 + tileY * 11;

        if (dead)
        {
            source = new Rect(192 + number % 4 * 16, 384, 16, 32);
            return true;
        }

        var column = fullGrown
            ? (dayOfCurrentPhase <= 0 ? 6 : 7)
            : currentPhase + (currentPhase == 0 && number % 2 == 0 ? -1 : 0) + 1;
        var oddRowOffset = rowInSpriteSheet % 2 != 0 ? 128 : 0;
        var x = Math.Min(240, column * 16 + oddRowOffset);
        var y = rowInSpriteSheet / 2 * 32;
        source = new Rect(x, y, 16, 32);
        return true;
    }

    private static bool TryGetBitmap(string contentFolder, out Bitmap bitmap)
    {
        bitmap = null!;

        if (_cachedFolder != contentFolder)
        {
            var path = Path.Combine(contentFolder, "TileSheets", "crops.png");
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
