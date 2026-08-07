using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Real item catalogs for the Collections tab's five pages (Shipping/Minerals/Cooking/
/// Fish/Artifacts) - each entry's numeric id is what actually gets stored as the underlying
/// dictionary key (see PlayerEditor.ShippedItems/MineralsFound/RecipesCooked/FishCaught/
/// ArtifactsFound remarks - Fish additionally needs a "(O)" prefix at the point of use, since
/// its real key is the qualified item id, not this catalog's plain numeric one), the name is
/// display-only.</summary>
public static class CollectionCatalogs
{
    private static IReadOnlyList<ViewModels.NamedValue>? _shippable;
    private static IReadOnlyList<ViewModels.NamedValue>? _minerals;
    private static IReadOnlyList<ViewModels.NamedValue>? _cookedDishes;
    private static IReadOnlyList<ViewModels.NamedValue>? _fish;
    private static IReadOnlyList<ViewModels.NamedValue>? _artifacts;

    /// <summary>Every non-BigCraftable Objects.json entry - shipping isn't gated by a single
    /// clean flag in the data, so this errs inclusive (marking something "shipped" that
    /// couldn't really be sold in-game is a harmless data toggle, not a correctness risk).</summary>
    public static IReadOnlyList<ViewModels.NamedValue> ShippableItems => _shippable ??= LoadObjects(el => true);

    /// <summary>Category -12 in Data/Objects.json - confirmed real minerals (Alamite, Bixite,
    /// Baryte, ...), not guessed from a broader "gems" category.</summary>
    public static IReadOnlyList<ViewModels.NamedValue> Minerals => _minerals ??= LoadObjects(el => Category(el) == -12);

    /// <summary>Category -7 - confirmed real cooked dishes (Fried Egg, Omelet, Salad, ...),
    /// matching what RecipesCooked's dictionary actually keys on (the dish's own item id).</summary>
    public static IReadOnlyList<ViewModels.NamedValue> CookedDishes => _cookedDishes ??= LoadObjects(el => Category(el) == -7);

    /// <summary>Category -4 - confirmed real fish (Pufferfish, Anchovy, Tuna, ...). Note the real
    /// FishCaught dictionary key needs a "(O)" prefix on this catalog's id - see
    /// PlayerEditor.FishCaught remarks.</summary>
    public static IReadOnlyList<ViewModels.NamedValue> Fish => _fish ??= LoadObjects(el => Category(el) == -4);

    /// <summary>Type == "Arch" - confirmed real artifacts (Dwarf Scroll I-IV, Chipped Amphora,
    /// Arrowhead, ...), not derivable from Category alone (artifacts share Category 0 with 189
    /// unrelated items).</summary>
    public static IReadOnlyList<ViewModels.NamedValue> Artifacts => _artifacts ??= LoadObjects(el => el.TryGetProperty("Type", out var t) && t.GetString() == "Arch");

    private static int? Category(JsonElement el) => el.TryGetProperty("Category", out var c) ? c.GetInt32() : (int?)null;

    private static List<ViewModels.NamedValue> LoadObjects(Func<JsonElement, bool> predicate)
    {
        var result = new List<ViewModels.NamedValue>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Objects.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (!int.TryParse(prop.Name, out var id))
                continue;

            var el = prop.Value;
            if (!predicate(el))
                continue;

            var name = el.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
            if (name.Length > 0)
                result.Add(new ViewModels.NamedValue(id, name));
        }

        return result.OrderBy(v => v.Name).ToList();
    }
}
