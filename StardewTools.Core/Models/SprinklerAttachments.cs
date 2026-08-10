using System.Collections.Generic;
using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// The two real sprinkler attachments (decompiled Object.cs's own drop-in check: `dropIn.
/// QualifiedItemId == "(O)915" || dropIn.QualifiedItemId == "(O)913"`, applied via the
/// sprinkler's own heldObject field - see ItemEditor.SetHeldObject) - Pressure Nozzle (+1
/// watering radius, GetModifiedRadiusForSprinkler) and Enricher (crop quality boost, no radius
/// change). Both are plain (non-bigCraftable) Objects, real Data/Objects.json Price/Type/
/// Edibility values.
/// </summary>
public static class SprinklerAttachments
{
    public const string PressureNozzleId = "915";
    public const string EnricherId = "913";

    public static IEnumerable<XElement> CreatePressureNozzle() => Build("Pressure Nozzle", 915);
    public static IEnumerable<XElement> CreateEnricher() => Build("Enricher", 913);

    private static IEnumerable<XElement> Build(string name, int parentSheetIndex)
        => ObjectXmlBuilder.Fields(name, parentSheetIndex, price: 200, edibility: -300, category: 0, type: "Basic", bigCraftable: false, stack: 1, tileX: 0, tileY: 0);
}
