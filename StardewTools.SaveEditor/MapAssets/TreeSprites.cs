using System.Collections.Generic;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Resolves a tree's (species, season) to its real sprite sheet and the source rect for its
/// current growth stage. Verified against the actual extracted tree1_spring.png pixel-by-pixel
/// (ImageMagick -trim per column): the sheet is 144x160; the adult canopy's visible (non-
/// transparent) content is a 48x80 region starting at Y=4, at X=0 for the normal look.
///
/// The X=96 column (verified against the same sheet - not just assumed) is NOT a second
/// cosmetic "variant" the way an earlier version of this file assumed - it's the real game's
/// moss-covered canopy, selected by Tree.hasMoss (confirmed against the decompiled Tree.draw():
/// `if (hasMoss) source_rect.X = 96;`). A tile-position formula previously picked between X=0
/// and X=96 for roughly half of all trees regardless of whether they actually had moss - now
/// gated on the tree's real HasMoss field instead. X=48 (the game's "alternate sprite" column,
/// gated on Data/trees.json's UseAlternateSpriteWhenSeedReady/WhenNotShaken flags combined with
/// HasSeed/WasShakenToday) isn't modeled here - would need a Data/trees.json lookup this tool
/// doesn't do yet, so it's a scoped-out gap, not a guess.
///
/// The real per-tree visual variation is a horizontal mirror (SpriteEffects.FlipHorizontally),
/// driven by Tree.flipped - applied by the caller (FarmMapControl.TryDrawTreeSprite), not here.
///
/// Growth stages 0-4 use entirely different, smaller sprites (verified against Tree.draw()'s
/// own growthStage switch, not the adult canopy at all) - see TryGetGrowthStageSprite.
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

    public static bool TryGetAdultSprite(string contentFolder, int treeType, string season, bool hasMoss, out Bitmap bitmap, out Rect source)
    {
        if (!TryGetBitmap(contentFolder, treeType, season, out bitmap))
        {
            source = default;
            return false;
        }

        source = new Rect(hasMoss ? 96 : 0, 4, 48, 80);
        return true;
    }

    /// <summary>Stages 0/1/2 (seed/sprout/sapling) are a single 16x16 icon; 3 and 4 (both
    /// unnamed past "bush") share one 16x32 sprite - verified against Tree.draw()'s own
    /// `growthStage switch` (Tree.cs), not guessed: 0=(32,128,16,16), 1=(0,128,16,16),
    /// 2=(16,128,16,16), 3/4=(0,96,16,32). Only valid for growthStage 0-4 - callers should use
    /// TryGetAdultSprite at stage 5.</summary>
    public static bool TryGetGrowthStageSprite(string contentFolder, int treeType, string season, int growthStage, out Bitmap bitmap, out Rect source)
    {
        if (!TryGetBitmap(contentFolder, treeType, season, out bitmap))
        {
            source = default;
            return false;
        }

        source = growthStage switch
        {
            0 => new Rect(32, 128, 16, 16),
            1 => new Rect(0, 128, 16, 16),
            2 => new Rect(16, 128, 16, 16),
            _ => new Rect(0, 96, 16, 32), // 3 and 4
        };
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
