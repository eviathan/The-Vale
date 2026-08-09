using System;
using System.Collections.Generic;
using System.Linq;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor;

/// <summary>Which independently-copyable chunk of a save a preset section corresponds to - see
/// PresetSections remarks for why this isn't a 1:1 mapping onto the 12 UI tabs (Storage has no
/// section of its own; several tabs share the same underlying container).</summary>
public enum PresetSection
{
    Player,
    Inventory,
    Stats,
    Achievements,
    Quests,
    Recipes,
    Powers,
    Collections,
    Relationships,
    Farm,
    Map,
}

/// <summary>
/// Cross-save recovery: copies one section's data from an independent, read-only source
/// SaveGameEditor into the currently-open target SaveGameEditor - see SaveEditorViewModel.
/// ApplyPreset, the only caller. Every method here reuses the SAME Core editor getters/setters
/// each tab's own UI already uses (PlayerEditor/StatsEditor/FarmMapEditor/...), not raw XML
/// grafting - a copy can never produce a shape the app doesn't already know is valid, because it's
/// built entirely out of calls the app already makes for other reasons.
///
/// "Preset" is deliberately not a new file format - source is just any other real save file
/// (SaveGameEditor.Load), including the broken save this whole feature exists to recover from, or
/// its own automatic .bak. See SaveEditorViewModel.SavePreset for the "save a copy of the current
/// state under a new name to reuse as a source later" half of this.
///
/// Storage has no section of its own - a chest's contents travel automatically wherever the chest
/// itself physically lives (Farm/FarmHouse objects via Map, or the player's own inventory via
/// Inventory), since a chest is just a PlacedObjectEditor in those containers, not an independent
/// top-level thing. Map is scoped to the Farm and FarmHouse locations only for this pass -
/// Greenhouse and any Shed/Barn/Coop's own animals are explicitly not carried over (a Shed/Barn/
/// Coop's placed objects/furniture/terrain DO come along, since those live inside the Building
/// element FarmMapEditor.CloneEntityAt deep-clones as a whole subtree - only the separate
/// GameLocation-level &lt;animals&gt; dictionary is skipped).
/// </summary>
public static class PresetSections
{
    public static readonly IReadOnlyDictionary<PresetSection, string> Descriptions = new Dictionary<PresetSection, string>
    {
        [PresetSection.Player] = "Name, money, health/stamina, skill levels, and the current season/day/year.",
        [PresetSection.Inventory] = "Everything carried in the backpack (replaces the target's current inventory entirely).",
        [PresetSection.Stats] = "Every tracked stat (steps taken, items shipped, monsters killed, ...).",
        [PresetSection.Achievements] = "Unlocked achievements.",
        [PresetSection.Quests] = "The active quest log (accepted/completed quests).",
        [PresetSection.Recipes] = "Known crafting and cooking recipes.",
        [PresetSection.Powers] = "Qi walnut room rewards, special items (Rusty Key, ...), and other Data/Powers.json unlocks.",
        [PresetSection.Collections] = "The Collections page: shipped items, minerals, cooked dishes, caught fish, found artifacts.",
        [PresetSection.Relationships] = "Friendship points/status with every NPC the source save has met.",
        [PresetSection.Farm] = "Farm type, tomorrow's weather, daily luck, and whether the Greenhouse is unlocked.",
        [PresetSection.Map] = "Everything placed on the Farm and inside the FarmHouse (trees, crops, buildings - including what's inside a Shed/Barn/Coop - furniture, chests and their contents). Greenhouse contents and farm animals are not included yet.",
    }.AsReadOnly();

    public static void Copy(PresetSection section, SaveGameEditor source, SaveGameEditor target)
    {
        switch (section)
        {
            case PresetSection.Player: CopyPlayer(source, target); break;
            case PresetSection.Inventory: CopyInventory(source, target); break;
            case PresetSection.Stats: CopyStats(source, target); break;
            case PresetSection.Achievements: CopyAchievements(source, target); break;
            case PresetSection.Quests: CopyQuests(source, target); break;
            case PresetSection.Recipes: CopyRecipes(source, target); break;
            case PresetSection.Powers: CopyPowers(source, target); break;
            case PresetSection.Collections: CopyCollections(source, target); break;
            case PresetSection.Relationships: CopyRelationships(source, target); break;
            case PresetSection.Farm: CopyFarm(source, target); break;
            case PresetSection.Map: CopyMap(source, target); break;
            default: throw new ArgumentOutOfRangeException(nameof(section));
        }
    }

