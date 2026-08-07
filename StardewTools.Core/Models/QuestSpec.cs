namespace StardewTools.Core.Models;

/// <summary>
/// Everything needed to fabricate a real Quest instance, already parsed from Data/Quests.json's
/// pipe-delimited raw string (mirroring the decompiled Quest.getQuestFromId's own parsing - see
/// PlayerEditor.CompleteQuest/QuestXmlBuilder for how each Type maps to a concrete shape).
/// Conditions is the space-split fields[4] segment; its meaning depends on Type (e.g. for
/// "ItemDelivery" it's [npcName, itemId, numberRequired]).
/// </summary>
public sealed record QuestSpec(
    string Id,
    string Type,
    string Title,
    string Description,
    string Objective,
    string[] Conditions,
    string[] NextQuests,
    int MoneyReward,
    string? RewardDescription,
    bool CanBeCancelled,
    string? TargetMessage);
