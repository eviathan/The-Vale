using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// A player-constructed building (barn, coop, shed, ...). Unverified against real data -
/// this project's reference save has never had one built, so there was no real
/// &lt;buildings&gt; data to confirm field names against. Field names here (buildingType,
/// tileX/tileY, tilesWide/tilesHigh) come from general, long-stable Stardew modding
/// knowledge, not something we've cross-checked the way everything else in Core has been.
/// Parsing is defensive (missing fields default rather than throw) so an unexpected shape
/// degrades gracefully instead of crashing the whole Map tab.
/// </summary>
public sealed class BuildingEditor
{
    private readonly XElement _element;

    public BuildingEditor(XElement buildingElement)
    {
        _element = buildingElement;
        Position = new TilePosition(_element.TryGetChildInt("tileX") ?? 0, _element.TryGetChildInt("tileY") ?? 0);
    }

    public TilePosition Position { get; }

    public string BuildingType => _element.Element("buildingType")?.Value ?? "Unknown";

    public int Width => _element.TryGetChildInt("tilesWide") ?? 1;
    public int Height => _element.TryGetChildInt("tilesHigh") ?? 1;

    internal XElement Element => _element;
}
