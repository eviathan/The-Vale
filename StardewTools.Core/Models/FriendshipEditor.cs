using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>Typed access over one NPC's &lt;Friendship&gt; element - the 8-field shape confirmed
/// against 2 real examples (Robin, Lewis) in an actual save: Points, GiftsThisWeek, GiftsToday,
/// TalkedToToday, ProposalRejected, Status, Proposer, RoommateMarriage. The decompiled
/// Friendship.cs has several more fields (LastGiftDate, WeddingDate, NextBirthingDate,
/// DaysMarried, CountdownToWedding, DaysUntilBirthing) that aren't in either real example - same
/// self-healing pattern documented elsewhere in this codebase (they're WorldDate-typed and
/// presumably only appear once actually assigned, e.g. after a wedding), so they're
/// deliberately not modeled here.</summary>
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

    public int GiftsThisWeek
    {
        get => _element.GetChildInt("GiftsThisWeek");
        set => _element.SetChildInt("GiftsThisWeek", value);
    }

    public int GiftsToday
    {
        get => _element.GetChildInt("GiftsToday");
        set => _element.SetChildInt("GiftsToday", value);
    }

    public bool TalkedToToday
    {
        get => _element.GetChildBool("TalkedToToday");
        set => _element.SetChildBool("TalkedToToday", value);
    }

    public bool ProposalRejected
    {
        get => _element.GetChildBool("ProposalRejected");
        set => _element.SetChildBool("ProposalRejected", value);
    }

    /// <summary>Raw string enum value - confirmed real values from the decompiled
    /// FriendshipStatus enum: Friendly, Dating, Engaged, Married, Divorced.</summary>
    public string Status
    {
        get => _element.GetChildText("Status");
        set => _element.SetChildText("Status", value);
    }

    public bool RoommateMarriage
    {
        get => _element.GetChildBool("RoommateMarriage");
        set => _element.SetChildBool("RoommateMarriage", value);
    }
}

/// <summary>
/// Typed access over &lt;player&gt;/&lt;friendshipData&gt;, a dictionary keyed by NPC name.
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

    /// <summary>Fabricates a fresh &lt;Friendship&gt; entry for an NPC you haven't met yet, so
    /// relationships can be set from zero rather than only edited once the game itself has
    /// already created the entry. Field shape/order confirmed against 2 real examples (Robin,
    /// Lewis) in an actual save - both non-married/default, matching what a freshly-met NPC's
    /// entry actually looks like.</summary>
    public FriendshipEditor GetOrCreate(string npcName)
    {
        if (TryGet(npcName) is { } existing)
            return existing;

        var friendship = new XElement("Friendship",
            new XElement("Points", 0),
            new XElement("GiftsThisWeek", 0),
            new XElement("GiftsToday", 0),
            new XElement("TalkedToToday", false),
            new XElement("ProposalRejected", false),
            new XElement("Status", "Friendly"),
            new XElement("Proposer", 0),
            new XElement("RoommateMarriage", false));

        var item = new XElement("item",
            new XElement("key", new XElement("string", npcName)),
            new XElement("value", friendship));

        _element.Add(item);
        return new FriendshipEditor(friendship);
    }

    private XElement? FindItem(string npcName)
        => _element.Elements("item")
            .FirstOrDefault(item => item.Element("key")?.Element("string")?.Value == npcName);
}
