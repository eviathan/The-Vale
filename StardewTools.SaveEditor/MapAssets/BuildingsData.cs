using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Real per-buildingType data from Data/Buildings.json needed for interior resolution
/// and upgrade-tier conversion - a separate, additive catalog from PlaceableBuildings (which
/// deliberately excludes anything with an interior). UpgradesFrom mirrors the real
/// BuildingToUpgrade field, confirmed to be a BACKWARD reference (Big Coop's own
/// BuildingToUpgrade is "Coop", not the other way around) - e.g. Coop -> Big Coop -> Deluxe Coop,
/// Barn -> Big Barn -> Deluxe Barn. Confirmed real Coop/Barn footprints (Size) do NOT change
/// across tiers - only MaxOccupants and the indoor map do.</summary>
public sealed record BuildingTierInfo(string Name, int Width, int Height, int MaxOccupants, string? IndoorMap, string? IndoorMapType, string? NonInstancedIndoorLocation, string? UpgradesFrom);

public static class BuildingsData
{
    private static IReadOnlyDictionary<string, BuildingTierInfo>? _all;

    public static IReadOnlyDictionary<string, BuildingTierInfo> All => _all ??= Load();

    /// <summary>BuildingToUpgrade is a backward reference (see class remarks) - the next tier
    /// for buildingType is whichever entry upgrades FROM it, not whatever buildingType's own
    /// UpgradesFrom points to.</summary>
    public static BuildingTierInfo? NextTier(string buildingType)
        => All.Values.FirstOrDefault(t => t.UpgradesFrom == buildingType);

    public static bool HasInterior(string buildingType)
        => All.TryGetValue(buildingType, out var t) && (t.IndoorMap is not null || t.NonInstancedIndoorLocation is not null);

    public static bool IsNonInstancedInterior(string buildingType)
        => All.TryGetValue(buildingType, out var t) && t.NonInstancedIndoorLocation is not null;

    private static Dictionary<string, BuildingTierInfo> Load()
    {
        var result = new Dictionary<string, BuildingTierInfo>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Buildings.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var el = prop.Value;

            var width = el.TryGetProperty("Size", out var size) && size.TryGetProperty("X", out var w) ? w.GetInt32() : 1;
            var height = el.TryGetProperty("Size", out size) && size.TryGetProperty("Y", out var h) ? h.GetInt32() : 1;
            var maxOccupants = el.TryGetProperty("MaxOccupants", out var mo) ? mo.GetInt32() : -1;
            var indoorMap = NullableString(el, "IndoorMap");
            var indoorMapType = NullableString(el, "IndoorMapType");
            var nonInstanced = NullableString(el, "NonInstancedIndoorLocation");
            var upgradesFrom = NullableString(el, "BuildingToUpgrade");

            result[prop.Name] = new BuildingTierInfo(prop.Name, width, height, maxOccupants, indoorMap, indoorMapType, nonInstanced, upgradesFrom);
        }

        return result;
    }

    private static string? NullableString(JsonElement el, string propertyName)
        => el.TryGetProperty(propertyName, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
