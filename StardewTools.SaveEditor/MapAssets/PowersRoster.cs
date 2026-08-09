using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

public enum PowerUnlockKind
{
    /// <summary>Settable - toggles a specific string's presence in Farmer.mailReceived.</summary>
    MailFlag,

    /// <summary>Settable - toggles a specific event id's presence in Farmer.eventsSeen.</summary>
    EventSeen,

    /// <summary>Settable - the real condition is "Stats.Get(key) &gt;= 1" (decompiled Stats.cs's
    /// own Get/Set), i.e. a key in the Values dictionary is nonzero. Confirmed against decompiled
    /// Stats.cs: `stat_dictionary` (the field this was originally modeled after) is itself marked
    /// obsolete - "kept to preserve data from old save files" - the real, current mechanism
    /// PLAYER_STAT reads is Stats.Values (StatsEditor.GetRaw/SetRaw, already implemented for the
    /// Stats tab, its exact XML shape confirmed against a real save's own populated Values
    /// dictionary), not stat_dictionary at all.</summary>
    StatDriven,
}

/// <summary>Real "Power" from Data/Powers.json (36 entries in 1.6) - covers what used to be
/// called "special items" (Rusty Key, Skull Key, Dwarvish Translation Guide, ...) as well as
/// Qi Walnut Room unlocks (skill books, masteries). Each entry's UnlockedCondition string is
/// parsed to find which underlying save mechanism actually grants it - confirmed by reading
/// every real UnlockedCondition value in the shipped Data/Powers.json, not guessed: 11 use
/// "PLAYER_HAS_MAIL &lt;Current|Host&gt; &lt;flag&gt;", 2 use "PLAYER_HAS_SEEN_EVENT Current
/// &lt;id&gt;", the remaining 23 (all Book_*/Mastery_*) use "PLAYER_STAT Current &lt;key&gt; 1".</summary>
public sealed record GamePower(string Id, string DisplayName, PowerUnlockKind Kind, string Key)
{
    public override string ToString() => DisplayName;
}

public static class PowersRoster
{
    private static IReadOnlyList<GamePower>? _all;

    public static IReadOnlyList<GamePower> All => _all ??= Load();

    private static List<GamePower> Load()
    {
        var result = new List<GamePower>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Powers.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var condition = prop.Value.TryGetProperty("UnlockedCondition", out var uc) ? uc.GetString() ?? "" : "";
            var tokens = condition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3)
                continue;

            var (kind, key) = tokens[0] switch
            {
                "PLAYER_HAS_MAIL" => (PowerUnlockKind.MailFlag, tokens[2]),
                "PLAYER_HAS_SEEN_EVENT" => (PowerUnlockKind.EventSeen, tokens[2]),
                "PLAYER_STAT" => (PowerUnlockKind.StatDriven, tokens[2]),
                _ => (PowerUnlockKind.StatDriven, ""),
            };

            if (key.Length == 0)
                continue;

            result.Add(new GamePower(prop.Name, Humanize(prop.Name), kind, key));
        }

        return result.OrderBy(p => p.Kind).ThenBy(p => p.DisplayName).ToList();
    }

    /// <summary>"DwarvishTranslationGuide" -> "Dwarvish Translation Guide", "Book_PriceCatalogue"
    /// -> "Book: Price Catalogue" - the real DisplayName field is a [LocalizedText ...] reference
    /// this tool doesn't resolve (same simplification already used for stat field names - see
    /// StatRowViewModel.Humanize), so the Id itself (already descriptive) is humanized instead.</summary>
    private static string Humanize(string id)
    {
        var parts = id.Split('_', 2);
        var sb = new StringBuilder();
        for (var i = 0; i < parts[0].Length; i++)
        {
            var c = parts[0][i];
            if (i == 0) { sb.Append(char.ToUpperInvariant(c)); continue; }
            if (char.IsUpper(c)) sb.Append(' ');
            sb.Append(c);
        }

        if (parts.Length > 1)
        {
            sb.Append(": ");
            for (var i = 0; i < parts[1].Length; i++)
            {
                var c = parts[1][i];
                if (i == 0) { sb.Append(char.ToUpperInvariant(c)); continue; }
                if (char.IsUpper(c)) sb.Append(' ');
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
