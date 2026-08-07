using System.Xml.Linq;
using StardewTools.Core.Models;

namespace StardewTools.Core.Serialization;

/// <summary>
/// Builds a real &lt;Quest&gt; element from a QuestSpec (parsed Data/Quests.json), replicating
/// the decompiled Quest.getQuestFromId's own construction logic - the canonical, game-verified
/// way a Quest instance gets built from that same raw data (used for billboard/mail quests in
/// the real game), not a guess.
///
/// Base field order/shape confirmed against 2 real quests in an actual save (a SocializeQuest
/// and an ItemHarvestQuest): _currentObjective, _questDescription, _questTitle,
/// rewardDescription, accepted, completed, dailyQuest, showNew, canBeCancelled, destroy, id,
/// moneyReward, questType, daysLeft, dayQuestAccepted, nextQuests, questTitle - then whatever
/// subtype-specific fields follow (also confirmed for Social/ItemHarvest via those same 2
/// examples; other subtypes' extra fields come from their own decompiled AddField list, the
/// same evidentiary tier already used for Tool/Weapon fabrication this session).
///
/// Deliberately omits every subtype's "parts"/"dialogueparts"/rich "objective"
/// NetDescriptionElementList tree fields - those exist purely to render formatted quest-log text
/// and have no bearing on completion state; a fabricated quest immediately marked completed
/// doesn't need them, and guessing their nested shape would be needless risk for a cosmetic
/// field. whoToGreet for Social quests is the one exception - reused verbatim from the real
/// confirmed example rather than recomputed, since there's only one Social quest in the entire
/// game (id 9, "Introductions").
/// </summary>
internal static class QuestXmlBuilder
{
    /// <summary>Real questType values per subtype - confirmed via each subclass's own
    /// constructor (`questType.Value = N`) or, for Social/ItemHarvest, the real save examples
    /// directly.</summary>
    private static int QuestTypeFor(string type) => type switch
    {
        "Basic" => 1,
        "Crafting" => 2,
        "ItemDelivery" => 3,
        "Location" => 6,
        "Building" => 8,
        "ItemHarvest" or "LostItem" or "SecretLostItem" => 9,
        "Social" => 5,
        _ => 1,
    };

    public static XElement Build(QuestSpec spec, string xsiTypeAttributeValue, XName xsiType)
    {
        var fields = new List<XElement>
        {
            new("_currentObjective", spec.Objective),
            new("_questDescription", spec.Description),
            new("_questTitle", spec.Title),
            new("rewardDescription", spec.RewardDescription ?? "-1"),
            new("accepted", true),
            new("completed", true),
            new("dailyQuest", false),
            new("showNew", true),
            new("canBeCancelled", spec.CanBeCancelled),
            new("destroy", false),
            new("id", spec.Id),
            new("moneyReward", spec.MoneyReward),
            new("questType", QuestTypeFor(spec.Type)),
            new("daysLeft", 0),
            new("dayQuestAccepted", -1),
            new("nextQuests", spec.NextQuests.Select(n => new XElement("int", n))),
            new("questTitle", spec.Title),
        };

        fields.AddRange(SubtypeFields(spec));

        return new XElement("Quest", new XAttribute(xsiType, xsiTypeAttributeValue), fields);
    }

    /// <summary>Maps Data/Quests.json's own Type string to the real save xsi:type attribute -
    /// confirmed for Social/ItemHarvest via real examples, the rest via getQuestFromId's own
    /// `q = new XyzQuest(...)` construction (the C# class name is always the xsi:type).</summary>
    public static string XsiTypeFor(string type) => type switch
    {
        "Crafting" => "CraftingQuest",
        "Location" => "GoSomewhereQuest",
        "ItemDelivery" => "ItemDeliveryQuest",
        "ItemHarvest" => "ItemHarvestQuest",
        "LostItem" => "LostItemQuest",
        "SecretLostItem" => "SecretLostItemQuest",
        "Social" => "SocializeQuest",
        _ => "Quest", // Basic and Building both use the plain base class, no subclass at all
    };

