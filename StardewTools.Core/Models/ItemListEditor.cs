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

    /// <summary>Every &lt;Item&gt; child, real or empty, in save order - unlike <see cref="Items"/>,
    /// this preserves slot position (index N here is exactly Farmer.Items[N] / slot N in the
    /// game's inventory grid). Only meaningful for a fixed-size positional list like the player's
    /// own inventory - a chest's &lt;items&gt; has no xsi:nil placeholders at all (confirmed
    /// against a real save: items are packed tightly, no empty-slot markers), so every entry
    /// there is already "real" and position carries no meaning.</summary>
    public int SlotCount => _container.Elements("Item").Count();

    public ItemEditor? GetSlot(int index)
    {
        var element = _container.Elements("Item").ElementAtOrDefault(index);
        if (element is null || (string?)element.Attribute(XsiNil) == "true")
            return null;

        return new ItemEditor(element);
    }

    /// <summary>Clears a slot in place (replaces its content with an empty &lt;Item xsi:nil="true"
    /// /&gt;) rather than removing the &lt;Item&gt; element - the correct "remove" semantics for a
    /// fixed-size positional list (the player's inventory), where deleting the element outright
    /// would shift every later slot's index and silently shrink capacity below maxItems. Not used
    /// for chests - see <see cref="Remove"/> for that (a chest's list is tightly-packed and has
    /// no concept of "this slot", so removing the element is correct there).</summary>
    public void ClearSlot(int index)
    {
        var element = _container.Elements("Item").ElementAtOrDefault(index);
        if (element is null)
            return;

        var nil = new XElement("Item");
        nil.SetAttributeValue(XsiNil, "true");
        element.ReplaceWith(nil);
    }

    /// <summary>Places a brand-new plain Object at an exact slot (see <see cref="AddNew"/> for
    /// the same field-shape caveat - not every item class is supported), overwriting whatever was
    /// there (empty or occupied) rather than reusing/appending elsewhere. Throws if the index is
    /// out of range - callers should size against <see cref="SlotCount"/> first (e.g. only drop
    /// onto a real, unlocked grid cell).</summary>
    public ItemEditor SetSlotToNewObject(int index, int parentSheetIndex, string name, int price, int edibility, int category, string type, int stack)
    {
        var element = new XElement("Item", new XAttribute(XsiType, "Object"),
            ObjectXmlBuilder.Fields(name, parentSheetIndex, price, edibility, category, type, bigCraftable: false, stack, tileX: 0, tileY: 0));

        return ReplaceSlot(index, element);
    }

    /// <summary>Places a brand-new Axe/Hoe/Pickaxe/WateringCan at an exact slot (see
    /// ToolXmlBuilder - only these 4 classes have a verified real-example shape). classXsiType
    /// is the real save's own xsi:type value for the tool (e.g. "Axe", "WateringCan") - matches
    /// Data/Tools.json's ClassName field for the entry being fabricated.</summary>
    public ItemEditor SetSlotToNewTool(int index, string classXsiType, string name, int spriteIndex, int menuSpriteIndex, int upgradeLevel)
    {
        var fields = ToolXmlBuilder.Fields(name, spriteIndex, menuSpriteIndex, upgradeLevel);
        if (classXsiType == "WateringCan")
            fields = fields.Concat(ToolXmlBuilder.WateringCanFields());

        var element = new XElement("Item", new XAttribute(XsiType, classXsiType), fields);
        return ReplaceSlot(index, element);
    }

    /// <summary>Places a brand-new MeleeWeapon (sword/dagger/club/scythe) at an exact slot - see
    /// WeaponXmlBuilder. type distinguishes the weapon subtype (0=stabbing sword, 1=dagger,
    /// 2=club, 3=defense sword per the decompiled MeleeWeapon constants - not independently
    /// re-verified here, taken from Data/Weapons.json's own Type field for the entry).</summary>
    public ItemEditor SetSlotToNewWeapon(int index, string name, int spriteIndex, int type, int minDamage, int maxDamage,
        int speed, int precision, int defense, int areaOfEffect, double knockback, double critChance, double critMultiplier)
    {
        var element = new XElement("Item", new XAttribute(XsiType, "MeleeWeapon"),
            WeaponXmlBuilder.Fields(name, spriteIndex, type, minDamage, maxDamage, speed, precision, defense, areaOfEffect, knockback, critChance, critMultiplier));

        return ReplaceSlot(index, element);
    }

    private ItemEditor ReplaceSlot(int index, XElement element)
    {
        var target = _container.Elements("Item").ElementAtOrDefault(index)
            ?? throw new InvalidDataException($"Slot {index} does not exist (SlotCount={SlotCount}).");

        target.ReplaceWith(element);
        return new ItemEditor(element);
    }

    /// <summary>Swaps two slots' contents in place (both keep their position; the drag source
    /// and drop target trade places) - used for reordering within the grid, e.g. dragging an
    /// existing inventory item onto another slot. A no-op if either index is out of range.</summary>
    public void SwapSlots(int indexA, int indexB)
    {
        if (indexA == indexB)
            return;

        var elements = _container.Elements("Item").ToList();
        if (indexA < 0 || indexA >= elements.Count || indexB < 0 || indexB >= elements.Count)
            return;

        var a = elements[indexA];
        var b = elements[indexB];
        var aReplacement = new XElement(b);
        var bReplacement = new XElement(a);
        a.ReplaceWith(aReplacement);
        b.ReplaceWith(bReplacement);
    }

    /// <summary>Grows or shrinks the slot list to match a new backpack size (see
    /// PlayerEditor.MaxItems) - grows by appending empty (xsi:nil) slots, shrinks by removing
    /// trailing empty slots only. Never removes an occupied slot, even if that means the result
    /// is larger than requested (e.g. shrinking to 12 with an item sitting in slot 20 leaves slot
    /// 20 in place) - silently deleting a carried item as a side effect of a capacity change would
    /// be a surprising way to lose an item. Returns the size actually applied.</summary>
    public int Resize(int newSize)
    {
        var elements = _container.Elements("Item").ToList();

        var lastOccupied = -1;
        for (var i = 0; i < elements.Count; i++)
            if ((string?)elements[i].Attribute(XsiNil) != "true")
                lastOccupied = i;

        var target = Math.Max(newSize, lastOccupied + 1);

        if (target > elements.Count)
        {
            for (var i = elements.Count; i < target; i++)
            {
                var nil = new XElement("Item");
                nil.SetAttributeValue(XsiNil, "true");
                _container.Add(nil);
            }
        }
        else if (target < elements.Count)
        {
            for (var i = elements.Count - 1; i >= target; i--)
                elements[i].Remove();
        }

        return target;
    }

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
