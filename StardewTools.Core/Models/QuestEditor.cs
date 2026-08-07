using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// Typed access over one &lt;Quest&gt; element in &lt;player&gt;/&lt;questLog&gt;. Every quest
/// subtype (SocializeQuest, ItemHarvestQuest, ...) has its own extra fields beyond this - only
/// the base Quest.cs fields common to all of them are exposed here (confirmed against the
/// decompiled Quest.cs's own AddField chain: accepted/completed/id/questType/daysLeft are
/// universal; Title/Description come from questTitle/_questDescription, present on both real
/// examples checked but not part of that same base AddField list, so read defensively). This
/// deliberately only edits quests already in the log - fabricating a brand-new Quest blob from
/// scratch would need a per-subtype shape this doesn't have verified (same class of gap as
/// Fence - see MAP_AUDIT.md).
/// </summary>
public sealed class QuestEditor
{
    private readonly XElement _element;

    public QuestEditor(XElement questElement)
    {
        _element = questElement;
    }

    public string Id => _element.Element("id")?.Value ?? "";
    public string QuestType => _element.Element("questType")?.Value ?? "";
    public string Title => _element.Element("questTitle")?.Value ?? _element.Element("_questTitle")?.Value ?? "(untitled quest)";
    public string Description => _element.Element("_questDescription")?.Value ?? "";

    public bool Accepted
    {
        get => _element.GetChildBool("accepted");
        set => _element.SetChildBool("accepted", value);
    }

    public bool Completed
    {
        get => _element.GetChildBool("completed");
        set => _element.SetChildBool("completed", value);
    }

    public int DaysLeft
    {
        get => _element.GetChildInt("daysLeft");
        set => _element.SetChildInt("daysLeft", value);
    }

    internal XElement Element => _element;
}
