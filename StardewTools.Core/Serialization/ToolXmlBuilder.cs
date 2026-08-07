using System.Xml.Linq;

namespace StardewTools.Core.Serialization;

/// <summary>
/// Builds the field list for a brand-new Tool item (Axe/Hoe/Pickaxe/WateringCan) - every field,
/// in this order, copied from 4 real carried tools in an actual save (a starting Axe/Hoe/
/// Pickaxe/WateringCan). MilkPail/Pan/Shears weren't included (their only extra decompiled
/// field, finishEvent, is a NetEvent trigger rather than persisted state, but there's no real
/// save example to confirm that against, so they're left as a documented gap rather than a
/// guess). FishingRod has real extra persisted fields (bobber, castDirection, ...) with no real
/// example available either - deliberately not attempted here at all.
/// </summary>
internal static class ToolXmlBuilder
{
    public static IEnumerable<XElement> Fields(string name, int spriteIndex, int menuSpriteIndex, int upgradeLevel)
    {
        yield return new XElement("isLostItem", false);
        yield return new XElement("category", -99);
        yield return new XElement("hasBeenInInventory", false);
        yield return new XElement("name", name);
        yield return new XElement("specialItem", false);
        yield return new XElement("SpecialVariable", 0);
        yield return new XElement("DisplayName", name);
        yield return new XElement("Name", name);
        yield return new XElement("Stack", 1);
        yield return new XElement("initialParentTileIndex", spriteIndex);
        yield return new XElement("currentParentTileIndex", spriteIndex);
        yield return new XElement("indexOfMenuItemView", menuSpriteIndex);
        yield return new XElement("stackable", false);
        yield return new XElement("instantUse", false);
        yield return new XElement("upgradeLevel", upgradeLevel);
        yield return new XElement("numAttachmentSlots", 0);
        yield return new XElement("attachments");
        yield return new XElement("BaseName", name);
        yield return new XElement("InitialParentTileIndex", spriteIndex);
        yield return new XElement("IndexOfMenuItemView", menuSpriteIndex);
        yield return new XElement("InstantUse", false);
        yield return new XElement("Stackable", false);
    }

    /// <summary>WateringCan's own two extra fields beyond the base Tool shape, appended after
    /// <see cref="Fields"/> - confirmed against the real save's starting Watering Can
    /// (waterCanMax/WaterLeft both 40). Real per-tier max capacity (Copper/Steel/Gold/Iridium
    /// hold more) isn't confirmed against a real save at any tier but the base one, so every
    /// tier fabricated here gets the same base capacity rather than a guessed scaled value.</summary>
    public static IEnumerable<XElement> WateringCanFields(int waterCanMax = 40)
    {
        yield return new XElement("waterCanMax", waterCanMax);
        yield return new XElement("WaterLeft", waterCanMax);
    }
}
