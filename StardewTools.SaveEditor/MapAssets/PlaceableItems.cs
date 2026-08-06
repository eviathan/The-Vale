using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Real per-item data needed to construct a new placed Object
/// (FarmMapEditor.AddObject) - Name/Category/Price/Edibility/Type, all read from the game's
/// own unpacked data rather than guessed or copied from a save's own (sometimes corrupted -
/// see FarmMapEditor.AddObject remarks) per-instance fields. IsBigCraftable distinguishes
/// Data/BigCraftables.json entries (machines/decorations like a Furnace or Grandfather Clock)
/// from Data/Objects.json ones (everything else) - both share the same underlying save
/// element shape, but are two entirely separate id spaces that happen to overlap numerically
/// (e.g. index 68 is a different item in each file), so this flag is what
/// FarmMapEditor.AddObject needs to know which index space parentSheetIndex refers to.</summary>
public sealed record PlaceableItem(int Index, string Name, int Category, int Price, int Edibility, string Type, bool IsBigCraftable)
{
    public override string ToString() => IsBigCraftable ? $"{Name} ({Index}, craftable)" : $"{Name} ({Index})";
}

public static class PlaceableItems
{
    private static IReadOnlyList<PlaceableItem>? _all;

    public static IReadOnlyList<PlaceableItem> All => _all ??= Load();

    private static List<PlaceableItem> Load()
    {
        var result = new List<PlaceableItem>();
        LoadObjects(result);
        LoadBigCraftables(result);
        return result.OrderBy(i => i.Name).ToList();
    }

    private static void LoadObjects(List<PlaceableItem> result)
    {
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Objects.json");
        if (!File.Exists(path))
            return;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (!int.TryParse(prop.Name, out var index))
                continue;

            var el = prop.Value;
            var name = el.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
            var category = el.TryGetProperty("Category", out var c) ? c.GetInt32() : 0;
            var price = el.TryGetProperty("Price", out var p) ? p.GetInt32() : 0;
            var edibility = el.TryGetProperty("Edibility", out var e) ? e.GetInt32() : -300;
            var type = el.TryGetProperty("Type", out var t) ? t.GetString() ?? "Basic" : "Basic";

            if (name.Length > 0)
                result.Add(new PlaceableItem(index, name, category, price, edibility, type, IsBigCraftable: false));
        }
    }

    /// <summary>Data/BigCraftables.json has no Category/Edibility/Type concept (confirmed - a
    /// real entry, Grandfather Clock, only has Name/Price/Fragility/CanBePlaced.../SpriteIndex)
    /// since these are placed machines/decorations, not produce - category 0 and edibility
    /// -300 (the same "not edible" sentinel Objects.json uses) are safe, inert defaults.</summary>
    private static void LoadBigCraftables(List<PlaceableItem> result)
    {
        var path = Path.Combine(BundledContent.FolderPath, "Data", "BigCraftables.json");
        if (!File.Exists(path))
            return;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (!int.TryParse(prop.Name, out var index))
                continue;

            var el = prop.Value;
            var name = el.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
            var price = el.TryGetProperty("Price", out var p) ? p.GetInt32() : 0;

            if (name.Length > 0)
                result.Add(new PlaceableItem(index, name, 0, price, -300, "Crafting", IsBigCraftable: true));
        }
    }
}
