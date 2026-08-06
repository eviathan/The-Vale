using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// Typed access over a container of &lt;Item&gt; elements - a player's inventory or a
/// chest's contents share this exact shape. Supports editing, removing, and duplicating
/// existing items, plus fabricating a brand-new plain Object (see AddNew) - but not other
/// item classes (Furniture, Rings, Tools, ...): a correct &lt;Item xsi:type="..."&gt; blob
/// varies by item class, and we only have a verified real example (a carried "Fiber" stack)
/// for the plain-Object shape, not every type. Duplicating an item already in the list
/// sidesteps that entirely for other types: it's always exactly as valid as the item it was
/// copied from.
/// </summary>
public sealed class ItemListEditor
{
    private static readonly XName XsiNil = XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance");
    private static readonly XName XsiType = XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance");

    private readonly XElement _container;

    public ItemListEditor(XElement container)
    {
        _container = container;
    }

    /// <summary>
    /// Real items only. Empty inventory/chest slots are serialized as &lt;Item xsi:nil="true" /&gt;
    /// (a fixed-size slot with nothing in it) and are filtered out here.
    /// </summary>
    public IReadOnlyList<ItemEditor> Items
        => _container.Elements("Item")
            .Where(e => (string?)e.Attribute(XsiNil) != "true")
            .Select(e => new ItemEditor(e))
            .ToList();

    public void Remove(ItemEditor item) => item.Element.Remove();

    /// <summary>
    /// Clones <paramref name="source"/> and inserts the copy with the given stack size.
    /// Prefers reusing an existing empty (xsi:nil) slot over appending a new element, so a
    /// fixed-capacity inventory doesn't grow past its real size. Returns null if the
    /// container has no empty slot to reuse and appending isn't safe to assume (chests,
    /// which aren't capacity-limited the same way, always append).
    /// </summary>
    public ItemEditor AddCopy(ItemEditor source, int stack)
    {
        var clone = new XElement(source.Element);
        InsertOrAppend(clone);

        var added = new ItemEditor(clone);
        added.Stack = stack;
        return added;
    }

    /// <summary>
    /// Fabricates a brand-new plain Object item (see class remarks - not every item class is
    /// supported) and inserts it, e.g. depositing a resource clump's real mining yield into
    /// the player's inventory. name/price/edibility/category/type should come from real
    /// Data/Objects.json or Data/BigCraftables.json data (see PlaceableItems in the app layer),
    /// same caution as FarmMapEditor.AddObject.
    /// </summary>
    public ItemEditor AddNew(int parentSheetIndex, string name, int price, int edibility, int category, string type, int stack)
    {
        var element = new XElement("Item", new XAttribute(XsiType, "Object"),
            ObjectXmlBuilder.Fields(name, parentSheetIndex, price, edibility, category, type, bigCraftable: false, stack, tileX: 0, tileY: 0));

        InsertOrAppend(element);
        return new ItemEditor(element);
    }

    /// <summary>Prefers reusing an existing empty (xsi:nil) slot over appending a new element,
    /// so a fixed-capacity inventory doesn't grow past its real size; chests (not capacity-
    /// limited the same way) just always append since they have no empty-slot placeholders.</summary>
    private void InsertOrAppend(XElement element)
    {
        var emptySlot = _container.Elements("Item")
            .FirstOrDefault(e => (string?)e.Attribute(XsiNil) == "true");

        if (emptySlot is not null)
            emptySlot.ReplaceWith(element);
        else
            _container.Add(element);
    }
}
