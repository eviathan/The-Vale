using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

public enum MapEntityKind
{
    Tree,
    Grass,
    HoeDirt,
    ResourceClump,
    Object,
    Building,
    Bush,
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

    public static MapEntitySummary FromHoeDirt(HoeDirtEditor dirt) => new()
    {
        Position = dirt.Position,
        Kind = MapEntityKind.HoeDirt,
        Label = dirt.Crop is { } crop
            ? $"Crop (phase {crop.CurrentPhase}, {(dirt.State == 1 ? "watered" : "dry")}){(crop.Dead ? " [dead]" : "")}"
            : $"Tilled soil ({(dirt.State == 1 ? "watered" : "dry")})",
        ColorHex = dirt.Crop is not null ? "#4CAF50" : (dirt.State == 1 ? "#4E342E" : "#795548"),
        Source = dirt,
    };

    public static MapEntitySummary FromClump(ResourceClumpEditor clump) => new()
    {
        Position = clump.Position,
        Kind = MapEntityKind.ResourceClump,
        Label = $"Resource clump (sheet {clump.ParentSheetIndex}, {clump.Width}x{clump.Height})",
        ColorHex = "#795548",
        Source = clump,
        Width = clump.Width,
        Height = clump.Height,
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

    private static readonly string[] SizeNames = { "small", "medium", "large", "tea", "walnut" };

    /// <summary>Footprint width in tiles per size - confirmed against the decompiled
    /// Bush.getBoundingBox() (sizes 0/3 -> 64px/1 tile, 1/4 -> 128px/2 tiles, 2 -> 192px/3
    /// tiles). Footprint height is always 1 tile.</summary>
    public static int FootprintWidth(int size) => size switch { 2 => 3, 1 or 4 => 2, _ => 1 };

    public static MapEntitySummary FromBush(BushEditor bush) => new()
    {
        Position = bush.Position,
        Kind = MapEntityKind.Bush,
        Label = $"Bush ({(bush.Size is >= 0 and < 5 ? SizeNames[bush.Size] : bush.Size.ToString())})",
        ColorHex = "#33691E",
        Source = bush,
        Width = FootprintWidth(bush.Size),
        Height = 1,
    };
}
