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
