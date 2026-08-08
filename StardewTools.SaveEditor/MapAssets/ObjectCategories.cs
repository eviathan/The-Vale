using System.Collections.Generic;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Friendly group labels for the real Data/Objects.json Category codes (see the
/// decompiled StardewValley.Object's *Category constants - names/values confirmed against that
/// source, not guessed). Only used to group the Map tab's object picker; nothing here changes
/// what actually gets written to a save. Big Craftables have no real Category concept of their
/// own (PlaceableItems.LoadBigCraftables always writes 0) - they get their own synthetic group
/// instead of falling into whatever Category 0 would otherwise mean.</summary>
public static class ObjectCategories
{
    public const string BigCraftablesGroup = "Big Craftables";
    public const string OtherGroup = "Other";

    private static readonly Dictionary<int, string> Labels = new()
    {
        [-2] = "Gems/Minerals",
        [-4] = "Fish",
        [-5] = "Eggs",
        [-6] = "Milk",
        [-7] = "Cooking",
        [-8] = "Crafting",
        [-9] = "Big Craftables",
        [-12] = "Minerals",
        [-14] = "Meat",
        [-15] = "Metal Resources",
        [-16] = "Building Resources",
        [-17] = "Sold at Pierre's",
        [-18] = "Sold at Pierre's/Marnie's",
        [-19] = "Fertilizer",
        [-20] = "Junk",
        [-21] = "Bait",
        [-22] = "Tackle",
        [-23] = "Sold at Fish Shop",
        [-24] = "Furniture",
        [-25] = "Ingredients",
        [-26] = "Artisan Goods",
        [-27] = "Syrup",
        [-28] = "Monster Loot",
        [-29] = "Equipment",
        [-74] = "Seeds",
        [-75] = "Vegetables",
        [-79] = "Fruits",
        [-80] = "Flowers",
        [-81] = "Greens",
        [-94] = "Clothing",
        [-95] = "Hats",
        [-96] = "Rings",
        [-97] = "Boots",
        [-98] = "Weapons",
        [-99] = "Tools",
        [-100] = "Clothing",
        [-101] = "Trinkets",
        [-102] = "Books",
        [-103] = "Skill Books",
        [-999] = "Litter",
    };

    /// <summary>The group an item's picker entry falls under - Big Craftables always group
    /// together regardless of their (always-zero) Category field; anything else with an
    /// unrecognized/zero Category falls into Other rather than being silently excluded from
    /// every filter.</summary>
    public static string GroupFor(PlaceableItem item)
    {
        if (item.IsBigCraftable)
            return BigCraftablesGroup;

        return Labels.TryGetValue(item.Category, out var label) ? label : OtherGroup;
    }
}
