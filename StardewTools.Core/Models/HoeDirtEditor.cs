using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// A tilled soil tile - confirmed against a real save's terrainFeatures dictionary. Planting
/// is optional: a HoeDirt tile can exist tilled-but-empty with no &lt;crop&gt; child at all.
/// </summary>
public sealed class HoeDirtEditor
{
    private readonly XElement _feature;

    public HoeDirtEditor(TilePosition position, XElement featureElement)
    {
        Position = position;
        _feature = featureElement;
    }

    public TilePosition Position { get; private set; }

    /// <summary>See TreeEditor.Move - same terrainFeatures dictionary, same key-is-the-position shape.</summary>
    public void Move(TilePosition newPosition)
    {
        if (Item.Element("key")?.Element("Vector2") is { } vector)
        {
            vector.SetChildInt("X", newPosition.X);
            vector.SetChildInt("Y", newPosition.Y);
        }

        Position = newPosition;
    }

    /// <summary>0 = dry, 1 = watered, 2 = paddy crop (general modding knowledge - same
    /// confidence tier as TreeEditor.TreeType's species mapping, not save-verified per value).</summary>
    public int State
    {
        get => _feature.GetChildInt("state");
        set => _feature.SetChildInt("state", value);
    }

    public int Fertilizer
    {
        get => _feature.GetChildInt("fertilizer");
        set => _feature.SetChildInt("fertilizer", value);
    }

    public bool IsGreenhouseDirt
    {
        get => _feature.GetChildBool("isGreenhouseDirt");
        set => _feature.SetChildBool("isGreenhouseDirt", value);
    }

    /// <summary>Null when this tile is tilled but nothing's planted.</summary>
    public CropEditor? Crop => _feature.Element("crop") is { } crop ? new CropEditor(crop) : null;

    /// <summary>Un-plants without un-tilling - removes just the &lt;crop&gt; child.</summary>
    public void RemoveCrop() => _feature.Element("crop")?.Remove();

    /// <summary>
    /// Plants (or replaces) this tile's crop. Every field here - and the field order/shape
    /// itself - is copied from a real planted crop in an actual save (Parsnip: seedIndex 472,
    /// phaseDays [1,1,1,1,99999], rowInSpriteSheet 0, indexOfHarvest 24; Kale: seedIndex 474,
    /// rowInSpriteSheet 2, confirming rowInSpriteSheet == Data/Crops.json's own SpriteIndex
    /// directly), not invented - the only per-crop values are what Data/Crops.json actually
    /// specifies for that seed, which the app layer's PlaceableCrops reads. The trailing 99999
    /// appended to phaseDays is the game's own "stays in this phase forever" sentinel once a
    /// crop finishes growing (Data/Crops.json's own DaysInPhase list never includes it).
    /// harvestMethod 0/1 = Grab/Scythe (StardewValley.GameData.Crops.HarvestMethod enum, decompiled).
    /// </summary>
    public CropEditor PlantCrop(
        int seedIndex, IReadOnlyList<int> daysInPhase, int regrowDays, int harvestItemId,
        int harvestMinStack, int harvestMaxStack, double harvestMaxIncreasePerFarmingLevel,
        bool isScytheHarvest, bool isRaisedSeeds, double chanceForExtraCrops, int rowInSpriteSheet,
        IReadOnlyList<string> seasons, int currentPhase, int dayOfCurrentPhase, bool fullGrown, bool flip)
    {
        _feature.Element("crop")?.Remove();

        var phaseDaysElement = new XElement("phaseDays", daysInPhase.Select(d => new XElement("int", d)));
        phaseDaysElement.Add(new XElement("int", 99999));

        var crop = new XElement("crop",
            phaseDaysElement,
            new XElement("rowInSpriteSheet", rowInSpriteSheet),
            new XElement("phaseToShow", -1),
            new XElement("currentPhase", currentPhase),
            new XElement("harvestMethod", isScytheHarvest ? 1 : 0),
            new XElement("indexOfHarvest", harvestItemId),
            new XElement("regrowAfterHarvest", regrowDays),
            new XElement("dayOfCurrentPhase", dayOfCurrentPhase),
            new XElement("minHarvest", harvestMinStack),
            new XElement("maxHarvest", harvestMaxStack),
            new XElement("maxHarvestIncreasePerFarmingLevel", harvestMaxIncreasePerFarmingLevel),
            new XElement("daysOfUnclutteredGrowth", 0),
            new XElement("whichForageCrop", 0),
            new XElement("seasonsToGrowIn", seasons.Select(s => new XElement("string", s.ToLowerInvariant()))),
            new XElement("tintColor", new XElement("B", 0), new XElement("G", 0), new XElement("R", 0), new XElement("A", 0), new XElement("PackedValue", 0)),
            new XElement("flip", flip),
            new XElement("fullGrown", fullGrown),
            new XElement("raisedSeeds", isRaisedSeeds),
            new XElement("programColored", false),
            new XElement("dead", false),
            new XElement("forageCrop", false),
            new XElement("chanceForExtraCrops", chanceForExtraCrops),
            new XElement("seedIndex", seedIndex));

        _feature.Add(crop);
        return new CropEditor(crop);
    }

    internal XElement Item => _feature.Parent!.Parent!;
}
