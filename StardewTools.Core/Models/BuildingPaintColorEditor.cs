using System.Xml.Linq;

namespace StardewTools.Core.Models;

/// <summary>
/// A building's custom paint job (the paint-bucket tool's 3 color slots) - real, confirmed nested
/// &lt;buildingPaintColor&gt; element. Each slot has a "use the building's default color" flag
/// plus its own HSL triple, but critically every one of those 12 fields is wrapped in its own
/// type-tag child (&lt;Color1Default&gt;&lt;boolean&gt;false&lt;/boolean&gt;&lt;/Color1Default&gt;,
/// &lt;Color1Hue&gt;&lt;int&gt;59&lt;/int&gt;&lt;/Color1Hue&gt;, ...) - NOT a bare value directly
/// inside the named element the way most other bool/int fields in this codebase work. Confirmed
/// against a real building painted through the actual in-game Carpenter's Menu (not guessed): a
/// real, reported bug - this class previously wrote/read the bare-value shape, which the game's
/// own deserializer can't parse as valid paint data, so it silently fell back to "default, no
/// paint" for every building this tool ever painted, in every save, the whole time. Valid numeric
/// ranges for Hue/Saturation/Lightness are also real and per-building/per-slot, not a universal
/// -100..100 - see BuildingSprites.LightnessRangesFor (SaveEditor layer) for the real values,
/// parsed from Data/PaintData.json.
/// </summary>
public sealed class BuildingPaintColorEditor
{
    private readonly XElement _element;

    public BuildingPaintColorEditor(XElement paintColorElement)
    {
        _element = paintColorElement;
    }

    /// <summary>The confirmed real default shape for a freshly-placed building - all three
    /// slots defaulted, no custom color.</summary>
    internal static XElement CreateDefault()
    {
        var el = new XElement("buildingPaintColor",
            new XElement("ColorName", new XElement("string", new XAttribute(XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance"), "true"))));
        foreach (var slot in new[] { "Color1", "Color2", "Color3" })
        {
            el.Add(new XElement(slot + "Default", new XElement("boolean", true)));
            el.Add(new XElement(slot + "Hue", new XElement("int", 0)));
            el.Add(new XElement(slot + "Saturation", new XElement("int", 0)));
            el.Add(new XElement(slot + "Lightness", new XElement("int", 0)));
        }

        return el;
    }

    public bool Color1Default { get => GetBool("Color1Default"); set => SetBool("Color1Default", value); }
    public int Color1Hue { get => GetInt("Color1Hue"); set => SetInt("Color1Hue", value); }
    public int Color1Saturation { get => GetInt("Color1Saturation"); set => SetInt("Color1Saturation", value); }
    public int Color1Lightness { get => GetInt("Color1Lightness"); set => SetInt("Color1Lightness", value); }

    public bool Color2Default { get => GetBool("Color2Default"); set => SetBool("Color2Default", value); }
    public int Color2Hue { get => GetInt("Color2Hue"); set => SetInt("Color2Hue", value); }
    public int Color2Saturation { get => GetInt("Color2Saturation"); set => SetInt("Color2Saturation", value); }
    public int Color2Lightness { get => GetInt("Color2Lightness"); set => SetInt("Color2Lightness", value); }

    public bool Color3Default { get => GetBool("Color3Default"); set => SetBool("Color3Default", value); }
    public int Color3Hue { get => GetInt("Color3Hue"); set => SetInt("Color3Hue", value); }
    public int Color3Saturation { get => GetInt("Color3Saturation"); set => SetInt("Color3Saturation", value); }
    public int Color3Lightness { get => GetInt("Color3Lightness"); set => SetInt("Color3Lightness", value); }

    /// <summary>Reads a Color{N}Default/Hue/Saturation/Lightness field, tolerating BOTH the real
    /// wrapped shape (&lt;Color1Hue&gt;&lt;int&gt;59&lt;/int&gt;&lt;/Color1Hue&gt;) and the old
    /// bare-value shape this class used to write (so a save already touched by an earlier,
    /// pre-fix version of this tool still reads back instead of throwing) - the bare form is
    /// never written going forward, only tolerated on read.</summary>
    private bool GetBool(string name)
    {
        var child = _element.Element(name);
        if (child is null)
            return true;

        var inner = child.Element("boolean");
        var text = inner?.Value ?? child.Value;
        return string.IsNullOrEmpty(text) || bool.Parse(text);
    }

    private void SetBool(string name, bool value)
    {
        var child = _element.Element(name);
        if (child is null)
        {
            child = new XElement(name);
            _element.Add(child);
        }

        var inner = child.Element("boolean");
        if (inner is null)
        {
            child.RemoveNodes();
            inner = new XElement("boolean");
            child.Add(inner);
        }

        inner.Value = value ? "true" : "false";
    }

    private int GetInt(string name)
    {
        var child = _element.Element(name);
        if (child is null)
            return 0;

        var inner = child.Element("int");
        var text = inner?.Value ?? child.Value;
        return string.IsNullOrEmpty(text) ? 0 : int.Parse(text);
    }

    private void SetInt(string name, int value)
    {
        var child = _element.Element(name);
        if (child is null)
        {
            child = new XElement(name);
            _element.Add(child);
        }

        var inner = child.Element("int");
        if (inner is null)
        {
            child.RemoveNodes();
            inner = new XElement("int");
            child.Add(inner);
        }

        inner.Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
