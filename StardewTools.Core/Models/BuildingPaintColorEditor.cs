using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// A building's custom paint job (the paint-bucket tool's 3 color slots) - real, confirmed
/// nested &lt;buildingPaintColor&gt; element, every field verbatim from a real Greenhouse. Each
/// slot has a "use the building's default color" flag plus its own HSL triple; the *Default
/// flags are true (and hue/saturation/lightness all 0) on every real building checked, since
/// none of the 4 local saves has an actually-repainted building to verify non-default values
/// against. The valid numeric range for Hue/Saturation/Lightness isn't confirmed either (the
/// decompiled BuildingPaintColor class shows no clamp) - callers should not assume a specific
/// max without further verification.
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
            el.Add(new XElement(slot + "Default", true));
            el.Add(new XElement(slot + "Hue", 0));
            el.Add(new XElement(slot + "Saturation", 0));
            el.Add(new XElement(slot + "Lightness", 0));
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

    private bool GetBool(string name) => _element.TryGetChildBool(name) ?? true;
    private void SetBool(string name, bool value) => _element.SetChildBoolCreateIfMissing(name, value);
    private int GetInt(string name) => _element.TryGetChildInt(name) ?? 0;
    private void SetInt(string name, int value) => _element.SetChildIntCreateIfMissing(name, value);
}
