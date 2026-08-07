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
/// </summary>
internal static class ExoticObjectCatalog
{
    private static readonly Dictionary<int, ExoticObjectSpec> ById = new()
    {
        [163] = new ExoticObjectSpec { XsiTypeName = "Cask", IncludeQuestId = true, ExtraFields = _ => CaskXmlBuilder.Fields() },
        [221] = new ExoticObjectSpec { XsiTypeName = "ItemPedestal", ExtraFields = _ => ItemPedestalXmlBuilder.Fields() },
        [93] = new ExoticObjectSpec { XsiTypeName = "Torch", ExtraFields = _ => Enumerable.Empty<XElement>() },
        [94] = new ExoticObjectSpec { XsiTypeName = "Torch", ExtraFields = _ => Enumerable.Empty<XElement>() },
        [37] = new ExoticObjectSpec { XsiTypeName = "Sign", ExtraFields = _ => SignXmlBuilder.Fields() },
        [38] = new ExoticObjectSpec { XsiTypeName = "Sign", ExtraFields = _ => SignXmlBuilder.Fields() },
        [39] = new ExoticObjectSpec { XsiTypeName = "Sign", ExtraFields = _ => SignXmlBuilder.Fields() },
        [62] = new ExoticObjectSpec { XsiTypeName = "IndoorPot", ExtraFields = _ => IndoorPotXmlBuilder.Fields() },
        [208] = new ExoticObjectSpec { XsiTypeName = "Workbench", ExtraFields = _ => Enumerable.Empty<XElement>() },
        [209] = new ExoticObjectSpec { XsiTypeName = "MiniJukebox", ExtraFields = _ => Enumerable.Empty<XElement>() },
        [211] = new ExoticObjectSpec { XsiTypeName = "WoodChipper", ExtraFields = _ => Enumerable.Empty<XElement>() },
        [214] = new ExoticObjectSpec { XsiTypeName = "Phone", ExtraFields = _ => Enumerable.Empty<XElement>() },
        [710] = new ExoticObjectSpec { XsiTypeName = "CrabPot", CanBeGrabbed = false, ExtraFields = _ => CrabPotXmlBuilder.Fields() },
        [322] = new ExoticObjectSpec { XsiTypeName = "Fence", ExtraFields = FenceXmlBuilder.Fields },
        [323] = new ExoticObjectSpec { XsiTypeName = "Fence", ExtraFields = FenceXmlBuilder.Fields },
        [324] = new ExoticObjectSpec { XsiTypeName = "Fence", ExtraFields = FenceXmlBuilder.Fields },
        [298] = new ExoticObjectSpec { XsiTypeName = "Fence", ExtraFields = FenceXmlBuilder.Fields },
    };

    public static bool IsKnown(int parentSheetIndex) => ById.ContainsKey(parentSheetIndex);

    public static ExoticObjectSpec Lookup(int parentSheetIndex) => ById[parentSheetIndex];
}
