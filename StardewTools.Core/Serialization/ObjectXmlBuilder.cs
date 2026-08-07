using System.Xml.Linq;

namespace StardewTools.Core.Serialization;

/// <summary>
/// Builds the field list for a brand-new plain Object (or bigCraftable machine/decoration) -
/// every field, in this order, copied from real examples in an actual, actively-played save
/// (a carried "Fiber" stack and a placed "Stone Chest"), not invented.
///
/// This shape replaced an earlier one built against a different, older real save (a fresh
/// ~2-day-old one) - the two differ meaningfully: this one has no DisplayName/Name/Stack
/// PascalCase duplicates, no isHoedirt/questId/hasBeenPickedUpByFarmer/preservedParentSheetIndex,
/// and adds itemId (a modern duplicate of parentSheetIndex) and destroyOvernight, with
/// isRecipe/quality/stack moved earlier in the field order. Confirmed the OLD shape really is
/// outdated, not just a different in-memory ordering that round-trips the same either way: a
/// real chest placed via the old shape (missing xsi:type="Chest" and every Chest-specific field
/// entirely, since nothing here special-cased chests before) loaded in-game as an inert,
/// uninteractable, visually-glitched ("bobbing") plain Object - not a functional chest at all.
/// Shared by FarmMapEditor.AddObject/AddChest (farm-placed, tile-positioned) and
/// ItemListEditor.AddNew (carried, position zeroed).
/// </summary>
internal static class ObjectXmlBuilder
{
    /// <summary>
    /// <paramref name="includeQuestId"/>: every real placed Cask (165 instances across all 4
    /// local saves) has a <c>questId</c> element right after <c>questItem</c> that plain Objects/
    /// Chests in the same saves don't have - cause unconfirmed (not touched by Cask's own code),
    /// but 100% reproducible, so it's included for Cask specifically rather than guessed away.
    /// <paramref name="heldObject"/>: Auto-Grabber's storage is its base-Object <c>heldObject</c>
    /// field (a nested Chest), not a Chest xsi:type of its own - confirmed via decompiled
    /// Object.cs placement code (<c>autoGrabber.heldObject.Value = new Chest()</c>). Declared
    /// between isLamp and minutesUntilReady to match Object.cs's real field declaration order.
    /// <paramref name="canBeGrabbed"/>: true for everything except Crab Pot, which the decompiled
    /// constructor explicitly overrides to false (it's picked up by re-baiting/interacting, not
    /// by the normal grab action).
    /// </summary>
    public static IEnumerable<XElement> Fields(string name, int parentSheetIndex, int price, int edibility, int category, string type, bool bigCraftable, int stack, int tileX, int tileY, bool includeQuestId = false, XElement? heldObject = null, bool canBeGrabbed = true, bool canBeSetDown = true)
    {
        var boundsX = tileX * 64;
        var boundsY = tileY * 64;

        yield return new XElement("isLostItem", false);
        yield return new XElement("category", category);
        yield return new XElement("hasBeenInInventory", false);
        yield return new XElement("name", name);
        yield return new XElement("parentSheetIndex", parentSheetIndex);
        yield return new XElement("itemId", parentSheetIndex);
        yield return new XElement("specialItem", false);
        yield return new XElement("isRecipe", false);
        yield return new XElement("quality", 0);
        yield return new XElement("stack", stack);
        yield return new XElement("SpecialVariable", 0);
        yield return new XElement("tileLocation", new XElement("X", tileX), new XElement("Y", tileY));
        yield return new XElement("owner", 0);
        yield return new XElement("type", type);
        yield return new XElement("canBeSetDown", canBeSetDown);
        yield return new XElement("canBeGrabbed", canBeGrabbed);
        yield return new XElement("isSpawnedObject", false);
        yield return new XElement("questItem", false);
        if (includeQuestId)
            yield return new XElement("questId", 0);
        yield return new XElement("isOn", true);
        yield return new XElement("fragility", 0);
        yield return new XElement("price", price);
        yield return new XElement("edibility", edibility);
        yield return new XElement("bigCraftable", bigCraftable);
        yield return new XElement("setOutdoors", false);
        yield return new XElement("setIndoors", false);
        yield return new XElement("readyForHarvest", false);
        yield return new XElement("showNextIndex", false);
        yield return new XElement("flipped", false);
        yield return new XElement("isLamp", false);
        if (heldObject is not null)
            yield return heldObject;
        yield return new XElement("minutesUntilReady", 0);
        yield return new XElement("boundingBox",
            new XElement("X", boundsX), new XElement("Y", boundsY),
            new XElement("Width", 64), new XElement("Height", 64),
            new XElement("Location", new XElement("X", boundsX), new XElement("Y", boundsY)),
            new XElement("Size", new XElement("X", 64), new XElement("Y", 64)));
        yield return new XElement("scale", new XElement("X", 0), new XElement("Y", 0));
        yield return new XElement("uses", 0);
        yield return new XElement("destroyOvernight", false);
    }
}
