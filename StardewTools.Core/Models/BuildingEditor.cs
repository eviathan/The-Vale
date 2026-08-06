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
    private static readonly XName XsiNil = XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance");

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

    /// <summary>
    /// Which reskin is applied (e.g. Pet Bowl's "Stone Pet Bowl"/"Hay Pet Bowl") - confirmed as
    /// a plain NetString field on Building (skinId), same xsi:nil-when-absent convention as an
    /// empty inventory slot (see ItemListEditor). Null means the building's default/base look.
    /// Not every building type has alternate skins to begin with - see Data/Buildings.json's
    /// Skins list, which PlaceableBuildingSkins (app layer) reads.
    /// </summary>
    public string? SkinId
    {
        get
        {
            var el = _element.Element("skinId");
            if (el is null || (string?)el.Attribute(XsiNil) == "true")
                return null;
            return string.IsNullOrEmpty(el.Value) ? null : el.Value;
        }
        set
        {
            var el = _element.Element("skinId");
            if (el is null)
            {
                el = new XElement("skinId");
                _element.Add(el);
            }

            el.RemoveAttributes();
            if (value is null)
            {
                el.SetAttributeValue(XsiNil, "true");
                el.Value = "";
            }
            else
            {
                el.Value = value;
            }
        }
    }

    /// <summary>A building's position is its own tileX/tileY fields (flat list, not a tile
    /// dictionary) - moving it is a direct field edit.</summary>
    public void Move(TilePosition newPosition)
    {
        _element.SetChildInt("tileX", newPosition.X);
        _element.SetChildInt("tileY", newPosition.Y);
        Position = newPosition;
    }

    /// <summary>Only meaningful for Silo (Data/Buildings.json's own HayCapacity - 240 for a
    /// real Silo, 0 for everything else) - a real, confirmed NetInt field on Building
    /// (Building.cs: hayCapacity) that was previously missing from newly-placed buildings
    /// entirely. Defensive create-if-missing since older buildings (constructed before this
    /// tool tracked it) may not have the element at all.</summary>
    public int HayCapacity
    {
        get => _element.TryGetChildInt("hayCapacity") ?? 0;
        set
        {
            var el = _element.Element("hayCapacity");
            if (el is null)
            {
                el = new XElement("hayCapacity");
                _element.Add(el);
            }

            el.Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    internal XElement Element => _element;
}
