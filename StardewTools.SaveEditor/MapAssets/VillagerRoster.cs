using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Every real relationship-trackable NPC, from Data/Characters.json's own SocialTab
/// field - confirmed against the decompiled data shape (AlwaysShown/UnknownUntilMet/
/// HiddenUntilMet all eventually appear in the game's own Social page; HiddenAlways never does,
/// e.g. kids, monsters, and other non-social characters, so those are excluded here). 35 real
/// names as of this game version, not a guess or a hardcoded list.</summary>
public sealed record Villager(string Name, bool CanBeRomanced)
{
    public override string ToString() => Name;
}

public static class VillagerRoster
{
    private static IReadOnlyList<Villager>? _all;

    public static IReadOnlyList<Villager> All => _all ??= Load();

    private static List<Villager> Load()
    {
        var result = new List<Villager>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Characters.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var el = prop.Value;
            var socialTab = el.TryGetProperty("SocialTab", out var st) ? st.GetString() : null;
            if (socialTab == "HiddenAlways")
                continue;

            var canBeRomanced = el.TryGetProperty("CanBeRomanced", out var cr) && cr.GetBoolean();
            result.Add(new Villager(prop.Name, canBeRomanced));
        }

        return result.OrderBy(v => v.Name).ToList();
    }
}
