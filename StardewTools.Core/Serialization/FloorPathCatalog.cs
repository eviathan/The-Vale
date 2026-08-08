namespace StardewTools.Core.Serialization;

/// <summary>
/// Maps a placeable floor/path item's real Data/Objects.json id to its Data/FloorsAndPaths.json
/// key (the "whichFloor" a placed Flooring terrain feature stores) - confirmed by cross-reading
/// both files directly: every FloorsAndPaths.json entry's own "ItemId" field names exactly one of
/// these ids, and no other entry shares it. Verified against the decompiled
/// StardewValley.Object.placementAction's IsFloorPathItem() branch: these items don't become a
/// plain placed Object at all - they add a Flooring to terrainFeatures instead (see
/// FarmMapEditor.AddFlooring), same redirection shape as ExoticObjectCatalog's Chest/Fence/etc.
/// entries, just landing in a different dictionary instead of getting a different xsi:type.
/// </summary>
internal static class FloorPathCatalog
{
    private static readonly Dictionary<int, string> ById = new()
    {
        [328] = "0", // Wood Floor
        [329] = "1", // Stone Floor
        [331] = "2", // Weathered Floor
        [333] = "3", // Crystal Floor
        [401] = "4", // Straw Floor
        [407] = "5", // Gravel Path
        [405] = "6", // Wood Path
        [409] = "7", // Crystal Path
        [411] = "8", // Cobblestone Path
        [415] = "9", // Stepping Stone Path
        [293] = "10", // Brick Floor
        [840] = "11", // Rustic Plank Floor
        [841] = "12", // Stone Walkway Floor
    };

    public static bool IsKnown(int parentSheetIndex) => ById.ContainsKey(parentSheetIndex);

    public static string Lookup(int parentSheetIndex) => ById[parentSheetIndex];
}
