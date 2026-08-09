using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

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

    private static readonly Dictionary<string, WriteableBitmap> TintCache = new();

    /// <summary>Real flowers (see CropEditor.ProgramColored remarks) draw a SECOND, colored copy
    /// of the sprite on top of the base one once mature - from a different column of the SAME
    /// row (decompiled Crop.draw()'s coloredSourceRect: column = currentPhase + 1 + 1, no
    /// odd-tile/phase-0 adjustment, unlike the base sprite's column formula above), tinted by a
    /// straight per-channel multiply (SpriteBatch.Draw's own color-tint semantics - the sprite's
    /// own alpha is preserved, not the tint's, since crops.png tiles are already fully opaque/
    /// transparent per pixel and every real TintColors entry sampled had alpha 255 anyway).
    /// Avalonia's DrawingContext has no per-draw tint parameter (unlike SpriteBatch.Draw), so -
    /// same rationale/technique as BuildingPainter.cs - this does the multiply once on the CPU
    /// into a small cached bitmap sized to just this one frame, not the whole sheet.</summary>
    public static bool TryGetTintedOverlay(string contentFolder, int rowInSpriteSheet, int currentPhase,
        byte tintR, byte tintG, byte tintB, out Bitmap tinted, out Rect source)
    {
        tinted = null!;
        source = default;

        if (!TryGetBitmap(contentFolder, out var sheet))
            return false;

        var column = currentPhase + 1 + 1;
        var oddRowOffset = rowInSpriteSheet % 2 != 0 ? 128 : 0;
        var x = Math.Min(240, column * 16 + oddRowOffset);
        var y = rowInSpriteSheet / 2 * 32;
        // The RETURNED bitmap is a small standalone crop of just this one frame (not the whole
        // sheet) - source must be in ITS local coordinates (0,0,16,32), not the sheet's (x,y) the
        // pixels were extracted FROM, or DrawImage samples out of bounds on a 16x32 bitmap and
        // silently renders nothing.
        source = new Rect(0, 0, 16, 32);

        var key = $"{contentFolder}|{x}|{y}|{tintR}|{tintG}|{tintB}";
        if (TintCache.TryGetValue(key, out var cached))
        {
            tinted = cached;
            return true;
        }

        const int w = 16, h = 32;
        var stride = w * 4;
        var buffer = Marshal.AllocHGlobal(stride * h);
        byte[] pixels;
        try
        {
            sheet.CopyPixels(new PixelRect(x, y, w, h), buffer, stride * h, stride);
            pixels = new byte[stride * h];
            Marshal.Copy(buffer, pixels, 0, pixels.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        for (var i = 0; i < pixels.Length; i += 4)
        {
            // RGBA byte order (same convention BuildingPainter.cs's own read/shift/write
            // round-trip already relies on - confirmed there via a real paint mask's red channel
            // landing at offset 0).
            pixels[i] = (byte)(pixels[i] * tintR / 255);
            pixels[i + 1] = (byte)(pixels[i + 1] * tintG / 255);
            pixels[i + 2] = (byte)(pixels[i + 2] * tintB / 255);
        }

        var writeable = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using (var fb = writeable.Lock())
        {
            for (var row = 0; row < h; row++)
                Marshal.Copy(pixels, row * stride, fb.Address + row * fb.RowBytes, stride);
        }

        TintCache[key] = writeable;
        tinted = writeable;
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
