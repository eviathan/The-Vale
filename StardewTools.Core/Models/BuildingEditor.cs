using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// A player-constructed building (barn, coop, shed, ...). Field names (buildingType, tileX/
/// tileY, tilesWide/tilesHigh) are now confirmed against the decompiled Building.cs's
/// [XmlElement] attributes, not just modding knowledge - but still not against a live save's
/// actual &lt;buildings&gt; data, since none of the saves available while building this ever
/// had one constructed. Parsing stays defensive (missing fields default rather than throw) so
/// an unexpected shape degrades gracefully instead of crashing the whole Map tab.
/// </summary>
public sealed class BuildingEditor
{
    private readonly XElement _element;

    public BuildingEditor(XElement buildingElement)
    {
        _element = buildingElement;
        Position = new TilePosition(_element.TryGetChildInt("tileX") ?? 0, _element.TryGetChildInt("tileY") ?? 0);
    }

    public TilePosition Position { get; private set; }

    public string BuildingType => _element.Element("buildingType")?.Value ?? "Unknown";

    public int Width => _element.TryGetChildInt("tilesWide") ?? 1;
    public int Height => _element.TryGetChildInt("tilesHigh") ?? 1;

    /// <summary>A building's position is its own tileX/tileY fields (flat list, not a tile
    /// dictionary) - moving it is a direct field edit.</summary>
    public void Move(TilePosition newPosition)
    {
        _element.SetChildInt("tileX", newPosition.X);
        _element.SetChildInt("tileY", newPosition.Y);
        Position = newPosition;
    }

    internal XElement Element => _element;
}
