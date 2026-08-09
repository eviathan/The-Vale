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

    /// <summary>Active/current quest log - see QuestEditor remarks for why this only edits
    /// quests already present rather than fabricating new ones.</summary>
    public IReadOnlyList<QuestEditor> Quests
        => (_element.Element("questLog")?.Elements("Quest") ?? Enumerable.Empty<XElement>())
            .Select(e => new QuestEditor(e))
            .ToList();

    public void RemoveQuest(QuestEditor quest) => quest.Element.Remove();

    public void RemoveQuestById(string id) => FindQuest(id)?.Element.Remove();

    /// <summary>Deep-clones an existing quest - possibly from a completely different save's own
    /// PlayerEditor, QuestEditor.Element is a plain XElement so this works regardless of which
    /// document it originated in - into this player's active log. Used by cross-save recovery
    /// (StardewTools.SaveEditor.PresetSections.CopyQuests); QuestEditor.Element is internal, so
    /// this has to live here rather than in the app layer.</summary>
    public QuestEditor CloneQuestInto(QuestEditor quest)
    {
        var container = _element.Element("questLog");
        if (container is null)
        {
            container = new XElement("questLog");
            _element.Add(container);
        }

        var clone = new XElement(quest.Element);
        container.Add(clone);
        return new QuestEditor(clone);
    }

    public bool IsQuestCompleted(string id) => FindQuest(id)?.Completed ?? false;

    /// <summary>Marks a quest completed by id - if it's already in the active log (a real quest
    /// the player has encountered), just flips Accepted/Completed on the existing entry;
    /// otherwise fabricates a full instance from spec (see QuestXmlBuilder) and adds it already
    /// completed. This is what lets "any quest in the game," not just ones currently offered, be
    /// marked done - see QuestXmlBuilder's own remarks for exactly what's fabricated and why.</summary>
    public void CompleteQuest(QuestSpec spec)
    {
        var existing = FindQuest(spec.Id);
        if (existing is not null)
        {
            existing.Accepted = true;
            existing.Completed = true;
            return;
        }

        var container = _element.Element("questLog");
        if (container is null)
        {
            container = new XElement("questLog");
            _element.Add(container);
        }

        var xsiType = XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance");
        container.Add(QuestXmlBuilder.Build(spec, QuestXmlBuilder.XsiTypeFor(spec.Type), xsiType));
    }

    private QuestEditor? FindQuest(string id) => Quests.FirstOrDefault(q => q.Id == id);

    /// <summary>Known crafting/cooking recipes - both real Farmer dictionaries with the exact
    /// same shape (see RecipeListEditor). Confirmed against the real save's starter recipe sets
    /// (Chest/Wood Fence/Gate/Torch/... for crafting; Fried Egg for cooking).</summary>
    public RecipeListEditor CraftingRecipes => new(_element.Element("craftingRecipes")
        ?? throw new InvalidDataException("<player> has no <craftingRecipes> child."));

    public RecipeListEditor CookingRecipes => new(_element.Element("cookingRecipes")
        ?? throw new InvalidDataException("<player> has no <cookingRecipes> child."));

    /// <summary>The Collections page's Shipping tab - confirmed real field (Farmer.basicShipped),
    /// keyed by the shipped item's own unqualified Data/Objects.json id (a number, e.g. 24 for
    /// Parsnip) - NOT its name, unlike CraftingRecipes/CookingRecipes above. Populated real
    /// examples found in two other local saves (FarmerBrian_392578958, FarmerBrian_392648367),
    /// confirming numeric-id keys serialize as &lt;int&gt;, not &lt;string&gt; - see
    /// NameCountDictionaryEditor's remarks for why (the game's own key-encoding rule, not a
    /// per-field quirk).</summary>
    public NameCountDictionaryEditor ShippedItems => new(_element.Element("basicShipped")
        ?? throw new InvalidDataException("<player> has no <basicShipped> child."));

    /// <summary>The Collections page's Minerals tab (Farmer.mineralsFound) - keyed by unqualified
    /// item id, explicitly documented as such in the decompiled Farmer.foundMineral's own XML
    /// doc comment (not inferred). Empty in every real save available, but shares the exact same
    /// NetStringDictionary&lt;int,NetInt&gt; declaration and numeric-id key shape as
    /// ShippedItems (which does have confirmed populated real examples) - see
    /// NameCountDictionaryEditor remarks.</summary>
    public NameCountDictionaryEditor MineralsFound => new(_element.Element("mineralsFound")
        ?? throw new InvalidDataException("<player> has no <mineralsFound> child."));

    /// <summary>The Collections page's Cooking tab (Farmer.recipesCooked) - "have you actually
    /// cooked this dish at least once", distinct from CookingRecipes above ("do you know how").
    /// Keyed by the resulting dish's own item id (e.g. 194 for Fried Egg), not the recipe name -
    /// confirmed via Farmer.cookedRecipe's own itemId parameter, which is the dish produced, not
    /// looked up from a recipe-name table. Same "empty but same shape as ShippedItems" note.</summary>
    public NameCountDictionaryEditor RecipesCooked => new(_element.Element("recipesCooked")
        ?? throw new InvalidDataException("<player> has no <recipesCooked> child."));

    /// <summary>The Collections page's Fish tab (Farmer.fishCaught) - value is [timesCaught,
    /// largestSize] (confirmed: Farmer.caughtFish does `new int[2] { numberCaught, size }`), keyed
    /// by the fish's QUALIFIED item id ("(O)145", not "145") - confirmed via caughtFish's own
    /// `itemId = itemData.QualifiedItemId;` reassignment before the dictionary lookup, unlike
    /// MineralsFound/ArtifactsFound which stay unqualified. No real populated example exists in
    /// any of the 4 local saves - this shape is derived directly from decompiled source
    /// (SerializableDictionary's actual WriteXml/ReadXml plus Farmer.caughtFish), not guessed
    /// from a field-name/type declaration alone - see ArrayCountDictionaryEditor remarks.</summary>
    public ArrayCountDictionaryEditor FishCaught => new(_element.Element("fishCaught")
        ?? throw new InvalidDataException("<player> has no <fishCaught> child."));

    /// <summary>The Collections page's Artifacts tab (Farmer.archaeologyFound) - value is
    /// [timesFound, timesFound] (confirmed: Farmer.foundArtifact does `new int[2] { number,
    /// number }` - both slots really do get the same value on creation, not a typo here), keyed
    /// by the artifact's own UNQUALIFIED item id (Farmer.foundArtifact compares `itemId == "102"`
    /// directly with no qualification step, unlike FishCaught). Same "no real populated example,
    /// derived from source" caveat as FishCaught.</summary>
    public ArrayCountDictionaryEditor ArtifactsFound => new(_element.Element("archaeologyFound")
        ?? throw new InvalidDataException("<player> has no <archaeologyFound> child."));

    /// <summary>Mail flags (NetStringList on Farmer, confirmed real: a starting save already has
    /// "button_tut_1"/"button_tut_2"). Many 1.6 "Powers" (special items like the Rusty Key,
    /// Dwarvish Translation Guide, Club Card, ...) are unlocked purely by presence of a specific
    /// flag here - see Data/Powers.json's UnlockedCondition and PowersRoster in the app layer.</summary>
    public bool HasMailFlag(string flag) => MailReceivedContainer().Elements("string").Any(e => e.Value == flag);

    public void SetMailFlag(string flag, bool has)
    {
        var container = MailReceivedContainer();
        var existing = container.Elements("string").FirstOrDefault(e => e.Value == flag);
        if (has && existing is null)
            container.Add(new XElement("string", flag));
        else if (!has && existing is not null)
            existing.Remove();
    }

    private XElement MailReceivedContainer() => _element.Element("mailReceived")
        ?? throw new InvalidDataException("<player> has no <mailReceived> child.");

    /// <summary>Event IDs the player has watched (NetStringHashSet on Farmer, confirmed real: a
    /// starting save already has one, "60367"). A couple of 1.6 Powers are unlocked by having
    /// seen a specific event rather than a mail flag - see PowersRoster.</summary>
    public bool HasSeenEvent(string eventId) => EventsSeenContainer().Elements("int").Any(e => e.Value == eventId);

    public void SetSeenEvent(string eventId, bool seen)
    {
        var container = EventsSeenContainer();
        var existing = container.Elements("int").FirstOrDefault(e => e.Value == eventId);
        if (seen && existing is null)
            container.Add(new XElement("int", eventId));
        else if (!seen && existing is not null)
            existing.Remove();
    }

    private XElement EventsSeenContainer() => _element.Element("eventsSeen")
        ?? throw new InvalidDataException("<player> has no <eventsSeen> child.");

    /// <summary>Backpack capacity - a real NetInt field on Farmer (Farmer.cs: maxItems, default
    /// 12), confirmed against a real save (12 &lt;Item&gt; slots under &lt;items&gt;, exactly
    /// matching &lt;maxItems&gt;12&lt;/maxItems&gt;). Vanilla only ever sets this to 12/24/36
    /// (starting/Backpack/Deluxe Backpack), but nothing here enforces that beyond the app-layer
    /// picker (see GameEnums.BackpackSizes) - the field itself is a plain int. Setting this also
    /// resizes the inventory's own slot list to match (Inventory.Resize) - the game keeps exactly
    /// maxItems &lt;Item&gt; elements (real or xsi:nil placeholders) at all times, confirmed
    /// against that same real save.</summary>
    public int MaxItems
    {
        get => _element.GetChildInt("maxItems");
        set
        {
            // Inventory.Resize won't shrink past an occupied slot (see its own remarks) - the
            // field has to reflect whatever size actually resulted, not the raw request, or
            // maxItems would drift out of sync with the real slot count (confirmed invariant
            // against a real save: 12 &lt;Item&gt; elements, &lt;maxItems&gt;12&lt;/maxItems&gt;,
            // always equal).
            var actual = Inventory.Resize(value);
            _element.SetChildInt("maxItems", actual);
        }
    }

    public string Name
    {
        get => _element.GetChildText("name");
        set => _element.SetChildText("name", value);
    }

    /// <summary>Real, confirmed field (PascalCase, unlike most others here) - a FarmAnimal's
    /// ownerID references this. Read-only: this is the farmer's own network identity, not
    /// something a save editor should let the user casually change.</summary>
    public long UniqueMultiplayerId => long.Parse(_element.GetChildText("UniqueMultiplayerID"));

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

    /// <summary>0 = starting cabin, up to 3 (Deluxe/renovated) - the farmhouse exterior isn't a
    /// Building save entry at all, it's a fixed overlay keyed to this level (see FarmhouseSprite
    /// in StardewTools.SaveEditor).</summary>
    public int HouseUpgradeLevel
    {
        get => _element.GetChildInt("houseUpgradeLevel");
        set => _element.SetChildInt("houseUpgradeLevel", value);
    }

    /// <summary>0-10 for each skill. Confirmed against the decompiled Farmer.cs: the level
    /// fields (farmingLevel etc.) are the real, directly-persisted, authoritative values - not
    /// recomputed from experiencePoints on load - so setting one alone is safe. Each setter
    /// here also updates the matching experiencePoints slot to getBaseExperienceForLevel(level)
    /// (the exact thresholds table from Farmer.cs, and the same thing the game's own debug/
    /// cheat level-set command does at Farmer.cs:7112-7116), so further in-game XP gains behave
    /// sensibly instead of the two fields silently disagreeing.</summary>
    public int FarmingLevel { get => GetLevel("farmingLevel"); set => SetLevel("farmingLevel", 0, value); }
    public int FishingLevel { get => GetLevel("fishingLevel"); set => SetLevel("fishingLevel", 1, value); }
    public int ForagingLevel { get => GetLevel("foragingLevel"); set => SetLevel("foragingLevel", 2, value); }
    public int MiningLevel { get => GetLevel("miningLevel"); set => SetLevel("miningLevel", 3, value); }
    public int CombatLevel { get => GetLevel("combatLevel"); set => SetLevel("combatLevel", 4, value); }
    public int LuckLevel { get => GetLevel("luckLevel"); set => SetLevel("luckLevel", 5, value); }

    private int GetLevel(string field) => _element.GetChildInt(field);

    private void SetLevel(string field, int experienceSlot, int level)
    {
        _element.SetChildInt(field, level);

        var experiencePoints = _element.Element("experiencePoints");
        var slot = experiencePoints?.Elements("int").ElementAtOrDefault(experienceSlot);
        if (slot is not null)
            slot.Value = BaseExperienceForLevel(level).ToString();
    }

    /// <summary>Exact XP thresholds from the decompiled Farmer.getBaseExperienceForLevel.</summary>
    private static int BaseExperienceForLevel(int level) => level switch
    {
        1 => 100,
        2 => 380,
        3 => 770,
        4 => 1300,
        5 => 2150,
        6 => 3300,
        7 => 4800,
        8 => 6900,
        9 => 10000,
        10 => 15000,
        _ => 0,
    };
}
