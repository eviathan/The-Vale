using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// A planted fruit tree - the &lt;TerrainFeature xsi:type="FruitTree"&gt; shape, distinct from a
/// wild Tree (see TreeEditor). Field shape derived from decompiled StardewValley.TerrainFeatures.
/// FruitTree's [XmlElement]/[XmlIgnore] attributes - no real placed-fruit-tree save evidence was
/// available locally (this tool couldn't plant one until now), same derivation confidence as the
/// Shed/AnimalHouse work. `fruit` (a NetList of actual Item objects currently hanging on the
/// tree) is always empty here - real fruit only appears once a mature tree's own daily-update
/// logic grows some, never at plant time.
/// </summary>
public sealed class FruitTreeEditor
{
    private readonly XElement _element;

    internal FruitTreeEditor(TilePosition position, XElement fruitTreeElement)
    {
        Position = position;
        _element = fruitTreeElement;
    }

    public TilePosition Position { get; private set; }

    internal XElement Element => _element;

    /// <summary>The owning &lt;item&gt; node in the terrainFeatures dictionary - see TreeEditor.Item.</summary>
    internal XElement Item => _element.Parent!.Parent!;

    /// <summary>The real Data/FruitTrees.json key (e.g. "628" for Cherry) - defines identity,
    /// read-only like CropEditor.SeedIndex/BuildingEditor.BuildingType.</summary>
    public string TreeId => _element.GetChildText("treeId");

    /// <summary>0=seed, 1=sprout, 2=sapling, 3=bush, 4=fully grown - real constants confirmed
    /// against decompiled FruitTree.cs (seedStage/sproutStage/saplingStage/bushStage/treeStage).</summary>
    public int GrowthStage
    {
        get => _element.GetChildInt("growthStage");
        set => _element.SetChildInt("growthStage", value);
    }

    public int DaysUntilMature
    {
        get => _element.GetChildInt("daysUntilMature");
        set => _element.SetChildInt("daysUntilMature", value);
    }

    public bool Stump
    {
        get => _element.GetChildBool("stump");
        set => _element.SetChildBool("stump", value);
    }

    /// <summary>Same terrainFeatures dictionary, same key-is-the-position shape as TreeEditor.Move.</summary>
    public void Move(TilePosition newPosition)
    {
        if (Item.Element("key")?.Element("Vector2") is { } vector)
        {
            vector.SetChildInt("X", newPosition.X);
            vector.SetChildInt("Y", newPosition.Y);
        }

        Position = newPosition;
    }
}
