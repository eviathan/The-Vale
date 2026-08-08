using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Recolors a building's sprite sheet per its real BuildingPaintColor field - ported directly
/// from the decompiled StardewValley.BuildingPainter/Utility.RGBtoHSL/HSLtoRGB, not
/// approximated: a real per-building mask file (Buildings/{Type}_PaintMask.png - confirmed on
/// disk for Beach/Log/Neighbor/Plank/Rustic/Stone/Trailer Cabin, Big Shed, Deluxe Barn, Deluxe
/// Coop, Stable) marks three paintable regions in pure red/lime/blue pixels (confirmed by direct
/// pixel inspection of Stable_PaintMask.png: exactly (255,0,0,255)/(0,255,0,255)/(0,0,255,255),
/// nothing in between). For each non-default color slot, the real game first resets those pixels
/// toward grayscale (a -100 saturation shift) and THEN applies the real hue/saturation/lightness
/// shift on top of that flattened baseline - not a shift directly from the original pixel -
/// replicated exactly here (BuildingPainter.Apply's own two-call `_ApplyPaint` sequence), not
/// simplified away.
/// Avalonia has no SpriteBatch-style GPU color-multiply, so this does the same thing the real
/// game does: full CPU-side pixel read/modify/write into a new bitmap, cached per (building
/// type + all 12 paint values) since it's real per-pixel work, not something to redo every frame.
/// </summary>
internal static class BuildingPainter
{
    private static readonly Dictionary<string, Bitmap?> Cache = new();

    public static bool TryGetPaintedBitmap(string contentFolder, string buildingType, Bitmap baseBitmap, BuildingPaintColorEditor paint, out Bitmap painted)
    {
        painted = null!;

        if (paint.Color1Default && paint.Color2Default && paint.Color3Default)
            return false;

        var key = string.Join('|', buildingType,
            paint.Color1Default, paint.Color1Hue, paint.Color1Saturation, paint.Color1Lightness,
            paint.Color2Default, paint.Color2Hue, paint.Color2Saturation, paint.Color2Lightness,
            paint.Color3Default, paint.Color3Hue, paint.Color3Saturation, paint.Color3Lightness);

        if (Cache.TryGetValue(key, out var cached))
        {
            if (cached is null)
                return false;
            painted = cached;
            return true;
        }

        var maskPath = Path.Combine(contentFolder, "Buildings", buildingType + "_PaintMask.png");
        if (!File.Exists(maskPath))
        {
            Cache[key] = null;
            return false;
        }

        var maskBitmap = new Bitmap(maskPath);
        var size = baseBitmap.PixelSize;
        if (maskBitmap.PixelSize != size)
        {
            Cache[key] = null;
            return false;
        }

        var basePixels = ReadPixels(baseBitmap, size);
        var maskPixels = ReadPixels(maskBitmap, size);

        var indices1 = new List<int>();
        var indices2 = new List<int>();
        var indices3 = new List<int>();
        var pixelCount = size.Width * size.Height;
        for (var i = 0; i < pixelCount; i++)
        {
            var o = i * 4;
            if (maskPixels[o + 3] != 255)
                continue;

            if (maskPixels[o] == 255 && maskPixels[o + 1] == 0 && maskPixels[o + 2] == 0)
                indices1.Add(i);
            else if (maskPixels[o] == 0 && maskPixels[o + 1] == 255 && maskPixels[o + 2] == 0)
                indices2.Add(i);
            else if (maskPixels[o] == 0 && maskPixels[o + 1] == 0 && maskPixels[o + 2] == 255)
                indices3.Add(i);
        }

        if (!paint.Color1Default)
        {
            ApplyPaint(0, -100, 0, basePixels, indices1);
            ApplyPaint(paint.Color1Hue, paint.Color1Saturation, paint.Color1Lightness, basePixels, indices1);
        }
        if (!paint.Color2Default)
        {
            ApplyPaint(0, -100, 0, basePixels, indices2);
            ApplyPaint(paint.Color2Hue, paint.Color2Saturation, paint.Color2Lightness, basePixels, indices2);
        }
        if (!paint.Color3Default)
        {
            ApplyPaint(0, -100, 0, basePixels, indices3);
            ApplyPaint(paint.Color3Hue, paint.Color3Saturation, paint.Color3Lightness, basePixels, indices3);
        }

        var writeable = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using (var fb = writeable.Lock())
        {
            for (var y = 0; y < size.Height; y++)
                Marshal.Copy(basePixels, y * size.Width * 4, fb.Address + y * fb.RowBytes, size.Width * 4);
        }

        Cache[key] = writeable;
        painted = writeable;
        return true;
    }

