using System;
using System.Linq;
using System.Xml.Linq;

namespace StardewTools.Core.Models;

/// <summary>
/// Typed access over &lt;player&gt;/&lt;stats&gt;. Exposes a curated set of the fields people
/// actually want to cheat/inspect; the underlying &lt;stats&gt; element has ~50 more (every
/// material/resource ever gathered) reachable via <see cref="GetRaw"/>/<see cref="SetRaw"/> by
/// the lowercase field name.
///
/// Two real, different storage shapes were found across local saves for the SAME logical stat:
/// a freshly-created save writes real values directly into legacy flat elements
/// (&lt;daysPlayed&gt;2&lt;/daysPlayed&gt;), while a more-progressed save (after the game's own
/// day-processing code has run at least once) leaves every legacy flat element
/// `xsi:nil="true"` and puts the real values in a &lt;Values&gt; dictionary instead - confirmed
/// via the decompiled Stats.cs's own field: `public SerializableDictionaryWithCaseInsensitiveKeys
/// &lt;uint&gt; Values`. Reading a nil'd flat element as if it had a value (an earlier version
/// of this class did exactly that) throws - `int.Parse("")` on the nil element's empty text -
/// which is what broke opening a real, actively-played save. This class now checks Values first,
/// then a non-nil flat element, defaulting to 0 rather than throwing either way.
/// </summary>
public sealed class StatsEditor
{
    private static readonly XName XsiNil = XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance");

    private readonly XElement _element;

    public StatsEditor(XElement statsElement)
    {
        _element = statsElement;
    }

    public int DaysPlayed { get => Get("daysPlayed"); set => Set("daysPlayed", value); }
    public int StepsTaken { get => Get("stepsTaken"); set => Set("stepsTaken", value); }
    public int MonstersKilled { get => Get("monstersKilled"); set => Set("monstersKilled", value); }
    public int ItemsShipped { get => Get("itemsShipped"); set => Set("itemsShipped", value); }
    public int ItemsCrafted { get => Get("itemsCrafted"); set => Set("itemsCrafted", value); }
    public int ItemsCooked { get => Get("itemsCooked"); set => Set("itemsCooked", value); }
    public int QuestsCompleted { get => Get("questsCompleted"); set => Set("questsCompleted", value); }
    public int TimesFished { get => Get("timesFished"); set => Set("timesFished", value); }
    public int TrufflesFound { get => Get("trufflesFound"); set => Set("trufflesFound", value); }
    public int DiamondsFound { get => Get("diamondsFound"); set => Set("diamondsFound", value); }
    public int GoldFound { get => Get("goldFound"); set => Set("goldFound", value); }
    public int IridiumFound { get => Get("iridiumFound"); set => Set("iridiumFound", value); }
    public int TotalMoneyGifted { get => Get("totalMoneyGifted"); set => Set("totalMoneyGifted", value); }
    public int IndividualMoneyEarned { get => Get("individualMoneyEarned"); set => Set("individualMoneyEarned", value); }

    /// <summary>Read any stat by its lowercase field name (e.g. "goatCheeseMade", "stoneGathered").</summary>
    public int GetRaw(string fieldName) => Get(fieldName);

    /// <summary>Write any stat by its lowercase field name.</summary>
    public void SetRaw(string fieldName, int value) => Set(fieldName, value);

    /// <summary>Every real scalar stat field name available, from either storage shape (see
    /// class remarks) - excludes non-scalar elements that happen to also start lowercase
    /// (specificMonstersKilled is a per-monster-type dictionary, stat_dictionary is a separate
    /// 1.6 overflow bucket for custom/mod stats - confirmed real elements, not something
    /// GetRaw/SetRaw's int parsing could ever handle) and any nil'd legacy element.</summary>
    public IReadOnlyList<string> AllFieldNames
    {
        get
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var e in _element.Elements())
            {
                if (e.Name.LocalName.Length == 0 || !char.IsLower(e.Name.LocalName[0]))
                    continue;
                if ((string?)e.Attribute(XsiNil) == "true")
                    continue;
                if (!int.TryParse(e.Value, out _))
                    continue;

                names.Add(e.Name.LocalName);
            }

            foreach (var key in ValuesKeys())
                names.Add(key);

            return names.ToList();
        }
    }

    private int Get(string fieldName)
    {
        var valuesItem = FindValuesItem(fieldName);
        var valuesText = valuesItem?.Element("value")?.Elements().FirstOrDefault()?.Value;
        if (valuesText is not null && int.TryParse(valuesText, out var valuesParsed))
            return valuesParsed;

        foreach (var name in new[] { fieldName, PascalCase(fieldName) })
        {
            var el = _element.Element(name);
            if (el is not null && (string?)el.Attribute(XsiNil) != "true" && int.TryParse(el.Value, out var flatValue))
                return flatValue;
        }

        return 0;
    }

    /// <summary>Writes through every real storage location that might be live for this save:
    /// the modern Values dictionary always (creating it if this save predates it entirely), plus
    /// any legacy flat element that already exists non-nil, keeping the two in sync rather than
    /// leaving a stale duplicate behind.</summary>
    private void Set(string fieldName, int value)
    {
        var container = ValuesContainer();
        var existing = FindValuesItem(fieldName);
        if (existing is not null)
        {
            var valueElement = existing.Element("value")!;
            valueElement.RemoveNodes();
            valueElement.Add(new XElement("unsignedInt", value));
        }
        else
        {
            container.Add(new XElement("item",
                new XElement("key", new XElement("string", fieldName)),
                new XElement("value", new XElement("unsignedInt", value))));
        }

        foreach (var name in new[] { fieldName, PascalCase(fieldName) })
        {
            var el = _element.Element(name);
            if (el is not null && (string?)el.Attribute(XsiNil) != "true")
                el.Value = value.ToString();
        }
    }

    private IEnumerable<string> ValuesKeys()
        => (_element.Element("Values")?.Elements("item") ?? Enumerable.Empty<XElement>())
            .Select(item => item.Element("key")?.Element("string")?.Value)
            .Where(key => key is not null)
            .Select(key => key!);

    private XElement? FindValuesItem(string fieldName)
        => _element.Element("Values")?.Elements("item")
            .FirstOrDefault(item => string.Equals(item.Element("key")?.Element("string")?.Value, fieldName, StringComparison.OrdinalIgnoreCase));

    private XElement ValuesContainer()
    {
        var container = _element.Element("Values");
        if (container is null)
        {
            container = new XElement("Values");
            _element.Add(container);
        }

        return container;
    }

    private static string PascalCase(string lowerCamelCase) => char.ToUpperInvariant(lowerCamelCase[0]) + lowerCamelCase[1..];
}
