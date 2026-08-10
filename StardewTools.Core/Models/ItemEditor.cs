using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// Typed access over a single &lt;Item xsi:type="..."&gt; element (tools, objects, rings,
/// furniture, etc. all share this shape). Different item subtypes serialize slightly
/// differently in practice - e.g. "Object"-derived items duplicate Stack/quality under
/// both a PascalCase and lowercase name, while "Tool"-derived items only have the
/// PascalCase one - so field access here tries every known variant rather than assuming one.
/// </summary>
public sealed class ItemEditor
{
    private static readonly XName XsiType = XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance");

    private readonly XElement _element;

    public ItemEditor(XElement itemElement)
    {
        _element = itemElement;
    }

    /// <summary>The concrete item class, e.g. "Axe", "Object", "Ring", "Furniture".</summary>
    public string ItemType => (string?)_element.Attribute(XsiType) ?? "";

    public string Name => _element.Element("Name")?.Value ?? _element.Element("name")?.Value ?? "";

    /// <summary>Clamped to [1, MaximumStackSize] on write - matches the real game's own
    /// Item.Stack setter (Item.cs: `Math.Min(Math.Max(0, value), maximumStackSize())`, floored at
    /// 1 instead of 0 here since a 0-stack item sitting in a slot isn't a state this editor ever
    /// wants to produce - removing an item should clear the slot, not leave a 0-stack husk).</summary>
    public int Stack
    {
        get => _element.GetChildIntAny("stack", "Stack");
        set => _element.SetChildIntAny(System.Math.Clamp(value, 1, MaximumStackSize), "stack", "Stack");
    }

    /// <summary>Not every item type tracks quality (most tools don't) - null when absent.</summary>
    public int? Quality
    {
        get => _element.TryGetChildInt("quality") ?? _element.TryGetChildInt("Quality");
        set
        {
            if (value is int v)
                _element.SetChildIntAny(v, "quality", "Quality");
        }
    }

    public bool HasQuality => _element.Element("quality") is not null || _element.Element("Quality") is not null;

    /// <summary>Sprite sheet index for this item's icon (confirmed real field, e.g. Weeds = 784). Null if absent.</summary>
    public int? ParentSheetIndex => _element.TryGetChildInt("parentSheetIndex") ?? _element.TryGetChildInt("ParentSheetIndex");

    /// <summary>Inventory-icon sprite index for Tool/MeleeWeapon items (confirmed real field on
    /// both - a real carried Axe and a real carried Scythe each have it, same lowercase/PascalCase
    /// duplication convention as Stack/Quality). Plain Objects use ParentSheetIndex instead and
    /// don't have this field at all.</summary>
    public int? IndexOfMenuItemView => _element.TryGetChildInt("indexOfMenuItemView") ?? _element.TryGetChildInt("IndexOfMenuItemView");

    /// <summary>Machines/decorations (Furnace, Grandfather Clock, ...) vs a plain Object -
    /// same underlying element shape either way (Object.cs), just a different ParentSheetIndex
    /// space (Data/BigCraftables.json instead of Data/Objects.json). Not every item type
    /// (e.g. tools) has this field at all, so this can't use the throwing GetChildBool.</summary>
    public bool BigCraftable => bool.TryParse(_element.Element("bigCraftable")?.Value, out var v) && v;

    /// <summary>How many of this item a single slot can hold - confirmed against every
    /// `maximumStackSize()` override in the decompiled source: Tool (and MeleeWeapon, which
    /// extends it), Clothing, Hat, SpecialItem, Ring, Trinket, Wallpaper, and Boots all
    /// unconditionally return 1 - only Object.maximumStackSize() (shared by plain objects and
    /// BigCraftables alike, since "bigCraftable" is just a bool flag on Object, not a separate
    /// class) defaults to 999. A handful of specific Object ids (roe, some artifacts) and
    /// Category -22 also cap at 1 in the real game; not modeled here (would need a
    /// Data/Objects.json category lookup this Core-layer type deliberately doesn't do - see
    /// ItemCategories in the app layer for why that lookup lives one layer up) - a rare,
    /// low-consequence gap, not a guess.</summary>
    public int MaximumStackSize => ItemType is "" or "Object" ? 999 : 1;

    public bool IsStackable => MaximumStackSize > 1;

