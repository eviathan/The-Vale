using System.Collections.Generic;
using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// General-purpose heldObject builder for "load an item into a machine" (Crystalarium's mineral,
/// a Furnace/Keg/Preserves Jar/etc.'s output, ...) - real Object.heldObject is a plain field on
/// every Object/BigCraftable, not something machines opt into individually, so unlike
/// SprinklerAttachments (deliberately constrained to the 2 real items a sprinkler can hold) this
/// accepts any real item's own data, supplied by the caller (see PlaceableItem in the app layer -
/// this Core-layer type can't reference that directly, so it takes primitives instead).
/// </summary>
public static class HeldItemBuilder
{
    public static IEnumerable<XElement> Create(string name, int parentSheetIndex, int price, int edibility, int category, string type, bool bigCraftable, string? itemId = null)
        => ObjectXmlBuilder.Fields(name, parentSheetIndex, price, edibility, category, type, bigCraftable, stack: 1, tileX: 0, tileY: 0, itemId: itemId);
}
