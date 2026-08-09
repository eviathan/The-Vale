using System.Collections.Generic;
using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// A building's real interior - a full nested &lt;indoors xsi:type="..."&gt; GameLocation
/// instance, embedded inline inside the owning &lt;Building&gt; element. This is a DIFFERENT
/// shape from Farmhouse/Greenhouse's &lt;nonInstancedIndoorsName&gt; (a string pointing at a
/// shared top-level &lt;GameLocation&gt; in &lt;locations&gt;) - Shed/Barn/Coop/cabins carry
/// their own private interior location instance right here instead.
///
/// Field shape derived from decompiled StardewValley.GameLocation/DecoratableLocation/Shed's
/// [XmlIgnore]/[XmlElement] attributes (.reference/StardewValleyDecompiled), cross-checked
/// field-by-field against a real save's top-level &lt;GameLocation xsi:type="Farm"&gt; (which
/// shares the same GameLocation base) - every field that decompiled source predicts should be
/// serialized was present, and every [XmlIgnore] field was absent, confirming this derivation
/// technique for a type (Shed) with no real placed-Shed save evidence available.
///
/// Per decompiled Building.load(), only some of these fields are actually read back out of the
/// deserialized value before it's replaced with a freshly-constructed indoor location (built
/// from Data/Buildings.json's own IndoorMap, not from anything here) - the rest just need to be
/// present and correctly typed so deserialization itself doesn't throw. Scope is Shed and
/// AnimalHouse (Barn/Coop and their upgrade tiers), not Cabin (FarmHouse's much larger field
/// surface).
/// </summary>
internal static class BuildingIndoorsEditor
{
    private static readonly XName XsiType = XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance");

    /// <summary>Every GameLocation+DecoratableLocation field common to every interior type this
    /// tool writes (confirmed serialized, none [XmlIgnore], via the real Farm-location field-by-
    /// field cross-check described in the class remarks) - shared by CreateDefaultShed and
    /// CreateDefaultAnimalHouse so the ~20-field list isn't duplicated between them.
    /// <paramref name="name"/> is the location's own real map name (e.g. "Shed"/"Barn"/"Coop2" -
    /// Data/Buildings.json's IndoorMap value for this building type, NOT always the building type
    /// itself - Big Shed's IndoorMap is "Shed2", for instance). <paramref name="uniqueName"/>
    /// mirrors Building.load()'s own fallback formula (IndoorMap + tileX*2000 + tileY) - set
    /// defensively even though the real game re-derives it if absent, same rationale as
    /// AddFarmhouse's nonInstancedIndoorsName.</summary>
    private static IEnumerable<XElement> CommonFields(string name, string uniqueName) =>
    [
        new XElement("buildings"),
        new XElement("animals"),
        new XElement("piecesOfHay", 0),
        new XElement("characters"),
        new XElement("objects"),
        new XElement("resourceClumps"),
        new XElement("largeTerrainFeatures"),
        new XElement("terrainFeatures"),
        new XElement("uniqueName", uniqueName),
        new XElement("name", name),
        BuildWaterColor(),
        new XElement("isFarm", false),
        new XElement("isOutdoors", false),
        new XElement("isStructure", true),
        new XElement("ignoreDebrisWeather", false),
        new XElement("ignoreOutdoorLighting", false),
        new XElement("ignoreLights", false),
        new XElement("treatAsOutdoors", false),
        new XElement("numberOfSpawnedObjectsOnMap", 0),
        new XElement("miniJukeboxCount", 0),
        new XElement("miniJukeboxTrack", ""),
        new XElement("furniture"),
        // DecoratableLocation additions - wallpaperIDs/floorIDs are [XmlIgnore] (re-derived from
        // the map on load), skip them.
        new XElement("appliedWallpaper"),
        new XElement("appliedFloor"),
    ];

    /// <summary>The confirmed-safe default shape for a freshly-placed, completely empty Shed
    /// interior. <paramref name="position"/> is the owning Building's exterior tile position -
    /// see CommonFields remarks for the uniqueName formula it feeds.</summary>
    internal static XElement CreateDefaultShed(TilePosition position)
    {
        var uniqueName = "Shed" + (position.X * 2000 + position.Y);
        return new XElement("indoors",
            new XAttribute(XsiType, "Shed"),
            CommonFields("Shed", uniqueName),
            // Shed's own addition.
            new XElement("upgradeLevel", 0));
    }

    /// <summary>The confirmed-safe default shape for a freshly-placed, completely empty Barn/Coop
    /// (or upgrade tier) interior - real class is AnimalHouse for all six building types (Barn,
    /// Big Barn, Deluxe Barn, Coop, Big Coop, Deluxe Coop): decompiled Building.createIndoors does
    /// `Type.GetType(data.IndoorMapType)` where IndoorMapType is literally "StardewValley.
    /// AnimalHouse" for all six - there's no per-tier subclass. <paramref name="mapName"/> is the
    /// building's own Data/Buildings.json IndoorMap value (Barn/Barn2/Barn3/Coop/Coop2/Coop3 -
    /// NOT the building type name). <paramref name="animalLimit"/> is the building's own
    /// MaxOccupants (already read by PlaceableBuildings.cs). AnimalHouse's only OWN fields beyond
    /// GameLocation+DecoratableLocation are animalLimit and animalsThatLiveHere (both confirmed
    /// serialized, no [XmlIgnore], via decompiled AnimalHouse.cs) - empty/zero-equivalent here
    /// since nothing lives here yet.</summary>
    internal static XElement CreateDefaultAnimalHouse(string mapName, int animalLimit, TilePosition position)
    {
        var uniqueName = mapName + (position.X * 2000 + position.Y);
        return new XElement("indoors",
            new XAttribute(XsiType, "AnimalHouse"),
            CommonFields(mapName, uniqueName),
            new XElement("animalLimit", animalLimit),
            new XElement("animalsThatLiveHere"));
    }

    /// <summary>SetChildColorCreateIfMissing builds a "waterColor" CHILD under whatever element
    /// it's called on - build it under a throwaway wrapper, then detach and return just the
    /// child, so it can be spliced directly into the indoors element's own constructor list.</summary>
    private static XElement BuildWaterColor()
    {
        var wrapper = new XElement("wrapper");
        wrapper.SetChildColorCreateIfMissing("waterColor", 255, 255, 255, 255);
        var waterColor = wrapper.Element("waterColor")!;
        waterColor.Remove();
        return waterColor;
    }
}