    private static IEnumerable<XElement> SubtypeFields(QuestSpec spec)
    {
        switch (spec.Type)
        {
            case "Crafting":
                // CraftingQuest's only netcode-synced field (ItemId) - isBigCraftable/
                // indexToCraft are legacy XmlElement-decorated compat accessors over the same
                // underlying value, not separately serialized.
                yield return new XElement("ItemId", QualifyItemId(spec.Conditions.ElementAtOrDefault(0) ?? ""));
                break;

            case "Location":
                yield return new XElement("whereToGo", spec.Conditions.ElementAtOrDefault(0) ?? "");
                break;

            case "Building":
                yield return new XElement("completionString", spec.Conditions.ElementAtOrDefault(0) ?? "");
                break;

            case "ItemDelivery":
                yield return new XElement("target", spec.Conditions.ElementAtOrDefault(0) ?? "");
                yield return new XElement("ItemId", QualifyItemId(spec.Conditions.ElementAtOrDefault(1) ?? ""));
                yield return new XElement("number", ParseIntOr(spec.Conditions.ElementAtOrDefault(2), 1));
                break;

            case "ItemHarvest":
                yield return new XElement("itemIndex", QualifyItemId(spec.Conditions.ElementAtOrDefault(0) ?? ""));
                yield return new XElement("number", ParseIntOr(spec.Conditions.ElementAtOrDefault(1), 1));
                break;

            case "LostItem":
                yield return new XElement("npcName", spec.Conditions.ElementAtOrDefault(0) ?? "");
                yield return new XElement("locationOfItem", spec.Conditions.ElementAtOrDefault(2) ?? "");
                yield return new XElement("ItemId", QualifyItemId(spec.Conditions.ElementAtOrDefault(1) ?? ""));
                yield return new XElement("tileX", ParseIntOr(spec.Conditions.ElementAtOrDefault(3), 0));
                yield return new XElement("tileY", ParseIntOr(spec.Conditions.ElementAtOrDefault(4), 0));
                yield return new XElement("itemFound", false);
                break;

            case "SecretLostItem":
                yield return new XElement("npcName", spec.Conditions.ElementAtOrDefault(0) ?? "");
                yield return new XElement("friendshipReward", ParseIntOr(spec.Conditions.ElementAtOrDefault(2), 0));
                yield return new XElement("exclusiveQuestId", spec.Conditions.ElementAtOrDefault(3) ?? "");
                yield return new XElement("ItemId", QualifyItemId(spec.Conditions.ElementAtOrDefault(1) ?? ""));
                yield return new XElement("itemFound", false);
                break;

            case "Social":
                // The one and only Social quest in the entire game (id 9, "Introductions") -
                // whoToGreet/total reused verbatim from the real confirmed example rather than
                // recomputed from a villager roster, since there's nothing to generalize over.
                var villagers = new[]
                {
                    "Abigail", "Caroline", "Clint", "Demetrius", "Willy", "Elliott", "Emily", "Evelyn",
                    "George", "Gus", "Haley", "Harvey", "Jas", "Jodi", "Alex", "Leah", "Linus", "Marnie",
                    "Maru", "Pam", "Penny", "Pierre", "Sam", "Sebastian", "Shane", "Vincent",
                };
                yield return new XElement("whoToGreet", villagers.Select(v => new XElement("string", v)));
                yield return new XElement("total", villagers.Length);
                break;
        }
    }

    /// <summary>Data/Quests.json's condition values are unqualified ids ("24") - the real
    /// fields want the qualified form ("(O)24"), confirmed via ItemHarvestQuest's constructor
    /// (ItemRegistry.QualifyItemId) - but a value that's already qualified or non-numeric
    /// (a location/NPC name used positionally in some subtypes) is passed through unchanged.</summary>
    private static string QualifyItemId(string rawId)
        => rawId.Length > 0 && rawId[0] != '(' && int.TryParse(rawId, out _) ? $"(O){rawId}" : rawId;

    private static int ParseIntOr(string? value, int fallback) => int.TryParse(value, out var v) ? v : fallback;
}
