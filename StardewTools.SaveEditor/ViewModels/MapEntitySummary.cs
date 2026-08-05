using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

public enum MapEntityKind
{
    Tree,
    Grass,
    ResourceClump,
    Object,
    Building,
}

/// <summary>
/// A uniform, render-friendly view over the map entity types Core exposes, so the map
/// control only needs to know about one shape instead of several. <see cref="Source"/>
/// keeps the real typed editor around for the details panel and removal.
/// </summary>
public sealed class MapEntitySummary
{
    public required TilePosition Position { get; init; }
    public required MapEntityKind Kind { get; init; }
    public required string Label { get; init; }
    public required string ColorHex { get; init; }
    public required object Source { get; init; }

    /// <summary>Footprint in tiles - 1x1 for everything except buildings.</summary>
    public int Width { get; init; } = 1;
    public int Height { get; init; } = 1;

    public static MapEntitySummary FromTree(TreeEditor tree) => new()
    {
        Position = tree.Position,
        Kind = MapEntityKind.Tree,
        Label = $"Tree (stage {tree.GrowthStage}, type {tree.TreeType}){(tree.Stump ? " [stump]" : "")}",
        ColorHex = tree.Stump ? "#6B4A2F" : "#2E7D32",
        Source = tree,
    };

    public static MapEntitySummary FromGrass(GrassEditor grass) => new()
    {
        Position = grass.Position,
        Kind = MapEntityKind.Grass,
        Label = $"Grass (type {grass.GrassType}, {grass.NumberOfWeeds} weeds)",
        ColorHex = "#8BC34A",
        Source = grass,
    };

    public static MapEntitySummary FromClump(ResourceClumpEditor clump) => new()
    {
        Position = clump.Position,
        Kind = MapEntityKind.ResourceClump,
        Label = $"Resource clump (sheet {clump.ParentSheetIndex}, {clump.Width}x{clump.Height})",
        ColorHex = "#795548",
        Source = clump,
    };

    public static MapEntitySummary FromObject(PlacedObjectEditor placed) => new()
    {
        Position = placed.Position,
        Kind = MapEntityKind.Object,
        Label = string.IsNullOrEmpty(placed.Item.ItemType)
            ? placed.Item.Name
            : $"{placed.Item.Name} ({placed.Item.ItemType})",
        ColorHex = "#FF9800",
        Source = placed,
    };

    public static MapEntitySummary FromBuilding(BuildingEditor building) => new()
    {
        Position = building.Position,
        Kind = MapEntityKind.Building,
        Label = $"{building.BuildingType} ({building.Width}x{building.Height}) - unverified field names, see README",
        ColorHex = "#B71C1C",
        Source = building,
        Width = building.Width,
        Height = building.Height,
    };
}
