using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Real per-fruit-tree-type data from Data/FruitTrees.json needed to plant one
/// (FarmMapEditor.AddFruitTree). Name is derived from the tree's own fruit item (Objects.json's
/// "Name" field for the first entry in Fruit[]), not DisplayName's LocalizedText reference -
/// same approach PlaceableCrops.cs already uses for seed/harvest names.</summary>
public sealed record PlaceableFruitTree(string TreeId, string Name, int TextureSpriteRow)
{
    public override string ToString() => Name;
}

public static class PlaceableFruitTrees
{
    private static IReadOnlyList<PlaceableFruitTree>? _all;

    public static IReadOnlyList<PlaceableFruitTree> All => _all ??= Load();

    public static string NameFor(string treeId) => All.FirstOrDefault(t => t.TreeId == treeId)?.Name ?? $"Fruit tree {treeId}";

    private static List<PlaceableFruitTree> Load()
    {
        var result = new List<PlaceableFruitTree>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "FruitTrees.json");
        if (!File.Exists(path))
            return result;

        var itemNames = LoadObjectNames();

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var el = prop.Value;

            var spriteRow = el.TryGetProperty("TextureSpriteRow", out var sr) ? sr.GetInt32() : 0;

            string? fruitItemId = null;
            if (el.TryGetProperty("Fruit", out var fruitArray) && fruitArray.ValueKind == JsonValueKind.Array && fruitArray.GetArrayLength() > 0)
            {
                var qualifiedId = fruitArray[0].TryGetProperty("ItemId", out var idEl) ? idEl.GetString() : null;
                fruitItemId = qualifiedId?.StartsWith("(O)") == true ? qualifiedId[3..] : qualifiedId;
            }

            var name = fruitItemId is not null && itemNames.TryGetValue(fruitItemId, out var n) ? n : $"Fruit tree {prop.Name}";
            result.Add(new PlaceableFruitTree(prop.Name, name, spriteRow));
        }

        return result.OrderBy(t => t.Name).ToList();
    }

    private static Dictionary<string, string> LoadObjectNames()
    {
        var result = new Dictionary<string, string>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Objects.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("Name", out var n))
                result[prop.Name] = n.GetString() ?? "";
        }

        return result;
    }
}
