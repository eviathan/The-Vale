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

    internal XElement Element => _element;
}
