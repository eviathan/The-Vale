using System.Xml.Linq;

namespace StardewTools.Core.Models;

/// <summary>
/// Typed access over a Farmer NetStringDictionary&lt;int, NetInt&gt; field (basicShipped,
/// mineralsFound, recipesCooked - confirmed identical declaration in the decompiled Farmer.cs).
///
/// Key encoding was initially assumed to always be &lt;string&gt; (matching craftingRecipes/
/// cookingRecipes, the only two real examples available at first) - that was wrong. Two other
/// real local saves (FarmerBrian_392578958, FarmerBrian_392648367) turned out to have a
/// populated basicShipped, and both write numeric-id keys as &lt;key&gt;&lt;int&gt;330&lt;/int&gt;
/// &lt;/key&gt;, not &lt;string&gt;. Reading the actual SerializableDictionary.WriteXml/ReadXml
/// source (StardewValley/SerializableDictionary.cs) explains why: even though TKey is statically
/// string for every one of these fields, the reader explicitly branches on
/// `typeof(TKey) == typeof(string) &amp;&amp; reader.Name == "int"` and converts back via
/// Convert.ChangeType - the game's own key serializer picks &lt;int&gt; when the string value
/// happens to parse as a number, and &lt;string&gt; otherwise (recipe names like "Chest" never
/// parse as a number, so those two are unaffected). This class now replicates that exact rule
/// rather than hardcoding one representation.
/// </summary>
public sealed class NameCountDictionaryEditor
{
    private readonly XElement _element;

    public NameCountDictionaryEditor(XElement dictionaryElement)
    {
        _element = dictionaryElement;
    }

    public IReadOnlyList<string> Keys
        => _element.Elements("item")
            .Select(ReadKey)
            .Where(key => key is not null)
            .Select(key => key!)
            .ToList();

    public bool Contains(string key) => FindItem(key) is not null;

    public int Count(string key)
        => FindItem(key)?.Element("value")?.Element("int")?.Value is { } v ? int.Parse(v) : 0;

    public void Add(string key, int count = 1)
    {
        if (Contains(key))
            return;

        var item = new XElement("item",
            new XElement("key", KeyElement(key)),
            new XElement("value", new XElement("int", count)));

        _element.Add(item);
    }

    public void Remove(string key) => FindItem(key)?.Remove();

    /// <summary>Matches the real game's own key encoding rule exactly (see class remarks) -
    /// &lt;int&gt; when the key parses as a number, &lt;string&gt; otherwise.</summary>
    internal static XElement KeyElement(string key)
        => int.TryParse(key, out var numericKey) ? new XElement("int", numericKey) : new XElement("string", key);

    internal static string? ReadKey(XElement item)
    {
        var keyElement = item.Element("key");
        return keyElement?.Element("int")?.Value ?? keyElement?.Element("string")?.Value;
    }

    private XElement? FindItem(string key)
        => _element.Elements("item").FirstOrDefault(item => ReadKey(item) == key);
}
