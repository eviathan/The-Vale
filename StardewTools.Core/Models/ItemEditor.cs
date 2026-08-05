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

    public int Stack
    {
        get => _element.GetChildIntAny("stack", "Stack");
        set => _element.SetChildIntAny(value, "stack", "Stack");
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

    internal XElement Element => _element;
}
