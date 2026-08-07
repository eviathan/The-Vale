using System.Xml.Linq;

namespace StardewTools.Core.Models;

/// <summary>
/// Typed access over &lt;player&gt;/&lt;craftingRecipes&gt; or &lt;cookingRecipes&gt; - both
/// share the identical dictionary shape (confirmed against a real save: string recipe name key,
/// int "times made" value - 0 means known but never crafted/cooked, same shape and even the same
/// key/value item wrapper as friendshipData). Knowing a recipe is just having an entry at all;
/// there's no separate "known" flag.
/// </summary>
public sealed class RecipeListEditor
{
    private readonly XElement _element;

    public RecipeListEditor(XElement recipesElement)
    {
        _element = recipesElement;
    }

    public IReadOnlyList<string> KnownRecipeNames
        => _element.Elements("item")
            .Select(item => item.Element("key")?.Element("string")?.Value)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToList();

    public bool IsKnown(string recipeName) => FindItem(recipeName) is not null;

    public int TimesMade(string recipeName)
        => FindItem(recipeName)?.Element("value")?.Element("int")?.Value is { } v ? int.Parse(v) : 0;

    /// <summary>No-ops if already known (real behavior - a recipe is either known or not, this
    /// isn't a "relearn" mechanic).</summary>
    public void Learn(string recipeName)
    {
        if (IsKnown(recipeName))
            return;

        var item = new XElement("item",
            new XElement("key", new XElement("string", recipeName)),
            new XElement("value", new XElement("int", 0)));

        _element.Add(item);
    }

    public void Forget(string recipeName) => FindItem(recipeName)?.Remove();

    private XElement? FindItem(string recipeName)
        => _element.Elements("item")
            .FirstOrDefault(item => item.Element("key")?.Element("string")?.Value == recipeName);
}
