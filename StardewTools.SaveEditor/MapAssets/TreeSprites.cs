using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Resolves a tree's (species, season) to its real sprite sheet and the source rect for its
/// current growth stage. The per-species TEXTURE FILE is resolved from the real
/// Data/WildTrees.json (not a hand-built "baseName_season" string) - confirmed necessary, not
/// just tidier: real tree types differ in how many season-specific files they even have (Pine,
/// type 3, has no separate summer file at all - summer falls back to its default/spring file;
/// Desert Palm/Mushroom Tree/Palm2/Mystic Tree - types 6/7/9/13 - have exactly ONE file each,
/// no season suffix whatsoever), so no single filename formula covers every real type. The
/// selection algorithm below (first Textures[] entry whose Season is null-or-matching, else
/// Textures[0]) is copied from the decompiled Tree.ChooseTexture() (Tree.cs) verbatim, minus its
/// Location.IsGreenhouse special-case (out of scope - this tool doesn't render a Farm tree
/// differently when the underlying GameLocation happens to be a Greenhouse) and its Condition
/// check (every real entry in Data/WildTrees.json has Condition: null, so there's nothing to
/// evaluate).
///
/// Once a texture file is resolved, the source-rect math itself IS universal across every real
/// tree type - confirmed against Tree.draw()/drawInMenu() (Tree.cs): the adult canopy is always
/// Rectangle(sourceRect.X, 0, 48, 96) with sourceRect.X toggled 0/48 (UseAlternateSpriteWhenSeedReady/
/// WhenNotShaken - not modeled here, real per-type gap, see TryGetAdultSprite) or 96 (hasMoss,
/// GrowsMoss is only true for types 1/2/3/10/11 and their winter files are narrower than 96px,
/// but the real game also hard-disables moss in winter - Tree.cs's hasMoss setter - so X=96 is
/// never actually requested against a too-narrow sheet in real play), growth stages 0-4 and the
/// stump are likewise fixed rects with no per-type branch anywhere in Tree.cs.
/// </summary>
public static class TreeSprites
{
    private static readonly Dictionary<string, Bitmap> BitmapCache = new();
    private static Dictionary<int, List<(string? Season, string Texture)>>? _treeData;
    private static string? _treeDataFolder;

    /// <summary>Canopy source rect - real, verbatim from Tree.cs's static
    /// <c>treeTopSourceRect = new Rectangle(0, 0, 48, 96)</c>, X toggled to 96 when hasMoss
    /// (Tree.draw()). A previous version of this cropped to the visible (non-transparent) region
    /// (X,4,48,80) instead - that's real about the *pixel content*, but not what the game itself
    /// actually draws or positions against (Tree.draw()'s canopy position/origin math is
    /// calibrated for the full 96-tall rect), and real gameplay ALSO always draws a separate
    /// trunk/stump layer underneath every living tree (see TryGetStumpSprite + FarmMapControl.
    /// TryDrawTreeSprite) - omitting that trunk layer, not this rect, was the actual visible bug
    /// a real user reported ("trees ... missing stumps").</summary>
    public static bool TryGetAdultSprite(string contentFolder, int treeType, string season, bool hasMoss, out Bitmap bitmap, out Rect source)
    {
        if (!TryGetBitmap(contentFolder, treeType, season, out bitmap))
        {
            source = default;
            return false;
        }

        source = new Rect(hasMoss ? 96 : 0, 0, 48, 96);
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

    /// <summary>
    /// The trunk/stump sprite - real, verbatim from Tree.cs's static
    /// <c>stumpSourceRect = new Rectangle(32, 96, 16, 32)</c>, X shifted +96 when hasMoss
    /// (`stumpSource.X += 96` in Tree.draw()). This is NOT only for a chopped-down stump - the
    /// real game draws this exact sprite underneath every living adult tree too (Tree.draw()'s
    /// unconditional second Draw call, gated only on health, not on the `stump` field) - see
    /// FarmMapControl.TryDrawTreeSprite for both call sites and their real, distinct positions
    /// (a chopped stump is positioned differently than the living-tree trunk layer - top-left
    /// anchored one tile above the tree's own tile in both cases, never bottom-anchored the way
    /// the canopy is - a previous version of this method's caller wrongly bottom-anchored it).
    /// </summary>
    public static bool TryGetStumpSprite(string contentFolder, int treeType, string season, bool hasMoss, out Bitmap bitmap, out Rect source)
    {
        if (!TryGetBitmap(contentFolder, treeType, season, out bitmap))
        {
            source = default;
            return false;
        }

        source = new Rect(32 + (hasMoss ? 96 : 0), 96, 16, 32);
        return true;
    }

    private static bool TryGetBitmap(string contentFolder, int treeType, string season, out Bitmap bitmap)
    {
        bitmap = null!;

        var data = LoadTreeData(contentFolder);
        if (!data.TryGetValue(treeType, out var textures) || textures.Count == 0)
            return false;

        var normalizedSeason = season.Trim();
        var chosen = textures.FirstOrDefault(t => t.Season is null || string.Equals(t.Season, normalizedSeason, StringComparison.OrdinalIgnoreCase));
        var texturePath = chosen.Texture ?? textures[0].Texture;

        // Real paths are backslash-separated content-pipeline ids (e.g. "TerrainFeatures\tree1_spring").
        var relativePath = texturePath.Replace('\\', Path.DirectorySeparatorChar) + ".png";
        var fullPath = Path.Combine(contentFolder, relativePath);

        var cacheKey = fullPath;
        if (!BitmapCache.TryGetValue(cacheKey, out var loaded))
        {
            if (!File.Exists(fullPath))
                return false;

            loaded = new Bitmap(fullPath);
            BitmapCache[cacheKey] = loaded;
        }

        bitmap = loaded;
        return true;
    }

    private static Dictionary<int, List<(string? Season, string Texture)>> LoadTreeData(string contentFolder)
    {
        if (_treeData is not null && _treeDataFolder == contentFolder)
            return _treeData;

        var result = new Dictionary<int, List<(string?, string)>>();
        var path = Path.Combine(contentFolder, "Data", "WildTrees.json");
        if (File.Exists(path))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!int.TryParse(prop.Name, out var treeType))
                    continue;
                if (!prop.Value.TryGetProperty("Textures", out var texturesEl) || texturesEl.ValueKind != JsonValueKind.Array)
                    continue;

                var list = new List<(string?, string)>();
                foreach (var entry in texturesEl.EnumerateArray())
                {
                    var texture = entry.TryGetProperty("Texture", out var t) ? t.GetString() : null;
                    if (string.IsNullOrEmpty(texture))
                        continue;

                    var seasonEl = entry.TryGetProperty("Season", out var s) ? s : default;
                    var season = seasonEl.ValueKind == JsonValueKind.String ? seasonEl.GetString() : null;
                    list.Add((season, texture));
                }

                if (list.Count > 0)
                    result[treeType] = list;
            }
        }

        _treeData = result;
        _treeDataFolder = contentFolder;
        return result;
    }
}
