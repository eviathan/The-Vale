using System.Xml.Linq;

namespace StardewTools.Core.Serialization;

/// <summary>
/// Extra fields for the placeable Object subclasses beyond Chest (see ChestXmlBuilder) that also
/// need a distinct xsi:type - Cask, Item Pedestal, Sign, Garden/Indoor Pot, Crab Pot, and Fence.
/// Field lists sourced from real save XML where any exists (Cask, Item Pedestal), decompiled
/// NetField declarations otherwise (see the research notes on each builder) - never guessed.
/// Optional NetRef fields the game only ever populates through gameplay (a cask's aged item, a
/// pedestal's required item, a sign's displayed item, a pot's planted bush, a crab pot's bait)
/// are omitted entirely for a freshly-placed instance, matching the real game's own
/// null-NetRef-is-omitted serialization rule.
/// </summary>
internal static class CaskXmlBuilder
{
    /// <summary>Real save evidence: 165 instances across all 4 local saves, 100% consistent.</summary>
    public static IEnumerable<XElement> Fields()
    {
        yield return new XElement("agingRate", 0);
        yield return new XElement("daysToMature", 0);
    }
}

internal static class ItemPedestalXmlBuilder
{
    /// <summary>Real save evidence (Ginger Island shrine pedestals, Brian_444995569) confirms
    /// field order; isIslandShrinePedestal is false here since this is the plain craftable
    /// variant, not a shrine.</summary>
    public static IEnumerable<XElement> Fields()
    {
        yield return new XElement("successColor", new XElement("B", 0), new XElement("G", 0), new XElement("R", 0), new XElement("A", 0), new XElement("PackedValue", 0));
        yield return new XElement("lockOnSuccess", false);
        yield return new XElement("locked", false);
        yield return new XElement("match", false);
        yield return new XElement("isIslandShrinePedestal", false);
    }
}

internal static class SignXmlBuilder
{
    /// <summary>Decompiled only (Sign.cs) - no real save instance exists locally. displayType=0
    /// means no item/hat/ring/furniture currently displayed on the sign.</summary>
    public static IEnumerable<XElement> Fields()
    {
        yield return new XElement("displayType", 0);
    }
}

internal static class IndoorPotXmlBuilder
{
    /// <summary>Decompiled only (IndoorPot.cs/HoeDirt.cs) - no real save instance exists locally.
    /// Unlike every other exotic type here, hoeDirt is never null (the constructor always
    /// creates one), so a freshly-placed Garden Pot needs the full nested HoeDirt element, not
    /// an omission.</summary>
    public static IEnumerable<XElement> Fields()
    {
        yield return new XElement("hoeDirt",
            new XElement("state", 0),
            new XElement("c", new XElement("B", 255), new XElement("G", 255), new XElement("R", 255), new XElement("A", 255), new XElement("PackedValue", 4294967295)));
    }
}

internal static class CrabPotXmlBuilder
{
    /// <summary>Decompiled only (CrabPot.cs) - no real save instance exists locally. Not a
    /// bigCraftable (uses the plain Object ctor) and canBeGrabbed is explicitly false in the
    /// real constructor - see ObjectXmlBuilder.Fields' canBeGrabbed parameter.</summary>
    public static IEnumerable<XElement> Fields()
    {
        yield return new XElement("directionOffset", new XElement("X", 0), new XElement("Y", 0));
    }
}

internal static class FenceXmlBuilder
{
    /// <summary>Decompiled only (Fence.cs) - no real save instance exists locally. health/
    /// maxHealth use each material's real Data/Fences.json base value (28/60/125/280/100 for Wood/
    /// Stone/Iron/Hardwood/Gate) times Fence.globalHealthMultiplier (2), with no random jitter (the
    /// real game adds +/-1.0 before the multiplier, randomized per-instance - a fixed baseline
    /// is used here instead of guessing at a specific roll). isGate is real, itemId-driven state -
    /// decompiled Object.placementAction sets it via `base.ItemId == "325"` (the Gate item), not a
    /// separate user choice - so it's derived here the same way, not always false. gateMotion is
    /// NOT actually serialized by the real game (Fence.cs's own field has no [XmlElement] - it's a
    /// per-frame transient), so it's deliberately not written here either.</summary>
    public static IEnumerable<XElement> Fields(int parentSheetIndex)
    {
        var health = BaseHealth(parentSheetIndex) * 2;
        yield return new XElement("health", health);
        yield return new XElement("maxHealth", health);
        yield return new XElement("gatePosition", 0);
        yield return new XElement("isGate", parentSheetIndex == 325);
    }

    private static int BaseHealth(int parentSheetIndex) => parentSheetIndex switch
    {
        322 => 28,  // Wood Fence
        323 => 60,  // Stone Fence
        324 => 125, // Iron Fence
        298 => 280, // Hardwood Fence
        325 => 100, // Gate
        _ => 28,
    };
}
