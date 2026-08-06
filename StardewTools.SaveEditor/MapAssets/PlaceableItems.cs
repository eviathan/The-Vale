using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Real per-item data from Data/Objects.json needed to construct a new placed
/// Object (FarmMapEditor.AddObject) - Name/Category/Price/Edibility/Type, all read from the
/// game's own unpacked data rather than guessed or copied from a save's own (sometimes
/// corrupted - see FarmMapEditor.AddObject remarks) per-instance fields.</summary>
public sealed record PlaceableItem(int Index, string Name, int Category, int Price, int Edibility, string Type)
{
    public override string ToString() => $"{Name} ({Index})";
}

public static class PlaceableItems
{
    private static IReadOnlyList<PlaceableItem>? _all;

    public static IReadOnlyList<PlaceableItem> All => _all ??= Load();

    private static List<PlaceableItem> Load()
    {
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Objects.json");
        if (!File.Exists(path))
            return new List<PlaceableItem>();

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var result = new List<PlaceableItem>();
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
                result.Add(new PlaceableItem(index, name, category, price, edibility, type));
        }

        return result.OrderBy(i => i.Name).ToList();
    }
}
