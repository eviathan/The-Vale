using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Real per-weapon data needed to fabricate a new MeleeWeapon item
/// (ItemListEditor.SetSlotToNewWeapon) - every field read from Data/Weapons.json, verified
/// against a real carried Scythe's exact XML shape (see WeaponXmlBuilder). Covers every entry in
/// Data/Weapons.json - swords/daggers/clubs/scythes all share the identical field shape, only
/// "Type" differs.</summary>
public sealed record PlaceableWeapon(string Id, string Name, int SpriteIndex, int Type, int MinDamage, int MaxDamage,
    int Speed, int Precision, int Defense, int AreaOfEffect, double Knockback, double CritChance, double CritMultiplier)
{
    public override string ToString() => Name;
}

public static class PlaceableWeapons
{
    private static IReadOnlyList<PlaceableWeapon>? _all;

    public static IReadOnlyList<PlaceableWeapon> All => _all ??= Load();

    private static List<PlaceableWeapon> Load()
    {
        var result = new List<PlaceableWeapon>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Weapons.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var el = prop.Value;
            var name = el.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
            var spriteIndex = el.TryGetProperty("SpriteIndex", out var si) ? si.GetInt32() : 0;
            var type = el.TryGetProperty("Type", out var t) ? t.GetInt32() : 0;
            var minDamage = el.TryGetProperty("MinDamage", out var mind) ? mind.GetInt32() : 1;
            var maxDamage = el.TryGetProperty("MaxDamage", out var maxd) ? maxd.GetInt32() : 1;
            var speed = el.TryGetProperty("Speed", out var sp) ? sp.GetInt32() : 0;
            var precision = el.TryGetProperty("Precision", out var pr) ? pr.GetInt32() : 0;
            var defense = el.TryGetProperty("Defense", out var df) ? df.GetInt32() : 0;
            var areaOfEffect = el.TryGetProperty("AreaOfEffect", out var ae) ? ae.GetInt32() : 0;
            var knockback = el.TryGetProperty("Knockback", out var kb) ? kb.GetDouble() : 1.0;
            var critChance = el.TryGetProperty("CritChance", out var cc) ? cc.GetDouble() : 0.02;
            var critMultiplier = el.TryGetProperty("CritMultiplier", out var cm) ? cm.GetDouble() : 3.0;

            if (name.Length > 0)
                result.Add(new PlaceableWeapon(prop.Name, name, spriteIndex, type, minDamage, maxDamage, speed, precision, defense, areaOfEffect, knockback, critChance, critMultiplier));
        }

        return result.OrderBy(w => w.Name).ToList();
    }
}
