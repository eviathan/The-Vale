using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// A farm animal living inside a Barn/Coop's &lt;indoors&gt; interior - the &lt;FarmAnimal&gt;
/// value of one entry in that GameLocation's &lt;animals&gt; NetLongDictionary. Field shape
/// derived from decompiled StardewValley.FarmAnimal's [XmlElement]/[XmlIgnore] attributes - no
/// real save evidence available (this tool couldn't place a Barn/Coop, let alone an animal, until
/// now), same derivation confidence as BuildingIndoorsEditor's own shapes. Character's own
/// `position`/`sprite`/most runtime state are [XmlIgnore] (re-derived on load, e.g. via
/// AnimalHouse.adoptAnimal's setRandomPosition) - not written here at all.
/// </summary>
public sealed class FarmAnimalEditor
{
    private readonly XElement _element;

    internal FarmAnimalEditor(XElement farmAnimalElement)
    {
        _element = farmAnimalElement;
    }

    /// <summary>The real animal-data id this animal is (e.g. "White Chicken") - defines identity,
    /// same read-only convention as CropEditor.SeedIndex/BuildingEditor.BuildingType.</summary>
    public string Type => _element.GetChildText("type");

    /// <summary>Character.name - the animal's own given name (distinct from Type).</summary>
    public string Name
    {
        get => _element.GetChildText("name");
        set => _element.SetChildText("name", value);
    }

    public long MyId => long.Parse(_element.GetChildText("myID"));

    public int Age { get => _element.GetChildInt("age"); set => _element.SetChildInt("age", value); }
    public int Health { get => _element.GetChildInt("health"); set => _element.SetChildInt("health", value); }
    public int FriendshipTowardFarmer { get => _element.GetChildInt("friendshipTowardFarmer"); set => _element.SetChildInt("friendshipTowardFarmer", value); }
    public int Happiness { get => _element.GetChildInt("happiness"); set => _element.SetChildInt("happiness", value); }
    public int Fullness { get => _element.GetChildInt("fullness"); set => _element.SetChildInt("fullness", value); }
    public bool AllowReproduction { get => _element.GetChildBool("allowReproduction"); set => _element.SetChildBool("allowReproduction", value); }

    /// <summary>The real minimal shape for a freshly-adopted animal - mirrors decompiled
    /// FarmAnimal(string type, long id, long ownerID)'s own field-by-field initialization
    /// (health=3, happiness=255, fullness=255; everything else left at Net* defaults, which
    /// round-trip safely since none of Character/FarmAnimal's own non-[XmlIgnore] fields are
    /// non-nullable-without-a-default). <paramref name="name"/> isn't randomized here (unlike the
    /// real game's Dialogue.randomName()) - left to the caller so a save-editor context can offer
    /// a real name picker instead of a random one.</summary>
    internal static XElement CreateDefault(string type, string name, long myId, long ownerId, string buildingTypeILiveIn) =>
        new("FarmAnimal",
            new XElement("name", name),
            new XElement("forceOneTileWide", false),
            new XElement("isEmoting", false),
            new XElement("isCharging", false),
            new XElement("isGlowing", false),
            new XElement("coloredBorder", false),
            new XElement("flip", false),
            new XElement("drawOnTop", false),
            new XElement("faceTowardFarmer", false),
            new XElement("ignoreMovementAnimation", false),
            new XElement("faceAwayFromFarmer", false),
            new XElement("scale", new XElement("float", 1)),
            new XElement("glowingTransparency", 0),
            new XElement("glowRate", 0),
            new XElement("Gender", "Undefined"),
            new XElement("willDestroyObjectsUnderfoot", true),
            new XElement("Position", new XElement("X", 0), new XElement("Y", 0)),
            new XElement("Speed", 0),
            new XElement("FacingDirection", 2),
            new XElement("IsEmoting", false),
            new XElement("CurrentEmote", 0),
            new XElement("Scale", 1),
            new XElement("isSwimming", new XElement("boolean", false)),
            new XElement("currentProduce", ""),
            new XElement("friendshipTowardFarmer", 0),
            new XElement("skinID", ""),
            new XElement("age", 0),
            new XElement("daysOwned", -1),
            new XElement("health", 3),
            new XElement("produceQuality", 0),
            new XElement("daysSinceLastLay", 0),
            new XElement("happiness", 255),
            new XElement("fullness", 255),
            new XElement("wasAutoPet", false),
            new XElement("wasPet", false),
            new XElement("allowReproduction", true),
            new XElement("type", type),
            new XElement("buildingTypeILiveIn", buildingTypeILiveIn),
            new XElement("myID", myId),
            new XElement("ownerID", ownerId),
            new XElement("parentId", -1),
            new XElement("hasEatenAnimalCracker", false),
            new XElement("moodMessage", 0),
            new XElement("isEating", false),
            new XElement("displayName", name));
}
