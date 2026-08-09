using System.Xml.Linq;

namespace StardewTools.Core.Serialization;

internal sealed class ExoticObjectSpec
{
    public required string XsiTypeName { get; init; }
    public bool CanBeGrabbed { get; init; } = true;
    public bool IncludeQuestId { get; init; }
    public required Func<int, IEnumerable<XElement>> ExtraFields { get; init; }
}

/// <summary>
/// The Object subclasses placeable through the Map tool that need a distinct xsi:type but aren't
/// Chest-family (see FarmMapEditor.ChestVariant) and aren't Auto-Grabber (see
/// FarmMapEditor.AddAutoGrabber). Real Data/Objects.json + Data/BigCraftables.json ids, confirmed
/// against decompiled StardewValley.Objects.* source (and real save XML where any exists - Cask,
/// Item Pedestal, Torch) as each needing a distinct C# class at runtime, not just a plain Object.
///
/// Keyed by (Index, IsBigCraftable), not Index alone - confirmed by direct cross-reference of
/// both JSON files that Objects.json and BigCraftables.json are genuinely independent id spaces
/// that collide numerically far more often than assumed (e.g. id 211 is "Wood Chipper" in
/// BigCraftables but plain "Pancakes" in Objects; id 94 is "Spirit Torch" in Objects but "Singing
/// Stone" - an unrelated item not modeled here at all - in BigCraftables). The original
/// int-only-keyed version of this catalog would have silently mis-rendered every one of the 8
/// affected non-bigCraftable foods/fish as its colliding BigCraftable entry's xsi:type the moment
/// a user placed one - a real, confirmed latent bug found and fixed 2026-08-08 while
/// cross-referencing the id space for an unrelated (Big Chest) fix.
/// </summary>
internal static class ExoticObjectCatalog
{
    private static readonly Dictionary<(int Index, bool IsBigCraftable), ExoticObjectSpec> ById = new()
    {
        [(163, true)] = new ExoticObjectSpec { XsiTypeName = "Cask", IncludeQuestId = true, ExtraFields = _ => CaskXmlBuilder.Fields() },
        [(221, true)] = new ExoticObjectSpec { XsiTypeName = "ItemPedestal", ExtraFields = _ => ItemPedestalXmlBuilder.Fields() },
        [(93, false)] = new ExoticObjectSpec { XsiTypeName = "Torch", ExtraFields = _ => Enumerable.Empty<XElement>() },
        [(94, false)] = new ExoticObjectSpec { XsiTypeName = "Torch", ExtraFields = _ => Enumerable.Empty<XElement>() },
        [(37, true)] = new ExoticObjectSpec { XsiTypeName = "Sign", ExtraFields = _ => SignXmlBuilder.Fields() },
        [(38, true)] = new ExoticObjectSpec { XsiTypeName = "Sign", ExtraFields = _ => SignXmlBuilder.Fields() },
        [(39, true)] = new ExoticObjectSpec { XsiTypeName = "Sign", ExtraFields = _ => SignXmlBuilder.Fields() },
        [(62, true)] = new ExoticObjectSpec { XsiTypeName = "IndoorPot", ExtraFields = _ => IndoorPotXmlBuilder.Fields() },
        [(208, true)] = new ExoticObjectSpec { XsiTypeName = "Workbench", ExtraFields = _ => Enumerable.Empty<XElement>() },
        [(209, true)] = new ExoticObjectSpec { XsiTypeName = "MiniJukebox", ExtraFields = _ => Enumerable.Empty<XElement>() },
        [(211, true)] = new ExoticObjectSpec { XsiTypeName = "WoodChipper", ExtraFields = _ => Enumerable.Empty<XElement>() },
        [(214, true)] = new ExoticObjectSpec { XsiTypeName = "Phone", ExtraFields = _ => Enumerable.Empty<XElement>() },
        [(710, false)] = new ExoticObjectSpec { XsiTypeName = "CrabPot", CanBeGrabbed = false, ExtraFields = _ => CrabPotXmlBuilder.Fields() },
        [(322, false)] = new ExoticObjectSpec { XsiTypeName = "Fence", ExtraFields = FenceXmlBuilder.Fields },
        [(323, false)] = new ExoticObjectSpec { XsiTypeName = "Fence", ExtraFields = FenceXmlBuilder.Fields },
        [(324, false)] = new ExoticObjectSpec { XsiTypeName = "Fence", ExtraFields = FenceXmlBuilder.Fields },
        [(298, false)] = new ExoticObjectSpec { XsiTypeName = "Fence", ExtraFields = FenceXmlBuilder.Fields },
        [(325, false)] = new ExoticObjectSpec { XsiTypeName = "Fence", ExtraFields = FenceXmlBuilder.Fields },
    };

    public static bool IsKnown(int parentSheetIndex, bool isBigCraftable) => ById.ContainsKey((parentSheetIndex, isBigCraftable));

    public static ExoticObjectSpec Lookup(int parentSheetIndex, bool isBigCraftable) => ById[(parentSheetIndex, isBigCraftable)];
}
