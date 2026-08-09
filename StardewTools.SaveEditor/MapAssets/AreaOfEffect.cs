using System;
using System.Collections.Generic;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// #9: "adding sprinklers with nozzles should [...] show us the area affected, infact anything
/// with an AOE should do that." Covers the two real, confirmed AOE object types (decompiled
/// Object.cs) - sprinklers (a watered-tile area) and scarecrows (a crow-protection area). Both
/// are plain Objects (sprinklers) or BigCraftables (scarecrows), not a separate item family, so
/// detection is by real item id/name, matching decompiled GetBaseRadiusForSprinkler/IsScarecrow/
/// GetRadiusForScarecrow exactly.
///
/// Deliberately NOT modeled: the Pressure Nozzle's +1 sprinkler radius bonus (decompiled
/// GetModifiedRadiusForSprinkler checks `heldObject.QualifiedItemId == "(O)915"`) - attaching a
/// held object to a placed Object isn't modeled by this tool at all yet (ItemEditor has no
/// HeldObject accessor), so every sprinkler preview here is its own BASE radius regardless of
/// what's really attached in a loaded save. Flagged rather than guessed.
/// </summary>
public static class AreaOfEffect
{
    /// <summary>The affected tiles as (dx, dy) offsets from the object's own tile - dx=dy=0 (the
    /// object's own tile) is included for the square sprinkler cases (radius 1/2) exactly like
    /// decompiled GetSprinklerTiles does, but not for the 4-neighbor radius-0 case or for
    /// scarecrows (a scarecrow's own tile isn't "protected" in any meaningful sense - decompiled
    /// GetRadiusForScarecrow's Vector2.Distance check happens to still be 0 there too, but it's
    /// never a plantable tile in practice). Null if this item has no known AOE.</summary>
    public static IReadOnlyList<(int Dx, int Dy)>? TilesFor(bool isBigCraftable, string itemId, string name)
    {
        if (!isBigCraftable && SprinklerBaseRadius(itemId) is { } sprinklerRadius)
            return SprinklerOffsets(sprinklerRadius);

        if (isBigCraftable && ScarecrowRadius(name) is { } scarecrowRadius)
            return ScarecrowOffsets(scarecrowRadius);

        return null;
    }

    /// <summary>Decompiled Object.GetBaseRadiusForSprinkler's own switch, by real unqualified
    /// Objects.json id: Basic Sprinkler(599)=0, Quality Sprinkler(621)=1, Iridium Sprinkler(645)=2.</summary>
    private static int? SprinklerBaseRadius(string itemId) => itemId switch
    {
        "599" => 0,
        "621" => 1,
        "645" => 2,
        _ => null,
    };

    /// <summary>Decompiled Object.GetSprinklerTiles: radius 0 is just the 4 cardinal neighbors
    /// (Utility.getAdjacentTileLocations - NOT including the sprinkler's own tile); radius&gt;0 is
    /// a full square from -radius to +radius inclusive on both axes (including the center tile,
    /// same as the real method - harmless since nothing can be planted where the sprinkler sits).</summary>
    private static IReadOnlyList<(int, int)> SprinklerOffsets(int radius)
    {
        if (radius == 0)
            return new (int, int)[] { (-1, 0), (1, 0), (0, 1), (0, -1) };

        var tiles = new List<(int, int)>();
        for (var dx = -radius; dx <= radius; dx++)
            for (var dy = -radius; dy <= radius; dy++)
                tiles.Add((dx, dy));
        return tiles;
    }

    /// <summary>Decompiled Object.IsScarecrow()/GetRadiusForScarecrow()'s own name-based fallback
    /// (the ContextTags-based override this tool's bundled Data/BigCraftables.json doesn't
    /// actually carry any values for - every real scarecrow/Rarecrow entry has ContextTags: None
    /// in the shipped data, confirmed by inspection). "Deluxe"-prefixed -&gt; 17, everything else
    /// containing "arecrow" (Scarecrow, all 8 Rarecrow variants) -&gt; 9.</summary>
    private static int? ScarecrowRadius(string name)
    {
        if (!name.Contains("arecrow", StringComparison.Ordinal))
            return null;

        return name.StartsWith("Deluxe", StringComparison.Ordinal) ? 17 : 9;
    }

    /// <summary>Decompiled Farm.cs's own crow-protection check: `Vector2.Distance(scarecrow, tile)
    /// &lt; radius` - a real Euclidean circle, strictly less-than (not &lt;=), not a square or
    /// diamond like the sprinkler case.</summary>
    private static IReadOnlyList<(int, int)> ScarecrowOffsets(int radius)
    {
        var tiles = new List<(int, int)>();
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                if (Math.Sqrt(dx * dx + dy * dy) < radius)
                    tiles.Add((dx, dy));
            }
        }
        return tiles;
    }
}
