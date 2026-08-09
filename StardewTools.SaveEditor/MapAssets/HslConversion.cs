using System;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Ported verbatim from the decompiled StardewValley.Utility.RGBtoHSL/HSLtoRGB - shared
/// by BuildingPainter (recoloring a building's real sprite sheet) and the Building Paint color
/// picker UI (converting between a real Hue/Saturation/Lightness paint slot and the RGB color an
/// Avalonia ColorPicker deals in).</summary>
public static class HslConversion
{
    public static void RgbToHsl(int r, int g, int b, out double h, out double s, out double l)
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

    public static void HslToRgb(double h, double s, double l, out byte r, out byte g, out byte b)
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
