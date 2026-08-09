using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Real per-animal-type data from Data/FarmAnimals.json needed to adopt a new one
/// (FarmMapEditor.AddAnimal). House is the real building family this type lives in ("Coop" or
/// "Barn" - every entry checked has exactly one of these two, confirmed by direct inspection) -
/// used to filter the picker to whatever building the user is actually standing inside.</summary>
public sealed record PlaceableFarmAnimal(string Type, string House)
{
    public override string ToString() => Type;
}

public static class PlaceableFarmAnimals
{
    private static IReadOnlyList<PlaceableFarmAnimal>? _all;

    public static IReadOnlyList<PlaceableFarmAnimal> All => _all ??= Load();

    private static List<PlaceableFarmAnimal> Load()
    {
        var result = new List<PlaceableFarmAnimal>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "FarmAnimals.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var house = prop.Value.TryGetProperty("House", out var h) ? h.GetString() : null;
            if (string.IsNullOrEmpty(house))
                continue;

            result.Add(new PlaceableFarmAnimal(prop.Name, house));
        }

        return result.OrderBy(a => a.Type).ToList();
    }

    /// <summary>The House category a given placed building's interior actually accepts - Barn/Big
    /// Barn/Deluxe Barn all house "Barn"-category animals, Coop/Big Coop/Deluxe Coop house
    /// "Coop"-category ones (real BuildingsData.ValidOccupantTypes already confirms this same
    /// Barn/Coop split - see PlaceableBuildings' own Data/Buildings.json read). Null for anything
    /// else (not an animal building).</summary>
    public static string? HouseCategoryFor(string buildingType) => buildingType switch
    {
        "Barn" or "Big Barn" or "Deluxe Barn" => "Barn",
        "Coop" or "Big Coop" or "Deluxe Coop" => "Coop",
        _ => null,
    };
}
