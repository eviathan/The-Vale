using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>Typed access over one NPC's &lt;Friendship&gt; element.</summary>
public sealed class FriendshipEditor
{
    private readonly XElement _element;

    public FriendshipEditor(XElement friendshipElement)
    {
        _element = friendshipElement;
    }

    public int Points
    {
        get => _element.GetChildInt("Points");
        set => _element.SetChildInt("Points", value);
    }

    public bool TalkedToToday
    {
        get => _element.GetChildBool("TalkedToToday");
        set => _element.SetChildBool("TalkedToToday", value);
    }
}

/// <summary>
/// Typed access over &lt;player&gt;/&lt;friendshipData&gt;, a dictionary keyed by NPC name.
/// Deliberately read/edit-only for NPCs you already have an entry for (i.e. have met/talked
/// to in-game) - fabricating a brand new &lt;Friendship&gt; blob for an NPC you've never
/// interacted with risks writing an incomplete element the game's deserializer might choke
/// on, and we don't have a populated save to verify the full required field set against.
/// </summary>
public sealed class FriendshipsEditor
{
    private readonly XElement _element;

    public FriendshipsEditor(XElement friendshipDataElement)
    {
        _element = friendshipDataElement;
    }

    public IReadOnlyList<string> NpcNames
        => _element.Elements("item")
            .Select(item => item.Element("key")?.Element("string")?.Value)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToList();

    public FriendshipEditor? TryGet(string npcName)
    {
        var friendship = FindItem(npcName)?.Element("value")?.Element("Friendship");
        return friendship is null ? null : new FriendshipEditor(friendship);
    }

    private XElement? FindItem(string npcName)
        => _element.Elements("item")
            .FirstOrDefault(item => item.Element("key")?.Element("string")?.Value == npcName);
}
