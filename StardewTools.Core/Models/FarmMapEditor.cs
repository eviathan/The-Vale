using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// Typed access over the Farm location's placed content: trees, grass, resource clumps
/// (stumps/boulders/logs), and world objects. This is a different layer from the base
/// terrain art (grass/dirt/path tile graphics) - that comes from the game's own map files,
/// not the save, and isn't covered here. What's covered is everything the save actually
/// tracks about what's *placed* on the farm, which is what's actually editable.
/// </summary>
public sealed class FarmMapEditor
{
    private static readonly XName XsiType = XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance");

    private readonly XElement _farmLocation;

    public FarmMapEditor(XElement farmLocationElement)
    {
        _farmLocation = farmLocationElement;
    }

    public IReadOnlyList<TreeEditor> Trees
        => DictionaryEntries("terrainFeatures")
            .Where(e => (string?)e.Value.Attribute(XsiType) == "Tree")
            .Select(e => new TreeEditor(e.Position, e.Value))
            .ToList();

    public IReadOnlyList<GrassEditor> Grass
        => DictionaryEntries("terrainFeatures")
            .Where(e => (string?)e.Value.Attribute(XsiType) == "Grass")
            .Select(e => new GrassEditor(e.Position, e.Value))
            .ToList();

    /// <summary>Tilled soil, optionally with a planted crop - confirmed against a real save.</summary>
    public IReadOnlyList<HoeDirtEditor> HoeDirtTiles
        => DictionaryEntries("terrainFeatures")
            .Where(e => (string?)e.Value.Attribute(XsiType) == "HoeDirt")
            .Select(e => new HoeDirtEditor(e.Position, e.Value))
            .ToList();

    /// <summary>
    /// terrainFeatures types present that we don't model yet. Surfaced here rather than
    /// silently dropped, so the map view can at least say "there's also N tiles of X here"
    /// instead of pretending they don't exist.
    /// </summary>
    public IReadOnlyList<(TilePosition Position, string Type)> UnmodeledTerrainFeatures
        => DictionaryEntries("terrainFeatures")
            .Select(e => (e.Position, Type: (string?)e.Value.Attribute(XsiType) ?? "Unknown"))
            .Where(e => e.Type is not ("Tree" or "Grass" or "HoeDirt"))
            .ToList();

    public IReadOnlyList<ResourceClumpEditor> ResourceClumps
        => (_farmLocation.Element("resourceClumps")?.Elements("ResourceClump") ?? Enumerable.Empty<XElement>())
            .Select(e => new ResourceClumpEditor(e))
            .ToList();

    public IReadOnlyList<PlacedObjectEditor> Objects
        => DictionaryEntries("objects")
            .Select(e => new PlacedObjectEditor(e.Position, e.Value))
            .ToList();

    /// <summary>Player-constructed buildings - see BuildingEditor remarks on why this is unverified.</summary>
    public IReadOnlyList<BuildingEditor> Buildings
        => (_farmLocation.Element("buildings")?.Elements("Building") ?? Enumerable.Empty<XElement>())
            .Select(e => new BuildingEditor(e))
            .ToList();

    /// <summary>
    /// Places a new plain Object on the farm at the given tile. The element shape (every
    /// field, in this order) is copied from a real placed Object in an actual save, not
    /// invented - the only per-item fields are name/parentSheetIndex/price/edibility/type/
    /// category, which should come from real Data/Objects.json data (see PlaceableItems),
    /// not the save's own &lt;type&gt;/&lt;category&gt; values - those turned out to already
    /// be corrupted with placeholder junk ("asdf" as a type, wrong category) in a real save
    /// this was verified against, apparently from some earlier, unrelated tool.
    /// </summary>
    public PlacedObjectEditor AddObject(TilePosition position, int parentSheetIndex, string name, int price, int edibility, int category, string type)
    {
        var container = _farmLocation.Element("objects");
        if (container is null)
        {
            container = new XElement("objects");
            _farmLocation.Add(container);
        }

        var boundsX = position.X * 64;
        var boundsY = position.Y * 64;

        var value = new XElement("Object",
            new XElement("isLostItem", false),
            new XElement("category", category),
            new XElement("hasBeenInInventory", false),
            new XElement("name", name),
            new XElement("parentSheetIndex", parentSheetIndex),
            new XElement("specialItem", false),
            new XElement("SpecialVariable", 0),
            new XElement("DisplayName", name),
            new XElement("Name", name),
            new XElement("Stack", 1),
            new XElement("tileLocation", new XElement("X", position.X), new XElement("Y", position.Y)),
            new XElement("owner", 0),
            new XElement("type", type),
            new XElement("canBeSetDown", true),
            new XElement("canBeGrabbed", true),
            new XElement("isHoedirt", false),
            new XElement("isSpawnedObject", false),
            new XElement("questItem", false),
            new XElement("questId", 0),
            new XElement("isOn", true),
            new XElement("fragility", 0),
            new XElement("price", price),
            new XElement("edibility", edibility),
            new XElement("stack", 1),
            new XElement("quality", 0),
            new XElement("bigCraftable", false),
            new XElement("setOutdoors", false),
            new XElement("setIndoors", false),
            new XElement("readyForHarvest", false),
            new XElement("showNextIndex", false),
            new XElement("flipped", false),
            new XElement("hasBeenPickedUpByFarmer", false),
            new XElement("isRecipe", false),
            new XElement("isLamp", false),
            new XElement("minutesUntilReady", 1),
            new XElement("boundingBox",
                new XElement("X", boundsX), new XElement("Y", boundsY),
                new XElement("Width", 64), new XElement("Height", 64),
                new XElement("Location", new XElement("X", boundsX), new XElement("Y", boundsY)),
                new XElement("Size", new XElement("X", 64), new XElement("Y", 64))),
            new XElement("scale", new XElement("X", 0), new XElement("Y", 0)),
            new XElement("uses", 0),
            new XElement("preservedParentSheetIndex", 0));

        var item = new XElement("item",
            new XElement("key", new XElement("Vector2", new XElement("X", position.X), new XElement("Y", position.Y))),
            new XElement("value", value));

        container.Add(item);
        return new PlacedObjectEditor(position, value);
    }

    public void Remove(TreeEditor tree) => tree.Item.Remove();
    public void Remove(GrassEditor grass) => grass.Item.Remove();
    public void Remove(HoeDirtEditor dirt) => dirt.Item.Remove();
    public void Remove(ResourceClumpEditor clump) => clump.Element.Remove();
    public void Remove(PlacedObjectEditor placedObject) => placedObject.WrappingItem.Remove();
    public void Remove(BuildingEditor building) => building.Element.Remove();

    /// <summary>
    /// Walks a `&lt;name&gt;&lt;item&gt;&lt;key&gt;&lt;Vector2&gt;X/Y&lt;/Vector2&gt;&lt;/key&gt;
    /// &lt;value&gt;{single child}&lt;/value&gt;&lt;/item&gt;...&lt;/name&gt;` tile dictionary,
    /// yielding each entry's position and its value's single child element.
    /// </summary>
    private IEnumerable<(TilePosition Position, XElement Value)> DictionaryEntries(string containerName)
    {
        var container = _farmLocation.Element(containerName);
        if (container is null)
            yield break;

        foreach (var item in container.Elements("item"))
        {
            var vector = item.Element("key")?.Element("Vector2");
            var value = item.Element("value")?.Elements().FirstOrDefault();
            if (vector is null || value is null)
                continue;

            yield return (new TilePosition(vector.GetChildInt("X"), vector.GetChildInt("Y")), value);
        }
    }
}
