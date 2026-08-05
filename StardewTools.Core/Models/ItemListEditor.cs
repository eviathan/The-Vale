using System.Xml.Linq;

namespace StardewTools.Core.Models;

/// <summary>
/// Typed access over a container of &lt;Item&gt; elements - a player's inventory or a
/// chest's contents share this exact shape. Supports editing, removing, and duplicating
/// existing items. Deliberately doesn't support fabricating a brand-new item type from
/// scratch (e.g. "give me an Iridium Ore I've never had") - a correct &lt;Item
/// xsi:type="..."&gt; blob varies by item class, and we don't have a verified real example
/// of every type to build one from safely. Duplicating an item already in the list sidesteps
/// that entirely: it's always exactly as valid as the item it was copied from.
/// </summary>
public sealed class ItemListEditor
{
    private static readonly XName XsiNil = XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance");

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

        var emptySlot = _container.Elements("Item")
            .FirstOrDefault(e => (string?)e.Attribute(XsiNil) == "true");

        if (emptySlot is not null)
            emptySlot.ReplaceWith(clone);
        else
            _container.Add(clone);

        var added = new ItemEditor(clone);
        added.Stack = stack;
        return added;
    }
}
