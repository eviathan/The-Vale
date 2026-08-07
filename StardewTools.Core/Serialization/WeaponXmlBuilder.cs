using System.Xml.Linq;

namespace StardewTools.Core.Serialization;

/// <summary>
/// Builds the field list for a brand-new MeleeWeapon item (swords/daggers/clubs/scythes all
/// share this exact shape - only the "type" field distinguishes the weapon subtype) - every
/// field, in this order, copied from a real carried Scythe in an actual save.
/// </summary>
internal static class WeaponXmlBuilder
{
    public static IEnumerable<XElement> Fields(string name, int spriteIndex, int type, int minDamage, int maxDamage,
        int speed, int precision, int defense, int areaOfEffect, double knockback, double critChance, double critMultiplier)
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
        yield return new XElement("indexOfMenuItemView", spriteIndex);
        yield return new XElement("stackable", false);
        yield return new XElement("instantUse", false);
        yield return new XElement("upgradeLevel", 0);
        yield return new XElement("numAttachmentSlots", 0);
        yield return new XElement("attachments");
        yield return new XElement("BaseName", name);
        yield return new XElement("InitialParentTileIndex", spriteIndex);
        yield return new XElement("IndexOfMenuItemView", spriteIndex);
        yield return new XElement("InstantUse", false);
        yield return new XElement("Stackable", false);
        yield return new XElement("type", type);
        yield return new XElement("minDamage", minDamage);
        yield return new XElement("maxDamage", maxDamage);
        yield return new XElement("speed", speed);
        yield return new XElement("addedPrecision", precision);
        yield return new XElement("addedDefense", defense);
        yield return new XElement("addedAreaOfEffect", areaOfEffect);
        yield return new XElement("knockback", knockback);
        yield return new XElement("critChance", critChance);
        yield return new XElement("critMultiplier", critMultiplier);
        yield return new XElement("isOnSpecial", false);
    }
}