    private static void CopyPlayer(SaveGameEditor source, SaveGameEditor target)
    {
        target.Player.Name = source.Player.Name;
        target.Player.Money = source.Player.Money;
        target.Player.Health = source.Player.Health;
        target.Player.MaxHealth = source.Player.MaxHealth;
        target.Player.Stamina = source.Player.Stamina;
        target.Player.FarmingLevel = source.Player.FarmingLevel;
        target.Player.FishingLevel = source.Player.FishingLevel;
        target.Player.ForagingLevel = source.Player.ForagingLevel;
        target.Player.MiningLevel = source.Player.MiningLevel;
        target.Player.CombatLevel = source.Player.CombatLevel;
        target.Player.LuckLevel = source.Player.LuckLevel;
        target.Season = source.Season;
        target.DayOfMonth = source.DayOfMonth;
        target.Year = source.Year;
    }

    private static void CopyInventory(SaveGameEditor source, SaveGameEditor target)
    {
        foreach (var item in target.Player.Inventory.Items.ToList())
            target.Player.Inventory.Remove(item);

        foreach (var item in source.Player.Inventory.Items)
            target.Player.Inventory.AddCopy(item, item.Stack);
    }

    private static void CopyStats(SaveGameEditor source, SaveGameEditor target)
    {
        foreach (var name in source.Stats.AllFieldNames)
            target.Stats.SetRaw(name, source.Stats.GetRaw(name));
    }

    private static void CopyAchievements(SaveGameEditor source, SaveGameEditor target)
    {
        foreach (var id in target.Achievements.Ids.ToList())
            target.Achievements.Remove(id);

        foreach (var id in source.Achievements.Ids)
            target.Achievements.Add(id);
    }

    private static void CopyQuests(SaveGameEditor source, SaveGameEditor target)
    {
        foreach (var quest in target.Player.Quests.ToList())
            target.Player.RemoveQuest(quest);

        foreach (var quest in source.Player.Quests)
            target.Player.CloneQuestInto(quest);
    }

    private static void CopyRecipes(SaveGameEditor source, SaveGameEditor target)
    {
        foreach (var name in target.Player.CraftingRecipes.KnownRecipeNames.ToList())
            target.Player.CraftingRecipes.Forget(name);
        foreach (var name in source.Player.CraftingRecipes.KnownRecipeNames)
            target.Player.CraftingRecipes.Learn(name);

        foreach (var name in target.Player.CookingRecipes.KnownRecipeNames.ToList())
            target.Player.CookingRecipes.Forget(name);
        foreach (var name in source.Player.CookingRecipes.KnownRecipeNames)
            target.Player.CookingRecipes.Learn(name);
    }

    /// <summary>Mirrors PowerRowViewModel's own per-kind read/write exactly (see PowersRoster.
    /// PowerUnlockKind remarks) - a Power has no container of its own, it's a view over Player's
    /// mail flags/seen events or Stats' Values, so this only touches the specific keys the roster
    /// names rather than copying those containers wholesale (Stats/Player have their own sections
    /// for that).</summary>
    private static void CopyPowers(SaveGameEditor source, SaveGameEditor target)
    {
        foreach (var power in PowersRoster.All)
        {
            switch (power.Kind)
            {
                case PowerUnlockKind.MailFlag:
                    target.Player.SetMailFlag(power.Key, source.Player.HasMailFlag(power.Key));
                    break;
                case PowerUnlockKind.EventSeen:
                    target.Player.SetSeenEvent(power.Key, source.Player.HasSeenEvent(power.Key));
                    break;
                case PowerUnlockKind.StatDriven:
                    target.Stats.SetRaw(power.Key, source.Stats.GetRaw(power.Key));
                    break;
            }
        }
    }

    private static void CopyCollections(SaveGameEditor source, SaveGameEditor target)
    {
        CopyNameCountDictionary(source.Player.ShippedItems, target.Player.ShippedItems);
        CopyNameCountDictionary(source.Player.MineralsFound, target.Player.MineralsFound);
        CopyNameCountDictionary(source.Player.RecipesCooked, target.Player.RecipesCooked);
        CopyArrayCountDictionary(source.Player.FishCaught, target.Player.FishCaught);
        CopyArrayCountDictionary(source.Player.ArtifactsFound, target.Player.ArtifactsFound);
    }

    private static void CopyNameCountDictionary(NameCountDictionaryEditor source, NameCountDictionaryEditor target)
    {
        foreach (var key in target.Keys.ToList())
            target.Remove(key);
        foreach (var key in source.Keys)
            target.Add(key, source.Count(key));
    }

