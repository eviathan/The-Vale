using System.Collections.Generic;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Resolves a tree's (species, season) to its real sprite sheet and the source rect for its
/// fully-grown form. Verified against the actual extracted tree1_spring.png pixel-by-pixel
/// (ImageMagick -trim per column): the sheet is 144x160; the two adult tree variants are each
/// 48x80 starting at Y=4, but they sit at X=0 and X=96 - NOT adjacent at X=0/X=48 - with a
/// 48px fully-transparent gap between them (X=48-96, unrelated content lives below in Y).
/// Sampling X=variant*48 (the original, unverified assumption) put every odd-variant tree in
/// that empty gap, rendering nothing for roughly half of all trees. Growth-stage (sapling/
/// bush) sprites aren't covered yet - only treeType 1/2/3 map directly to tree1/2/3_{season}.png;
/// exotic types (palm, mystic, mushroom) use differently-named files we haven't mapped, so
/// those still fall back to the marker.
/// </summary>
public static class TreeSprites
{
    private static readonly Dictionary<string, Bitmap> Cache = new();

    private static readonly Dictionary<int, string> KnownTypeFiles = new()
    {
        [1] = "tree1",
        [2] = "tree2",
        [3] = "tree3",
    };

    public static bool TryGetAdultSprite(string contentFolder, int treeType, string season, int variant, out Bitmap bitmap, out Rect source)
    {
        if (!TryGetBitmap(contentFolder, treeType, season, out bitmap))
        {
            source = default;
            return false;
        }

        var col = variant % 2;
        source = new Rect(col * 96, 4, 48, 80);
        return true;
    }

    /// <summary>Source rect for a chopped stump - Rectangle(32, 96, 16, 32) in the decompiled
    /// Tree.cs, a single static field shared by every tree type/season (not something that
    /// varies per texture the way the adult sprite's two variants do).</summary>
    public static bool TryGetStumpSprite(string contentFolder, int treeType, string season, out Bitmap bitmap, out Rect source)
    {
        if (!TryGetBitmap(contentFolder, treeType, season, out bitmap))
        {
            source = default;
            return false;
        }

        source = new Rect(32, 96, 16, 32);
        return true;
    }

    private static bool TryGetBitmap(string contentFolder, int treeType, string season, out Bitmap bitmap)
    {
        bitmap = null!;

        if (!KnownTypeFiles.TryGetValue(treeType, out var baseName))
            return false;

        var key = $"{baseName}_{season.ToLowerInvariant()}";
        if (!Cache.TryGetValue(key, out var loaded))
        {
            var path = System.IO.Path.Combine(contentFolder, "TerrainFeatures", key + ".png");
            if (!System.IO.File.Exists(path))
                return false;

            loaded = new Bitmap(path);
            Cache[key] = loaded;
        }

        bitmap = loaded;
        return true;
    }
}
