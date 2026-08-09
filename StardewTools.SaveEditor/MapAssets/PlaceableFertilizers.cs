using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>One real fertilizer/soil-additive type applicable to a HoeDirt tile (HoeDirtEditor.
/// ApplyFertilizer). Name is derived from the real Data/Objects.json "Name" field, same approach
/// PlaceableCrops/PlaceableFruitTrees already use.</summary>
public sealed record PlaceableFertilizer(string ItemId, string Name)
{
    /// <summary>What HoeDirtEditor.FertilizerId actually stores - decompiled HoeDirt.plant()
    /// always qualifies the id (ItemRegistry.QualifyItemId) before writing it.</summary>
    public string QualifiedId => "(O)" + ItemId;

    public override string ToString() => Name;
}

public static class PlaceableFertilizers
{
    /// <summary>The real item ids decompiled HoeDirt.GetFertilizerSourceRect() recognizes -
    /// Basic/Quality/Deluxe Fertilizer, Basic/Quality/Deluxe Retaining Soil, and Speed-Gro/
    /// Deluxe Speed-Gro/Hyper Speed-Gro (HoeDirt.cs's own fertilizerLowQualityID/
    /// fertilizerHighQualityID/etc. constants). Deliberately NOT every Category: Fertilizer
    /// object in Objects.json - Tree Fertilizer (805) is a different item applied to Tree/
    /// FruitTree, not HoeDirt, so it's excluded here. Order is gameplay tier order, not
    /// alphabetical - more useful in a picker than a shuffled list.</summary>
    private static readonly string[] RealFertilizerIds = { "368", "369", "919", "370", "371", "920", "465", "466", "918" };

    private static IReadOnlyList<PlaceableFertilizer>? _all;

    public static IReadOnlyList<PlaceableFertilizer> All => _all ??= Load();

    private static List<PlaceableFertilizer> Load()
    {
        var result = new List<PlaceableFertilizer>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Objects.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var id in RealFertilizerIds)
        {
            if (doc.RootElement.TryGetProperty(id, out var el) && el.TryGetProperty("Name", out var n))
                result.Add(new PlaceableFertilizer(id, n.GetString() ?? id));
        }

        return result;
    }
}
