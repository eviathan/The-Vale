using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Every real quest from Data/Quests.json, parsed the same way the decompiled
/// Quest.getQuestFromId parses it (pipe-delimited fields, space-delimited conditions/next-quest
/// lists) - see QuestXmlBuilder (Core) for how each Type becomes a real Quest instance.
///
/// Two real Types are deliberately excluded: "Fishing" (Data/Quests.json has exactly one such
/// entry, id 131 "Willy's Challenge" - but getQuestFromId's own switch statement has no case for
/// "Fishing" at all, meaning even the real game can't construct this one through the normal
/// data-driven path; it's dead/legacy data, not something to replicate) and "Monster" (needs a
/// fully-populated NetRef&lt;Monster&gt; field with no safe way to fabricate a real Monster
/// instance from data alone). Everything else - 64 of 66 real quests - is covered.</summary>
public static class QuestCatalog
{
    private static readonly HashSet<string> SupportedTypes = new()
    {
        "Basic", "ItemDelivery", "Location", "ItemHarvest", "LostItem", "Building", "Crafting", "SecretLostItem", "Social",
    };

    private static IReadOnlyList<QuestSpec>? _all;

    public static IReadOnlyList<QuestSpec> All => _all ??= Load();

    private static List<QuestSpec> Load()
    {
        var result = new List<QuestSpec>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Quests.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var raw = prop.Value.GetString();
            if (string.IsNullOrEmpty(raw))
                continue;

            var fields = raw.Split('/');
            if (fields.Length < 9)
                continue;

            var type = fields[0];
            if (!SupportedTypes.Contains(type))
                continue;

            var title = fields[1];
            var description = fields[2];
            var objective = fields[3];
            var conditions = SplitOrEmpty(fields[4]);
            var nextQuests = SplitOrEmpty(fields[5]);
            var moneyReward = int.TryParse(fields[6], out var mr) ? mr : 0;
            var rewardDescription = fields[7] is "null" or "" ? null : fields[7];
            var canBeCancelled = fields.Length > 8 && bool.TryParse(fields[8], out var cbc) && cbc;
            var targetMessage = fields.Length > 9 && fields[9] != "null" ? fields[9] : null;

            result.Add(new QuestSpec(prop.Name, type, title, description, objective, conditions, nextQuests, moneyReward, rewardDescription, canBeCancelled, targetMessage));
        }

        return result.OrderBy(q => q.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string[] SplitOrEmpty(string field)
        => field is "null" or "" ? Array.Empty<string>() : field.Split(' ');
}
