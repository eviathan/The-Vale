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
    /// a plain NetString field on Building (skinId). Null means the building's default/base
    /// look. Not every building type has alternate skins to begin with - see Data/Buildings.json's
    /// Skins list, which PlaceableBuildingSkins (app layer) reads.
    /// Real save shape (confirmed via a real Greenhouse's &lt;skinId&gt;) nests the nil marker
    /// on an inner &lt;string&gt; element - <c>&lt;skinId&gt;&lt;string xsi:nil="true" /&gt;&lt;/skinId&gt;</c> -
    /// not on &lt;skinId&gt; itself. This previously read/wrote the flatter (wrong) shape; fixed
    /// here, and in FarmMapEditor.AddBuilding/AddFarmhouse's write template, together.
    /// </summary>
    public string? SkinId
    {
        get
        {
            var box = _element.Element("skinId")?.Element("string");
            if (box is null || (string?)box.Attribute(XsiNil) == "true")
                return null;
            return string.IsNullOrEmpty(box.Value) ? null : box.Value;
        }
        set
        {
            var el = _element.Element("skinId");
            if (el is null)
            {
                el = new XElement("skinId");
                _element.Add(el);
            }

            el.RemoveAll();
            var box = new XElement("string");
            if (value is null)
                box.SetAttributeValue(XsiNil, "true");
            else
                box.Value = value;
            el.Add(box);
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

    /// <summary>
    /// Which top-level &lt;GameLocation&gt; (by its real &lt;name&gt;) this building's interior
    /// resolves to, for the "singleton" interior case (Farmhouse -> "FarmHouse", Greenhouse ->
    /// "Greenhouse") - confirmed real, both present in an actual save. Null when empty/absent,
    /// which also covers buildings with no interior at all (Pet Bowl, Shipping Bin) and the
    /// *other* real interior shape (Barn/Coop/Shed's own per-instance nested &lt;indoors&gt;
    /// GameLocation) - that shape has no real save evidence available and isn't modeled here;
    /// see SaveGameEditor.GetLocationMap for how this resolves to an editable location.
    /// </summary>
    public string? NonInstancedIndoorsName
    {
        get
        {
            var name = _element.Element("nonInstancedIndoorsName")?.Value;
            return string.IsNullOrEmpty(name) ? null : name;
        }
    }

    /// <summary>Animal-building capacity/current headcount - real fields on every building type
    /// (not just Barn/Coop; Greenhouse/Farmhouse both carry the inert default -1/20 depending on
    /// vintage), confirmed via real save data. Defensive: TryGet since older/tool-created
    /// buildings may predate these fields (see BushEditor.Health's identical rationale, added
    /// after a real crash from an over-eager throwing accessor).</summary>
    public int MaxOccupants
    {
        get => _element.TryGetChildInt("maxOccupants") ?? -1;
        set => _element.SetChildIntCreateIfMissing("maxOccupants", value);
    }

    public int CurrentOccupants
    {
        get => _element.TryGetChildInt("currentOccupants") ?? 0;
        set => _element.SetChildIntCreateIfMissing("currentOccupants", value);
    }

    /// <summary>Days left before a Robin-built structure finishes construction - 0 for every
    /// real building checked (all long-finished), but a real, confirmed field.</summary>
    public int DaysOfConstructionLeft
    {
        get => _element.TryGetChildInt("daysOfConstructionLeft") ?? 0;
        set => _element.SetChildIntCreateIfMissing("daysOfConstructionLeft", value);
    }

    /// <summary>Junimo-hut-style magical construction flag - real NetBool field.</summary>
    public bool Magical
    {
        get => _element.TryGetChildBool("magical") ?? false;
        set => _element.SetChildBoolCreateIfMissing("magical", value);
    }

    public bool AnimalDoorOpen
    {
        get => _element.TryGetChildBool("animalDoorOpen") ?? false;
        set => _element.SetChildBoolCreateIfMissing("animalDoorOpen", value);
    }

    public TilePosition HumanDoor
    {
        get => ReadDoor("humanDoor");
        set => WriteDoor("humanDoor", value);
    }

    public TilePosition AnimalDoor
    {
        get => ReadDoor("animalDoor");
        set => WriteDoor("animalDoor", value);
    }

    private TilePosition ReadDoor(string childName)
    {
        var door = _element.Element(childName);
        return new TilePosition(door?.TryGetChildInt("X") ?? -1, door?.TryGetChildInt("Y") ?? -1);
    }

    private void WriteDoor(string childName, TilePosition value)
    {
        var door = _element.Element(childName);
        if (door is null)
        {
            door = new XElement(childName);
            _element.Add(door);
        }

        door.SetChildIntCreateIfMissing("X", value.X);
        door.SetChildIntCreateIfMissing("Y", value.Y);
    }

    /// <summary>Real, confirmed nested field (13 sub-fields) - see BuildingPaintColorEditor.
    /// Create-if-missing since older/tool-created buildings may predate it.</summary>
    public BuildingPaintColorEditor PaintColor
    {
        get
        {
            var el = _element.Element("buildingPaintColor");
            if (el is null)
            {
                el = BuildingPaintColorEditor.CreateDefault();
                _element.Add(el);
            }

            return new BuildingPaintColorEditor(el);
        }
    }

    /// <summary>
    /// Converts this building to a different tier (e.g. Coop -> Big Coop) by writing the new
    /// buildingType and mirroring its footprint/capacity for this tool's own immediate preview.
    /// Takes plain values rather than a Data/Buildings.json-shaped type since that catalog is
    /// app-layer knowledge (StardewTools.SaveEditor.MapAssets.BuildingsData), not something
    /// Core depends on - same separation already used for PlaceableItems/Data/Objects.json.
    /// Deliberately does not touch &lt;indoors&gt;/nonInstancedIndoorsName - the real game's own
    /// Building.load() unconditionally re-derives/relinks those from Data/Buildings.json on
    /// every load (confirmed via decompiled source), so a buildingType change alone is safe and
    /// sufficient; hand-writing indoor-map relinking here would be guessing at an unconfirmed
    /// shape for no benefit.
    /// </summary>
    public void UpgradeTo(string buildingType, int tilesWide, int tilesHigh, int maxOccupants)
    {
        _element.SetChildText("buildingType", buildingType);
        _element.SetChildIntCreateIfMissing("tilesWide", tilesWide);
        _element.SetChildIntCreateIfMissing("tilesHigh", tilesHigh);
        _element.SetChildIntCreateIfMissing("maxOccupants", maxOccupants);
    }

    internal XElement Element => _element;
}
