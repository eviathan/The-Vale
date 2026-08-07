using System.Xml.Linq;

namespace StardewTools.Core.Serialization;

/// <summary>
/// Chest's own extra fields, appended after ObjectXmlBuilder.Fields (bigCraftable: true) - every
/// field, in this order, copied from a real placed "Stone Chest" in an actual save. This is the
/// piece that was missing entirely before: placing a Chest through the generic Object tool wrote
/// none of this (not even the xsi:type="Chest" attribute), so the game loaded it as an inert
/// plain Object instead of a real Chest - confirmed via direct user report (uninteractable,
/// visually glitched "bobbing" placeholder, fixed in-game only by destroying it and building a
/// real one through the normal construction menu).
/// </summary>
internal static class ChestXmlBuilder
{
    /// <summary>
    /// currentLidFrame is parentSheetIndex + 1 for every Chest-family item, including the
    /// disguised ones (Mini-Fridge/Mini-Shipping Bin/Hopper) - confirmed against the real example
    /// (Stone Chest, parentSheetIndex 232, currentLidFrame 233) and, for the disguised variants,
    /// against the decompiled placement code in Object.cs (each one's starting_lid_frame argument
    /// is always its own parentSheetIndex + 1).
    /// <paramref name="lidFrameCount"/>/<paramref name="fridge"/>/<paramref name="specialChestType"/>
    /// default to a plain Chest/Stone Chest/Junimo Chest's values; Mini-Fridge, Mini-Shipping Bin,
    /// and Hopper are also real Chest instances under the hood (per Object.cs's placement code)
    /// but each overrides one of these three - see FarmMapEditor's chest-variant table.
    /// </summary>
    public static IEnumerable<XElement> Fields(int parentSheetIndex, int lidFrameCount = 5, bool fridge = false, string specialChestType = "None", bool playerChest = true)
    {
        yield return new XElement("currentLidFrame", parentSheetIndex + 1);
        yield return new XElement("lidFrameCount", new XElement("int", lidFrameCount));
        yield return new XElement("frameCounter", -1);
        yield return new XElement("items");
        yield return new XElement("separateWalletItems", new XElement("SerializableDictionaryOfInt64Inventory"));
        yield return new XElement("tint", new XElement("B", 255), new XElement("G", 255), new XElement("R", 255), new XElement("A", 255), new XElement("PackedValue", 4294967295));
        yield return new XElement("playerChoiceColor", new XElement("B", 0), new XElement("G", 0), new XElement("R", 0), new XElement("A", 255), new XElement("PackedValue", 4278190080));
        yield return new XElement("playerChest", playerChest);
        yield return new XElement("fridge", fridge);
        yield return new XElement("giftbox", false);
        yield return new XElement("giftboxIndex", 0);
        yield return new XElement("giftboxIsStarterGift", new XElement("boolean", false));
        yield return new XElement("spriteIndexOverride", -1);
        yield return new XElement("dropContents", false);
        yield return new XElement("synchronized", false);
        yield return new XElement("specialChestType", specialChestType);
        var globalInventoryId = new XElement("globalInventoryId", new XElement("string"));
        globalInventoryId.Element("string")!.SetAttributeValue(XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance"), "true");
        yield return globalInventoryId;
    }
}
