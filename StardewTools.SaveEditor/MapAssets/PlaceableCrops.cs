using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// Real per-crop data needed to plant one (HoeDirtEditor.PlantCrop) - everything here is read
/// straight from Data/Crops.json (keyed by the seed's own item id), cross-referenced with
/// Data/Objects.json only for the seed's and harvest item's display names. DaysInPhase/
/// SpriteIndex/HarvestItemId etc. define crop identity the same way ParentSheetIndex does for
/// PlaceableItem - not invented, not guessed.
/// </summary>
public sealed record PlaceableCrop(
    int SeedIndex,
    string Name,
    IReadOnlyList<int> DaysInPhase,
    int RegrowDays,
    int HarvestItemId,
    string HarvestItemName,
    int HarvestMinStack,
    int HarvestMaxStack,
    double HarvestMaxIncreasePerFarmingLevel,
    bool IsScytheHarvest,
    bool IsRaisedSeeds,
    double ExtraHarvestChance,
    int SpriteIndex,
    IReadOnlyList<string> Seasons)
{
    /// <summary>The crop's own last real growth phase index (DaysInPhase.Length - 1) - what
    /// "currentPhase" should be to render as fully grown. Distinct from the game's internal
    /// phaseDays array, which additionally appends a 99999-day "stays forever" sentinel phase
    /// (see HoeDirtEditor.PlantCrop) - currentPhase never needs to reach that far.</summary>
    public int MaturePhase => DaysInPhase.Count - 1;

    public override string ToString() => Name;
}

public static class PlaceableCrops
{
    private static IReadOnlyList<PlaceableCrop>? _all;

    public static IReadOnlyList<PlaceableCrop> All => _all ??= Load();

    private static List<PlaceableCrop> Load()
    {
        var result = new List<PlaceableCrop>();
        var cropsPath = Path.Combine(BundledContent.FolderPath, "Data", "Crops.json");
        if (!File.Exists(cropsPath))
            return result;

        var itemNames = LoadObjectNames();

        using var doc = JsonDocument.Parse(File.ReadAllText(cropsPath));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (!int.TryParse(prop.Name, out var seedIndex))
                continue;

            var el = prop.Value;

            var daysInPhase = el.TryGetProperty("DaysInPhase", out var dp) && dp.ValueKind == JsonValueKind.Array
                ? dp.EnumerateArray().Select(x => x.GetInt32()).ToList()
                : new List<int>();
            if (daysInPhase.Count == 0)
                continue; // no real growth cycle to plant (e.g. a malformed/non-standard entry) - skip rather than guess one

            var regrowDays = el.TryGetProperty("RegrowDays", out var rd) && rd.ValueKind == JsonValueKind.Number ? rd.GetInt32() : -1;
            var harvestItemId = el.TryGetProperty("HarvestItemId", out var hi) && int.TryParse(hi.GetString(), out var hid) ? hid : 0;
            var harvestMin = el.TryGetProperty("HarvestMinStack", out var hMin) ? hMin.GetInt32() : 1;
            var harvestMax = el.TryGetProperty("HarvestMaxStack", out var hMax) ? hMax.GetInt32() : 1;
            var harvestBonus = el.TryGetProperty("HarvestMaxIncreasePerFarmingLevel", out var hBonus) ? hBonus.GetDouble() : 0;
            var isScythe = el.TryGetProperty("HarvestMethod", out var hm) && hm.GetString() == "Scythe";
            var isRaised = el.TryGetProperty("IsRaised", out var ir) && ir.ValueKind == JsonValueKind.True;
            var extraChance = el.TryGetProperty("ExtraHarvestChance", out var ec) ? ec.GetDouble() : 0;
            var spriteIndex = el.TryGetProperty("SpriteIndex", out var si) ? si.GetInt32() : 0;
            var seasons = el.TryGetProperty("Seasons", out var seasonsEl) && seasonsEl.ValueKind == JsonValueKind.Array
                ? seasonsEl.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                : new List<string>();

            var seedName = itemNames.GetValueOrDefault(seedIndex, $"Seed {seedIndex}");
            var harvestName = itemNames.GetValueOrDefault(harvestItemId, $"Item {harvestItemId}");

            result.Add(new PlaceableCrop(seedIndex, seedName, daysInPhase, regrowDays, harvestItemId, harvestName,
                harvestMin, harvestMax, harvestBonus, isScythe, isRaised, extraChance, spriteIndex, seasons));
        }

        return result.OrderBy(c => c.Name).ToList();
    }

    private static Dictionary<int, string> LoadObjectNames()
    {
        var result = new Dictionary<int, string>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Objects.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (int.TryParse(prop.Name, out var id) && prop.Value.TryGetProperty("Name", out var n))
                result[id] = n.GetString() ?? "";
        }

        return result;
    }
}