    /// <summary>The dye/tint color a ColoredObject-subclass item carries (Wool, Roe/Aged Roe,
    /// Juice, Wine, Duck Feather, ... - anything whose real class is
    /// StardewValley.Objects.ColoredObject, not plain Object) - confirmed real field/shape via
    /// decompiled ColoredObject.cs (&lt;color&gt;, same B/G/R/A/PackedValue NetColor shape as
    /// Chest.playerChoiceColor). Absent (null) for every other item type - checking the element's
    /// own presence rather than trusting ItemType == "ColoredObject" alone, since that string
    /// match is the same defensive-by-presence approach Quality/ParentSheetIndex already use
    /// here rather than assuming a fixed set of xsi:type values is exhaustive.</summary>
    public (byte R, byte G, byte B, byte A)? Color
    {
        get => _element.TryGetChildColor("color");
        set
        {
            if (value is { } c)
                _element.SetChildColorCreateIfMissing("color", c.R, c.G, c.B, c.A);
        }
    }

    public bool HasColor => _element.Element("color") is not null;

    /// <summary>Real Item.SpecialVariable field - a public property (not the private NetInt
    /// backing field it wraps) so it serializes under its own PascalCase name despite having no
    /// explicit [XmlElement], confirmed against real placed-object save examples. Repurposed by
    /// sprinklers specifically as a "has a torch placed on top" flag - decompiled Object.cs sets
    /// it to the literal sentinel 999999 when a Torch (id 93) is dropped onto a sprinkler, and
    /// checks `SpecialVariable == 999999` at draw time to show it (Torch.drawBasicTorch). 0 is
    /// the real default/"no torch" value for every other object type too.</summary>
    public int SpecialVariable
    {
        get => _element.TryGetChildInt("SpecialVariable") ?? 0;
        set => _element.SetChildIntCreateIfMissing("SpecialVariable", value);
    }

    /// <summary>The nested Object this item holds (real NetRef&lt;Object&gt; `heldObject` field -
    /// e.g. a sprinkler's attached Pressure Nozzle/Enricher, or Auto-Grabber's storage Chest) -
    /// absent entirely when nothing's attached (not present-with-nil - see ObjectXmlBuilder's own
    /// heldObject parameter/remarks for the confirmed real shape), so null here means "nothing
    /// attached", not "attached but empty".
    ///
    /// The held item's own fields are DIRECT children of &lt;heldObject&gt; itself - there is no
    /// nested &lt;Object&gt; (or similar) wrapper element inside it; &lt;heldObject&gt; IS the
    /// item's root, same shape as a plain top-level &lt;Object&gt; entity just under a different
    /// tag name (an xsi:type attribute is added only for a non-Object subclass, e.g.
    /// FarmMapEditor's Auto-Grabber Chest: `&lt;heldObject xsi:type="Chest"&gt;`). Confirmed
    /// against a real save: a Pressure Nozzle attached to a sprinkler by hand in-game serialized
    /// as `&lt;heldObject&gt;&lt;isLostItem&gt;...&lt;parentSheetIndex&gt;915&lt;/parentSheetIndex&gt;...&lt;/heldObject&gt;`
    /// with no wrapper. This tool previously wrapped the fields in an extra &lt;Object&gt; element
    /// - the real game's NetRef&lt;Object&gt; deserializer doesn't expect that extra nesting, so
    /// every sprinkler attachment written by this tool silently failed to resolve into a real
    /// item and rendered as the "Error Item" fallback sprite (a circle-with-a-slash) once loaded
    /// in the actual game, even though it looked fine and round-tripped through this tool's own
    /// parser (confirmed by finding several `&lt;itemId&gt;-1&lt;/itemId&gt;&lt;name&gt;Error Item&lt;/name&gt;`
    /// entries - the game's own canonical "couldn't resolve this" placeholder - in a real save
    /// where this tool had attached nozzles before this fix).</summary>
    public ItemEditor? HeldObject
    {
        get
        {
            var held = _element.Element("heldObject");
            return held is null ? null : new ItemEditor(held);
        }
    }

    /// <summary>Attaches (replacing any existing one) a fresh Object as this item's heldObject -
    /// <paramref name="fields"/> should be the item's own field elements (e.g. from
    /// ObjectXmlBuilder.Fields), NOT wrapped in an outer &lt;Object&gt; element - see this
    /// property's remarks for why.</summary>
    public void SetHeldObject(IEnumerable<XElement> fields)
    {
        _element.Element("heldObject")?.Remove();
        _element.Add(new XElement("heldObject", fields));
    }

    public void ClearHeldObject() => _element.Element("heldObject")?.Remove();

    internal XElement Element => _element;
}
