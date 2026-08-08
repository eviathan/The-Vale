using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Real per-item data needed to construct a new placed Object
/// (FarmMapEditor.AddObject) - Name/Category/Price/Edibility/Type, all read from the game's
/// own unpacked data rather than guessed or copied from a save's own (sometimes corrupted -
/// see FarmMapEditor.AddObject remarks) per-instance fields. IsBigCraftable distinguishes
/// Data/BigCraftables.json entries (machines/decorations like a Furnace or Grandfather Clock)
/// from Data/Objects.json ones (everything else) - both share the same underlying save
/// element shape, but are two entirely separate id spaces that happen to overlap numerically
/// (e.g. index 68 is a different item in each file), so this flag is what
/// FarmMapEditor.AddObject needs to know which index space parentSheetIndex refers to.</summary>
public sealed record PlaceableItem(int Index, string Name, int Category, int Price, int Edibility, string Type, bool IsBigCraftable, string? ItemId = null)
{
    public override string ToString() => IsBigCraftable ? $"{Name} ({Index}, craftable)" : $"{Name} ({Index})";

    /// <summary>The real save-XML identity (Item.ItemId - confirmed via decompiled Item.cs to be
    /// the authoritative field in 1.6, with ParentSheetIndex/Index now just a derived sprite
    /// lookup) - a plain numeric string for legacy items (where the two happen to be equal), or
    /// the real id (e.g. "BigChest") for items introduced with a non-numeric key.</summary>
    public string RealItemId => ItemId ?? Index.ToString();
}

public static class PlaceableItems
{
    private static IReadOnlyList<PlaceableItem>? _all;

    public static IReadOnlyList<PlaceableItem> All => _all ??= Load();

    private static List<PlaceableItem> Load()
    {
        var result = new List<PlaceableItem>();
        LoadObjects(result);
        LoadBigCraftables(result);
        return result.OrderBy(i => i.Name).ToList();
    }

    private static void LoadObjects(List<PlaceableItem> result)
    {
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Objects.json");
        if (!File.Exists(path))
            return;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (!int.TryParse(prop.Name, out var index))
                continue;

            var el = prop.Value;
            var name = el.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
            var category = el.TryGetProperty("Category", out var c) ? c.GetInt32() : 0;
            var price = el.TryGetProperty("Price", out var p) ? p.GetInt32() : 0;
            var edibility = el.TryGetProperty("Edibility", out var e) ? e.GetInt32() : -300;
            var type = el.TryGetProperty("Type", out var t) ? t.GetString() ?? "Basic" : "Basic";

            if (name.Length > 0)
                result.Add(new PlaceableItem(index, name, category, price, edibility, type, IsBigCraftable: false));
        }
    }

    /// <summary>Data/BigCraftables.json has no Category/Edibility/Type concept (confirmed - a
    /// real entry, Grandfather Clock, only has Name/Price/Fragility/CanBePlaced.../SpriteIndex)
    /// since these are placed machines/decorations, not produce - category 0 and edibility
    /// -300 (the same "not edible" sentinel Objects.json uses) are safe, inert defaults.
    ///
    /// A real, non-numeric key (e.g. "BigChest", "HeavyFurnace" - 13 total, all 1.6+ additions)
    /// used to be silently skipped entirely here, since the whole placement pipeline assumed a
    /// numeric id - real player-reported gap ("big chest modification" missing). Confirmed via
    /// direct inspection that every one of these 13 has `Texture: null`, meaning (same as every
    /// legacy numeric entry) it draws from the default shared BigCraftables sheet at its own real
    /// `SpriteIndex` - no new texture/rendering work needed, only carrying the real string
    /// ItemId through instead of assuming ItemId always equals the numeric key.</summary>
    private static void LoadBigCraftables(List<PlaceableItem> result)
    {
        var path = Path.Combine(BundledContent.FolderPath, "Data", "BigCraftables.json");
        if (!File.Exists(path))
            return;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var el = prop.Value;
            var name = el.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
            var price = el.TryGetProperty("Price", out var p) ? p.GetInt32() : 0;
            var hasCustomTexture = el.TryGetProperty("Texture", out var t) && t.ValueKind == JsonValueKind.String;

            var isNumeric = int.TryParse(prop.Name, out var numericIndex);
            if (!isNumeric)
            {
                // Not on the default shared sheet - would need real per-item texture-loading
                // support this tool doesn't have yet (see ObjectSprites remarks). Skip rendering
                // it as a real sprite for now rather than guess, but every non-numeric id here
                // that DOES use the default sheet (all 13 as of this writing) is still included.
                if (hasCustomTexture || !el.TryGetProperty("SpriteIndex", out var si))
                    continue;
                numericIndex = si.GetInt32();
            }

            if (name.Length > 0)
                result.Add(new PlaceableItem(numericIndex, name, 0, price, -300, "Crafting", IsBigCraftable: true, ItemId: isNumeric ? null : prop.Name));
        }
    }
}
