using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Every real recipe name, from Data/CraftingRecipes.json (150 entries) and
/// Data/CookingRecipes.json (81 entries) - the dictionary key itself is the exact recipe name
/// RecipeListEditor.Learn/Forget expects (confirmed matching the real save's known-recipe keys,
/// e.g. "Wood Fence", "Fried Egg").</summary>
public static class RecipeCatalog
{
    private static IReadOnlyList<string>? _crafting;
    private static IReadOnlyList<string>? _cooking;

    public static IReadOnlyList<string> CraftingRecipeNames => _crafting ??= Load("CraftingRecipes.json");
    public static IReadOnlyList<string> CookingRecipeNames => _cooking ??= Load("CookingRecipes.json");

    private static List<string> Load(string fileName)
    {
        var result = new List<string>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", fileName);
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
            result.Add(prop.Name);

        return result.OrderBy(n => n).ToList();
    }
}
