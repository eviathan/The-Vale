using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Real per-building data needed to construct a new placed Building
/// (FarmMapEditor.AddBuilding). Deliberately limited to Data/Buildings.json entries with no
/// interior (IndoorMap and NonInstancedIndoorLocation both null) - confirmed these all share
/// a HumanDoor of (-1,-1), i.e. no door, nothing to wire up: Gold Clock, the 4 Obelisks, Well,
/// Silo, Mill, Fish Pond, Pet Bowl, Stable, Shipping Bin, Junimo Hut. Buildings with a real
/// interior (Barn, Coop, Big Shed, ...) need that interior location linked correctly, which
/// isn't verified yet, so they're excluded here rather than risk a building whose door leads
/// nowhere.</summary>
public sealed record PlaceableBuilding(string Name, int TilesWide, int TilesHigh, bool Magical)
{
    public override string ToString() => $"{Name} ({TilesWide}x{TilesHigh})";
}

public static class PlaceableBuildings
{
    private static IReadOnlyList<PlaceableBuilding>? _all;

    public static IReadOnlyList<PlaceableBuilding> All => _all ??= Load();

    private static List<PlaceableBuilding> Load()
    {
        var result = new List<PlaceableBuilding>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Buildings.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var el = prop.Value;

            var hasIndoorMap = el.TryGetProperty("IndoorMap", out var indoor) && indoor.ValueKind != JsonValueKind.Null;
            var hasNonInstanced = el.TryGetProperty("NonInstancedIndoorLocation", out var nonInstanced) && nonInstanced.ValueKind != JsonValueKind.Null;
            if (hasIndoorMap || hasNonInstanced)
                continue;

            var width = el.TryGetProperty("Size", out var size) && size.TryGetProperty("X", out var w) ? w.GetInt32() : 1;
            var height = el.TryGetProperty("Size", out size) && size.TryGetProperty("Y", out var h) ? h.GetInt32() : 1;
            var magical = el.TryGetProperty("MagicalConstruction", out var m) && m.GetBoolean();

            result.Add(new PlaceableBuilding(prop.Name, width, height, magical));
        }

        return result.OrderBy(b => b.Name).ToList();
    }
}
