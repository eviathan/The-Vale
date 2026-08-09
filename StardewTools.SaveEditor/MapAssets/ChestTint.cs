using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Recolors a placed Chest's sprite per its real PlayerChoiceColor - decompiled Chest.draw() (the
/// branch gated on QualifiedItemId being one of the 4 real colorable chests: Chest/130, Stone
/// Chest/232, Big Chest/304, Big Stone Chest/328) draws TWO layers, not one: a "body" cell tinted
/// by a straight `playerChoiceColor * alpha` multiply (not an HSL shift like BuildingPainter - a
/// chest's stored color IS the exact target RGB), plus a separate "lid" cell drawn plain white
/// (untinted) on top of it. Critically, the body cell ISN'T the chest's own ParentSheetIndex for
/// Chest/Big Chest (it's a fixed override - 168 and 312 respectively, confirmed via decompiled
/// Chest.draw()'s own hardcoded switch) - using ParentSheetIndex directly (this class's first,
/// wrong version) drew a real but unrelated cell from the sheet, which is why a tinted chest still
/// looked wrong even after tinting was added. Only Stone Chest/Big Stone Chest actually use their
/// own ParentSheetIndex as the body cell (the switch's default case). The lid cell is always
/// ParentSheetIndex + 1 (the real currentLidFrame for a freshly-placed, closed chest - matches
/// ChestXmlBuilder's own convention) plus a small fixed offset that also differs per type.
/// Chest.draw()'s remaining two layers (a small clasp/latch decoration, and a lighter double-alpha
/// re-tint of a "coloredLidRect" highlight) aren't reproduced - body + lid is a close approximation
/// appropriate for a small map-preview icon, not the animated in-game chest.
/// </summary>
internal static class ChestTint
{
    private static readonly Dictionary<string, Bitmap?> Cache = new();

    /// <summary>(BodyIndex, LidIndex) for the real colorable chest types - null for anything else
    /// (Junimo Chest/Mini-Fridge/Mini-Shipping Bin/Hopper aren't recolorable via playerChoiceColor
    /// at all in the real game; they use a separate `tint` field instead, per decompiled
    /// Chest.draw()'s own `(bool)playerChest` fallback branch).</summary>
    public static (int BodyIndex, int LidIndex)? FrameFor(int parentSheetIndex)
    {
        var currentLidFrame = parentSheetIndex + 1;
        return parentSheetIndex switch
        {
            130 => (168, currentLidFrame + 46),        // Chest
            232 => (parentSheetIndex, currentLidFrame + 8),  // Stone Chest
            304 => (312, currentLidFrame + 16),        // Big Chest
            328 => (parentSheetIndex, currentLidFrame + 8),  // Big Stone Chest
            _ => null,
        };
    }

    public static bool TryGetCellBitmap(string contentFolder, int cellIndex, Bitmap sheetBitmap, Rect cellSource,
        (byte R, byte G, byte B, byte A)? tintColor, out Bitmap bitmap)
    {
        bitmap = null!;

        var key = string.Join('|', contentFolder, cellIndex, tintColor?.R, tintColor?.G, tintColor?.B, tintColor?.A);
        if (Cache.TryGetValue(key, out var cached))
        {
            if (cached is null)
                return false;
            bitmap = cached;
            return true;
        }

        var size = new PixelSize((int)cellSource.Width, (int)cellSource.Height);
        var pixels = ReadPixels(sheetBitmap, cellSource, size);

        if (tintColor is { } color)
        {
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = (byte)(pixels[i] * color.R / 255);
                pixels[i + 1] = (byte)(pixels[i + 1] * color.G / 255);
                pixels[i + 2] = (byte)(pixels[i + 2] * color.B / 255);
            }
        }

        var writeable = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using (var fb = writeable.Lock())
        {
            for (var y = 0; y < size.Height; y++)
                Marshal.Copy(pixels, y * size.Width * 4, fb.Address + y * fb.RowBytes, size.Width * 4);
        }

        Cache[key] = writeable;
        bitmap = writeable;
        return true;
    }

    private static byte[] ReadPixels(Bitmap bitmap, Rect sourceRect, PixelSize size)
    {
        var stride = size.Width * 4;
        var buffer = Marshal.AllocHGlobal(stride * size.Height);
        try
        {
            bitmap.CopyPixels(new PixelRect((int)sourceRect.X, (int)sourceRect.Y, size.Width, size.Height), buffer, stride * size.Height, stride);
            var bytes = new byte[stride * size.Height];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
