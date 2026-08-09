using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>An extra tile (relative to the building's own top-left) that also needs to be
/// valid ground before the building can be placed - e.g. Farmhouse's mailbox tile sits outside
/// its main footprint. Confirmed real, enforced validation (not just a rendering/interaction
/// hint) against the decompiled GameLocation.buildStructure, which checks isBuildable for every
/// one of these in addition to the building's core TilesWide x TilesHigh rectangle.</summary>
public sealed record PlacementTileArea(int X, int Y, int Width, int Height, bool OnlyNeedsToBePassable);

/// <summary>Real per-building data needed to construct a new placed Building
/// (FarmMapEditor.AddBuilding). Limited to Data/Buildings.json entries with no interior
/// (IndoorMap and NonInstancedIndoorLocation both null) - confirmed these all share a HumanDoor
/// of (-1,-1), i.e. no door, nothing to wire up: Gold Clock, the 4 Obelisks, Well, Silo, Mill,
/// Fish Pond, Pet Bowl, Stable, Shipping Bin, Junimo Hut - plus Shed and the Barn/Coop family,
/// whose real nested &lt;indoors&gt; interior FarmMapEditor.AddBuilding now writes (see
/// BuildingIndoorsEditor). Big Shed and cabins still need interior fields (upgradeLevel wiring,
/// Cabin's own farmhand-slot fields) not implemented yet, so they stay excluded here rather than
/// risk a building whose door leads nowhere. IndoorMap/MaxOccupants are the two real
/// Data/Buildings.json fields BuildingIndoorsEditor.CreateDefaultAnimalHouse needs (the building's
/// interior map name and animal capacity) - null/0 for anything without a supported interior.</summary>
public sealed record PlaceableBuilding(string Name, int TilesWide, int TilesHigh, bool Magical, int HayCapacity,
    string? IndoorMap, int MaxOccupants, IReadOnlyList<PlacementTileArea> AdditionalPlacementTiles)
{
    public override string ToString() => $"{Name} ({TilesWide}x{TilesHigh})";
}

public static class PlaceableBuildings
{
    private static IReadOnlyList<PlaceableBuilding>? _all;

    public static IReadOnlyList<PlaceableBuilding> All => _all ??= Load();

    private static List<PlaceableBuilding> Load()
    {
        var result = new List<PlaceableBuilding>();
        var path = Path.Combine(BundledContent.FolderPath, "Data", "Buildings.json");
        if (!File.Exists(path))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var el = prop.Value;

            var hasIndoorMap = el.TryGetProperty("IndoorMap", out var indoor) && indoor.ValueKind != JsonValueKind.Null;
            var hasNonInstanced = el.TryGetProperty("NonInstancedIndoorLocation", out var nonInstanced) && nonInstanced.ValueKind != JsonValueKind.Null;
            // "Big Shed" shares IndoorMapType "StardewValley.Shed" with plain "Shed" (same Shed
            // class, upgradeLevel 1) but needs its own map name ("Shed2", not "Shed") and
            // upgradeLevel wired into BuildingIndoorsEditor before it's safe to place - not done
            // yet, so this checks the exact key, not just the shared IndoorMapType string. Same
            // reasoning extends the allowlist to the Barn/Coop family (all six share
            // IndoorMapType "StardewValley.AnimalHouse", each with its own real IndoorMap name).
            var hasSupportedIndoors = prop.Name is "Shed" or "Barn" or "Big Barn" or "Deluxe Barn" or "Coop" or "Big Coop" or "Deluxe Coop";
            if ((hasIndoorMap || hasNonInstanced) && !hasSupportedIndoors)
                continue;

            var width = el.TryGetProperty("Size", out var size) && size.TryGetProperty("X", out var w) ? w.GetInt32() : 1;
            var height = el.TryGetProperty("Size", out size) && size.TryGetProperty("Y", out var h) ? h.GetInt32() : 1;
            var magical = el.TryGetProperty("MagicalConstruction", out var m) && m.GetBoolean();
            var hayCapacity = el.TryGetProperty("HayCapacity", out var hc) ? hc.GetInt32() : 0;
            var indoorMap = hasIndoorMap ? indoor.GetString() : null;
            var maxOccupants = el.TryGetProperty("MaxOccupants", out var mo) ? mo.GetInt32() : 0;

            var additionalTiles = new List<PlacementTileArea>();
            if (el.TryGetProperty("AdditionalPlacementTiles", out var apt) && apt.ValueKind == JsonValueKind.Array)
            {
                foreach (var tile in apt.EnumerateArray())
                {
                    if (!tile.TryGetProperty("TileArea", out var area))
                        continue;

                    var x = area.TryGetProperty("X", out var ax) ? ax.GetInt32() : 0;
                    var y = area.TryGetProperty("Y", out var ay) ? ay.GetInt32() : 0;
                    var w2 = area.TryGetProperty("Width", out var aw) ? aw.GetInt32() : 1;
                    var h2 = area.TryGetProperty("Height", out var ah) ? ah.GetInt32() : 1;
                    var onlyPassable = tile.TryGetProperty("OnlyNeedsToBePassable", out var op) && op.ValueKind == JsonValueKind.True;

                    additionalTiles.Add(new PlacementTileArea(x, y, w2, h2, onlyPassable));
                }
            }

            result.Add(new PlaceableBuilding(prop.Name, width, height, magical, hayCapacity, indoorMap, maxOccupants, additionalTiles));
        }

        return result.OrderBy(b => b.Name).ToList();
    }
}
