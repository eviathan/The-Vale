using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Real per-tool data needed to fabricate a new Tool item (ItemListEditor.SetSlotToNewTool) -
/// Name/SpriteIndex/MenuSpriteIndex/UpgradeLevel read from Data/Tools.json. Deliberately limited
/// to the 4 classes with a verified real-example XML shape (Axe/Hoe/Pickaxe/WateringCan - see
/// ToolXmlBuilder) - MilkPail/Pan/Shears/FishingRod/Wand/Lantern/GenericTool aren't offered here
/// even though they're in Data/Tools.json, since fabricating their exact save shape hasn't been
/// verified against a real example.</summary>
public sealed record PlaceableTool(string Id, string ClassName, string Name, int SpriteIndex, int MenuSpriteIndex, int UpgradeLevel)
{
    /// <summary>Matches ToolDataDefinition.GetData's own fallback (MenuSpriteIndex > -1 ?
    /// MenuSpriteIndex : SpriteIndex) - confirmed against the decompiled source, not guessed.</summary>
    public int IconSpriteIndex => MenuSpriteIndex > -1 ? MenuSpriteIndex : SpriteIndex;

    public override string ToString() => Name;
}

public static class PlaceableTools
{
    private static readonly HashSet<string> FabricatableClasses = new() { "Axe", "Hoe", "Pickaxe", "WateringCan" };

    private static IReadOnlyList<PlaceableTool>? _all;

    public static IReadOnlyList<PlaceableTool> All => _all ??= Load();

    private static List<PlaceableTool> Load()
    {
        var result = new List<PlaceableTool>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Tools.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var el = prop.Value;
            var className = el.TryGetProperty("ClassName", out var cn) ? cn.GetString() ?? "" : "";
            if (!FabricatableClasses.Contains(className))
                continue;

            var name = el.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
            var spriteIndex = el.TryGetProperty("SpriteIndex", out var si) ? si.GetInt32() : 0;
            var menuSpriteIndex = el.TryGetProperty("MenuSpriteIndex", out var msi) ? msi.GetInt32() : -1;
            var upgradeLevel = el.TryGetProperty("UpgradeLevel", out var ul) ? ul.GetInt32() : 0;

            if (name.Length > 0)
                result.Add(new PlaceableTool(prop.Name, className, name, spriteIndex, menuSpriteIndex, upgradeLevel));
        }

        return result.OrderBy(t => t.Name).ToList();
    }
}
