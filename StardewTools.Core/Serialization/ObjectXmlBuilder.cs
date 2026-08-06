using System.Xml.Linq;

namespace StardewTools.Core.Serialization;

/// <summary>
/// Builds the field list for a brand-new plain Object (or bigCraftable machine/decoration) -
/// every field, in this order, copied from a real placed Object in an actual save, not
/// invented. Shared by FarmMapEditor.AddObject (a farm-placed &lt;Object&gt;, tile-positioned)
/// and ItemListEditor.AddNew (a carried &lt;Item xsi:type="Object"&gt;, position zeroed) -
/// confirmed via a real inventory item (a carried "Fiber" stack) that both contexts use this
/// exact same field set, differing only in the wrapping element/attribute and whether
/// tileLocation/boundingBox are tile-positioned or zeroed.
/// </summary>
internal static class ObjectXmlBuilder
{
    public static IEnumerable<XElement> Fields(string name, int parentSheetIndex, int price, int edibility, int category, string type, bool bigCraftable, int stack, int tileX, int tileY)
    {
        var boundsX = tileX * 64;
        var boundsY = tileY * 64;

        yield return new XElement("isLostItem", false);
        yield return new XElement("category", category);
        yield return new XElement("hasBeenInInventory", false);
        yield return new XElement("name", name);
        yield return new XElement("parentSheetIndex", parentSheetIndex);
        yield return new XElement("specialItem", false);
        yield return new XElement("SpecialVariable", 0);
        yield return new XElement("DisplayName", name);
        yield return new XElement("Name", name);
        yield return new XElement("Stack", stack);
        yield return new XElement("tileLocation", new XElement("X", tileX), new XElement("Y", tileY));
        yield return new XElement("owner", 0);
        yield return new XElement("type", type);
        yield return new XElement("canBeSetDown", true);
        yield return new XElement("canBeGrabbed", true);
        yield return new XElement("isHoedirt", false);
        yield return new XElement("isSpawnedObject", false);
        yield return new XElement("questItem", false);
        yield return new XElement("questId", 0);
        yield return new XElement("isOn", true);
        yield return new XElement("fragility", 0);
        yield return new XElement("price", price);
        yield return new XElement("edibility", edibility);
        yield return new XElement("stack", stack);
        yield return new XElement("quality", 0);
        yield return new XElement("bigCraftable", bigCraftable);
        yield return new XElement("setOutdoors", false);
        yield return new XElement("setIndoors", false);
        yield return new XElement("readyForHarvest", false);
        yield return new XElement("showNextIndex", false);
        yield return new XElement("flipped", false);
        yield return new XElement("hasBeenPickedUpByFarmer", false);
        yield return new XElement("isRecipe", false);
        yield return new XElement("isLamp", false);
        yield return new XElement("minutesUntilReady", 1);
        yield return new XElement("boundingBox",
            new XElement("X", boundsX), new XElement("Y", boundsY),
            new XElement("Width", 64), new XElement("Height", 64),
            new XElement("Location", new XElement("X", boundsX), new XElement("Y", boundsY)),
            new XElement("Size", new XElement("X", 64), new XElement("Y", 64)));
        yield return new XElement("scale", new XElement("X", 0), new XElement("Y", 0));
        yield return new XElement("uses", 0);
        yield return new XElement("preservedParentSheetIndex", 0);
    }
}
