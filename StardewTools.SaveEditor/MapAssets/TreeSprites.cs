using System.Collections.Generic;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Resolves a tree's (species, season) to its real sprite sheet and the source rect for its
/// fully-grown form. Confirmed by visually inspecting the actual extracted tree1_spring.png:
/// two ~48px-wide adult tree variants side by side at the top of the sheet, a sapling icon
/// and two stumps below. Growth-stage (sapling/bush) sprites aren't covered yet - only
/// treeType 1/2/3 map directly to tree1/2/3_{season}.png; exotic types (palm, mystic,
/// mushroom) use differently-named files we haven't mapped, so those still fall back to
/// the marker.
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
        bitmap = null!;
        source = default;

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
        // Two adult variants side by side, ~48px wide, ~80px tall, at the top of the sheet.
        var col = variant % 2;
        source = new Rect(col * 48, 0, 48, 80);
        return true;
    }
}
