using System.Xml.Linq;

namespace StardewTools.Core.Models;

/// <summary>
/// Typed access over a Farmer NetStringIntArrayDictionary field (fishCaught, archaeologyFound) -
/// same &lt;item&gt;&lt;key&gt;/&lt;value&gt; wrapper as NameCountDictionaryEditor (both go
/// through the identical SerializableDictionary&lt;string, T&gt;.WriteXml/ReadXml - see that
/// class's remarks for the key-encoding rule this reuses), but the value is a 2-element int
/// array (confirmed via the decompiled Farmer.caughtFish/foundArtifact: `new int[2] { count,
/// size }` / `new int[2] { count, count }`) rather than a single int - which XmlSerializer
/// renders as two bare &lt;int&gt; siblings under &lt;value&gt;, no wrapper element, matching
/// the same convention already observed for other real int[] fields in this exact save shape
/// (Tree's nextQuests, Chest's lidFrameCount).
///
/// No real save with a populated fishCaught/archaeologyFound was found (checked all 4 local
/// saves) - this shape is derived directly from the serializer's own source rather than a real
/// example, the same evidentiary tier already used for BuildingEditor's unverified fields.
/// </summary>
public sealed class ArrayCountDictionaryEditor
{
    private readonly XElement _element;

    public ArrayCountDictionaryEditor(XElement dictionaryElement)
    {
        _element = dictionaryElement;
    }

    public IReadOnlyList<string> Keys
        => _element.Elements("item")
            .Select(NameCountDictionaryEditor.ReadKey)
            .Where(key => key is not null)
            .Select(key => key!)
            .ToList();

    public bool Contains(string key) => FindItem(key) is not null;

    public (int First, int Second) Values(string key)
    {
        var ints = FindItem(key)?.Element("value")?.Elements("int").Select(e => int.Parse(e.Value)).ToList();
        return ints is { Count: >= 2 } ? (ints[0], ints[1]) : (0, 0);
    }

    public void Add(string key, int first, int second)
    {
        if (Contains(key))
            return;

        var item = new XElement("item",
            new XElement("key", NameCountDictionaryEditor.KeyElement(key)),
            new XElement("value", new XElement("int", first), new XElement("int", second)));

        _element.Add(item);
    }

    public void Remove(string key) => FindItem(key)?.Remove();

    private XElement? FindItem(string key)
        => _element.Elements("item").FirstOrDefault(item => NameCountDictionaryEditor.ReadKey(item) == key);
}