    private static byte[] ReadPixels(Bitmap bitmap, PixelSize size)
    {
        var stride = size.Width * 4;
        var buffer = Marshal.AllocHGlobal(stride * size.Height);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), buffer, stride * size.Height, stride);
            var bytes = new byte[stride * size.Height];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void ApplyPaint(int hueShift, int saturationShift, int lightnessShift, byte[] pixels, List<int> indices)
    {
        foreach (var index in indices)
        {
            var o = index * 4;
            RgbToHsl(pixels[o], pixels[o + 1], pixels[o + 2], out var h, out var s, out var l);
            h += hueShift;
            s += saturationShift / 100.0;
            l += lightnessShift / 100.0;
            h %= 360.0;
            if (h < 0)
                h += 360.0;
            s = Math.Clamp(s, 0.0, 1.0);
            l = Math.Clamp(l, 0.0, 1.0);
            HslToRgb(h, s, l, out var r, out var g, out var b);
            pixels[o] = r;
            pixels[o + 1] = g;
            pixels[o + 2] = b;
        }
    }

    /// <summary>Ported verbatim from the decompiled StardewValley.Utility.RGBtoHSL.</summary>
    private static void RgbToHsl(int r, int g, int b, out double h, out double s, out double l)
    {
        var dr = r / 255.0;
        var dg = g / 255.0;
        var db = b / 255.0;
        var max = Math.Max(dr, Math.Max(dg, db));
        var min = Math.Min(dr, Math.Min(dg, db));
        var diff = max - min;
        l = (max + min) / 2.0;
        if (Math.Abs(diff) < 1e-05)
        {
            s = 0.0;
            h = 0.0;
            return;
        }

        s = l <= 0.5 ? diff / (max + min) : diff / (2.0 - max - min);
        var rDist = (max - dr) / diff;
        var gDist = (max - dg) / diff;
        var bDist = (max - db) / diff;
        if (dr == max)
            h = bDist - gDist;
        else if (dg == max)
            h = 2.0 + rDist - bDist;
        else
            h = 4.0 + gDist - rDist;

        h *= 60.0;
        if (h < 0.0)
            h += 360.0;
    }

    /// <summary>Ported verbatim from the decompiled StardewValley.Utility.HSLtoRGB.</summary>
    private static void HslToRgb(double h, double s, double l, out byte r, out byte g, out byte b)
    {
        var p2 = l <= 0.5 ? l * (1.0 + s) : l + s - l * s;
        var p1 = 2.0 * l - p2;
        double dr, dg, db;
        if (s == 0.0)
        {
            dr = l;
            dg = l;
            db = l;
        }
        else
        {
            dr = QqhToRgb(p1, p2, h + 120.0);
            dg = QqhToRgb(p1, p2, h);
            db = QqhToRgb(p1, p2, h - 120.0);
        }

        r = (byte)(dr * 255.0);
        g = (byte)(dg * 255.0);
        b = (byte)(db * 255.0);
    }

    private static double QqhToRgb(double q1, double q2, double hue)
    {
        if (hue > 360.0)
            hue -= 360.0;
        else if (hue < 0.0)
            hue += 360.0;

        if (hue < 60.0)
            return q1 + (q2 - q1) * hue / 60.0;
        if (hue < 180.0)
            return q2;
        if (hue < 240.0)
            return q1 + (q2 - q1) * (240.0 - hue) / 60.0;
        return q1;
    }
}
