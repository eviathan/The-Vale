using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Whether an item's ParentSheetIndex belongs to a category the game ever actually assigns
/// non-zero quality to. This is NOT a hard rule enforced by the game's data model - the
/// `quality` field exists on every Object (Object.cs) and its star icon renders unconditionally
/// whenever quality > 0, regardless of category (confirmed in the decompiled Object.draw()).
/// It's an emergent fact about which gameplay mechanics ever *set* it, found by grepping the
/// decompiled source for every place quality is assigned to a newly-created Object:
///   Crop.cs (harvest)          -> Vegetable/Fruit/Flower
///   FishingRod.cs (catch)      -> Fish
///   Bush.cs / GameLocation.cs  -> Forage
///   FarmAnimal.cs produceQuality (used by Shears.cs/MilkPail.cs/egg pickup) -> Egg/Milk/Wool
///   Cask.cs (aging)            -> Artisan Goods
/// Nothing else (raw resources, minerals, bars, seeds, crafted/cooked goods, tapper products,
/// ...) has any code path that ever sets quality above 0 in vanilla play, so those stay
/// disabled here even though the field is technically still there and editable via Core.
/// Reads Data/Objects.json from BundledContent (not the user's configurable Map ContentFolder -
/// this is static game data, not something that needs a live install to stay fresh).
/// </summary>
public static class ItemCategories
{
    private static readonly HashSet<int> QualityBearingCategories = new()
    {
        -75, // Vegetable
        -79, // Fruit
        -80, // Flower
        -81, // Forage
        -26, // Artisan Goods
        -4,  // Fish
        -5,  // Egg
        -6,  // Milk
        -18, // Wool / other animal byproducts
    };

    private static Dictionary<int, int>? _categoryByIndex;

    public static bool SupportsQuality(int? parentSheetIndex)
    {
        if (parentSheetIndex is not int index)
            return false;

        _categoryByIndex ??= Load();
        return _categoryByIndex.TryGetValue(index, out var category) && QualityBearingCategories.Contains(category);
    }

    private static Dictionary<int, int> Load()
    {
        var result = new Dictionary<int, int>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Objects.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (int.TryParse(prop.Name, out var index) && prop.Value.TryGetProperty("Category", out var category))
                result[index] = category.GetInt32();
        }

        return result;
    }
}