    private static void CopyArrayCountDictionary(ArrayCountDictionaryEditor source, ArrayCountDictionaryEditor target)
    {
        foreach (var key in target.Keys.ToList())
            target.Remove(key);
        foreach (var key in source.Keys)
        {
            var (first, second) = source.Values(key);
            target.Add(key, first, second);
        }
    }

    /// <summary>Only copies NPCs the source save has actually met (FriendshipsEditor.NpcNames),
    /// not the full 35-villager roster - fabricating a zero-friendship entry for someone the
    /// target player hasn't met yet isn't "copying," it's inventing data that wasn't there.</summary>
    private static void CopyRelationships(SaveGameEditor source, SaveGameEditor target)
    {
        foreach (var name in source.Friendships.NpcNames)
        {
            var sourceFriendship = source.Friendships.TryGet(name);
            if (sourceFriendship is null)
                continue;

            var targetFriendship = target.Friendships.GetOrCreate(name);
            targetFriendship.Points = sourceFriendship.Points;
            targetFriendship.GiftsThisWeek = sourceFriendship.GiftsThisWeek;
            targetFriendship.GiftsToday = sourceFriendship.GiftsToday;
            targetFriendship.TalkedToToday = sourceFriendship.TalkedToToday;
            targetFriendship.ProposalRejected = sourceFriendship.ProposalRejected;
            targetFriendship.Status = sourceFriendship.Status;
            targetFriendship.RoommateMarriage = sourceFriendship.RoommateMarriage;
        }
    }

    private static void CopyFarm(SaveGameEditor source, SaveGameEditor target)
    {
        target.Farm.WhichFarm = source.Farm.WhichFarm;
        target.Farm.WeatherForTomorrow = source.Farm.WeatherForTomorrow;
        target.Farm.DailyLuck = source.Farm.DailyLuck;
        target.Map.GreenhouseUnlocked = source.Map.GreenhouseUnlocked;
    }

    /// <summary>Clears and re-clones the Farm location's whole entity set (FarmMapEditor.
    /// ClearAllContent/CloneEntityAt - already cross-document-safe, the same deep-clone-by-value
    /// mechanism Copy/Paste already relies on), then repeats the same clear+clone for FarmHouse so
    /// upgraded-house furniture transplants too. HouseUpgradeLevel travels along with it so the
    /// target's FarmHouse map file selection matches the layout the transplanted furniture was
    /// actually placed in (see MapTabViewModel.FarmHouseMapFileNameFor) - copying the furniture
    /// without the level would leave it positioned for a room shape the target isn't showing.</summary>
    private static void CopyMap(SaveGameEditor source, SaveGameEditor target)
    {
        CopyLocationContent(source.Map, target.Map);

        if (source.GetLocationMap("FarmHouse") is { } sourceFarmHouse && target.GetLocationMap("FarmHouse") is { } targetFarmHouse)
        {
            CopyLocationContent(sourceFarmHouse, targetFarmHouse);
            target.Player.HouseUpgradeLevel = source.Player.HouseUpgradeLevel;
        }
    }

    private static void CopyLocationContent(FarmMapEditor source, FarmMapEditor target)
    {
        target.ClearAllContent();

        foreach (var tree in source.Trees) target.CloneEntityAt(tree, tree.Position);
        foreach (var grass in source.Grass) target.CloneEntityAt(grass, grass.Position);
        foreach (var fruitTree in source.FruitTrees) target.CloneEntityAt(fruitTree, fruitTree.Position);
        foreach (var dirt in source.HoeDirtTiles) target.CloneEntityAt(dirt, dirt.Position);
        foreach (var flooring in source.Flooring) target.CloneEntityAt(flooring, flooring.Position);
        foreach (var clump in source.ResourceClumps) target.CloneEntityAt(clump, clump.Position);
        foreach (var bush in source.Bushes) target.CloneEntityAt(bush, bush.Position);
        foreach (var obj in source.Objects) target.CloneEntityAt(obj, obj.Position);
        foreach (var furniture in source.Furniture) target.CloneEntityAt(furniture, furniture.Position);
        // Buildings last - CloneEntityAt mints each a fresh id (same as Copy/Paste already does,
        // safe here too since nothing in the target document could already reference the source's
        // building ids). Each Building's nested <indoors> (a Shed/Barn/Coop's own placed content)
        // comes along automatically, since it's just a descendant of the element being cloned.
        foreach (var building in source.Buildings) target.CloneEntityAt(building, building.Position);
    }
}
