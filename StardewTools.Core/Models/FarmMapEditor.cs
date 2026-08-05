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

    /// <summary>
    /// terrainFeatures types present that we don't model yet (e.g. HoeDirt/crops - this
    /// reference save had none planted, so we didn't have real data to verify that schema
    /// against). Surfaced here rather than silently dropped, so the map view can at least
    /// say "there's also N tiles of X here" instead of pretending they don't exist.
    /// </summary>
    public IReadOnlyList<(TilePosition Position, string Type)> UnmodeledTerrainFeatures
        => DictionaryEntries("terrainFeatures")
            .Select(e => (e.Position, Type: (string?)e.Value.Attribute(XsiType) ?? "Unknown"))
            .Where(e => e.Type is not ("Tree" or "Grass"))
            .ToList();

    public IReadOnlyList<ResourceClumpEditor> ResourceClumps
        => (_farmLocation.Element("resourceClumps")?.Elements("ResourceClump") ?? Enumerable.Empty<XElement>())
            .Select(e => new ResourceClumpEditor(e))
            .ToList();

    public IReadOnlyList<PlacedObjectEditor> Objects
        => DictionaryEntries("objects")
            .Select(e => new PlacedObjectEditor(e.Position, e.Value))
            .ToList();

    public void Remove(TreeEditor tree) => tree.Item.Remove();
    public void Remove(GrassEditor grass) => grass.Item.Remove();
    public void Remove(ResourceClumpEditor clump) => clump.Element.Remove();
    public void Remove(PlacedObjectEditor placedObject) => placedObject.WrappingItem.Remove();

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
