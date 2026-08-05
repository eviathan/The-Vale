using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>Typed read/write access over a &lt;player&gt; element from a save file.</summary>
public sealed class PlayerEditor
{
    private readonly XElement _element;

    public PlayerEditor(XElement playerElement)
    {
        _element = playerElement;
        Inventory = new ItemListEditor(_element.Element("items")
            ?? throw new InvalidDataException("<player> has no <items> child."));
    }

    public ItemListEditor Inventory { get; }

    public string Name
    {
        get => _element.GetChildText("name");
        set => _element.SetChildText("name", value);
    }

    public int Money
    {
        get => _element.GetChildInt("money");
        set => _element.SetChildInt("money", value);
    }

    public int Health
    {
        get => _element.GetChildInt("health");
        set => _element.SetChildInt("health", value);
    }

    public int MaxHealth
    {
        get => _element.GetChildInt("maxHealth");
        set => _element.SetChildInt("maxHealth", value);
    }

    public int Stamina
    {
        get => _element.GetChildInt("stamina");
        set => _element.SetChildInt("stamina", value);
    }
}
