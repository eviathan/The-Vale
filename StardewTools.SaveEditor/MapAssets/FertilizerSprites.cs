using System.Collections.Generic;
using Avalonia;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Where a fertilized HoeDirt tile's overlay icon lives in LooseSprites/Cursors.png
/// (MenuChrome.Cursors) - ported verbatim from decompiled HoeDirt.GetFertilizerSourceRect()'s
/// switch statement (fertilizerIndex -&gt; Rectangle(173 + index/3*16, 462 + index%3*16, 16, 16)).
/// Drawn at the tile's own top-left corner at 4x scale (64x64), same anchor as the dirt sprite
/// itself - not bottom-anchored like a tree/bush.</summary>
public static class FertilizerSprites
{
    private static readonly IReadOnlyDictionary<string, int> IndexByItemId = new Dictionary<string, int>
    {
        ["369"] = 1,
        ["370"] = 3,
        ["371"] = 4,
        ["920"] = 5,
        ["465"] = 6,
        ["466"] = 7,
        ["918"] = 8,
        ["919"] = 2,
        // 368 (Basic Fertilizer) and any unrecognized id fall through to index 0, matching the
        // decompiled switch's own default case.
    };

    /// <summary>Accepts either the qualified ("(O)369") or legacy unqualified ("369") form -
    /// HoeDirtEditor.FertilizerId can be either, same as the real game's own switch.</summary>
    public static Rect SourceFor(string fertilizerId)
    {
        var bareId = fertilizerId.StartsWith("(O)") ? fertilizerId[3..] : fertilizerId;
        var index = IndexByItemId.GetValueOrDefault(bareId, 0);
        return new Rect(173 + index / 3 * 16, 462 + index % 3 * 16, 16, 16);
    }
}
