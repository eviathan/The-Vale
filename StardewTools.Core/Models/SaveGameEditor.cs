using System;
using System.Linq;
using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>Typed read/write access over the &lt;SaveGame&gt; root of a loaded save file.</summary>
public sealed class SaveGameEditor
{
    private static readonly XName XsiType = XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance");

    private readonly SaveFile _saveFile;

    public SaveGameEditor(SaveFile saveFile)
    {
        _saveFile = saveFile;

        var playerElement = saveFile.Root.Element("player")
            ?? throw new InvalidDataException("Save file has no <player> element.");

        Player = new PlayerEditor(playerElement);
        Stats = new StatsEditor(playerElement.Element("stats")
            ?? throw new InvalidDataException("<player> has no <stats> element."));
        Achievements = new AchievementsEditor(playerElement.Element("achievements")
            ?? throw new InvalidDataException("<player> has no <achievements> element."));
        Friendships = new FriendshipsEditor(playerElement.Element("friendshipData")
            ?? throw new InvalidDataException("<player> has no <friendshipData> element."));
        Storage = new StorageEditor(saveFile.Root);
        Farm = new FarmEditor(saveFile.Root);

        var farmLocation = saveFile.Root.Element("locations")?.Elements("GameLocation")
            .FirstOrDefault(e => (string?)e.Attribute(XsiType) == "Farm")
            ?? throw new InvalidDataException("Save file has no Farm GameLocation.");
        Map = new FarmMapEditor(farmLocation);

        // Self-heals the real game's own habit of dropping xsi:type from Building elements that
        // need it (Junimo Hut/Stable/...) on every save - see FarmMapEditor.RepairBuildingSubtypes
        // remarks. Runs on every load so a save round-tripped through vanilla gameplay between
        // visits to this tool keeps getting fixed automatically, not just once.
        Map.RepairBuildingSubtypes();
    }

    /// <summary>The underlying raw document - exposed for undo/redo (StardewTools.SaveEditor.
    /// UndoManager), which needs to subscribe to real change events and clone/restore full
    /// snapshots without this class needing to know undo exists.</summary>
    public SaveFile SaveFile => _saveFile;

    public PlayerEditor Player { get; }
    public StatsEditor Stats { get; }
    public AchievementsEditor Achievements { get; }
    public FriendshipsEditor Friendships { get; }
    public StorageEditor Storage { get; }
    public FarmEditor Farm { get; }
    public FarmMapEditor Map { get; }

    /// <summary>
    /// Every location this save tracks (Farm, FarmHouse, Town, Beach, Mine, ...) by its real,
    /// unique &lt;name&gt; child element - confirmed real (this save alone has ~90). Previously
    /// keyed off xsi:type instead, which was wrong two ways, both confirmed against this save's
    /// real data: many real locations (Greenhouse, JoshHouse, Blacksmith, Saloon, Trailer, ...)
    /// have no xsi:type attribute at all, so they were 100% invisible to the location picker;
    /// and xsi:type doesn't even uniquely identify a location where it IS present - Cellar/
    /// Cellar2/Cellar3/Cellar4 all share xsi:type="Cellar". The real &lt;name&gt; is also exactly
    /// what Building.NonInstancedIndoorsName references (see FindLocationElement/GetLocationMap),
    /// so this fix is a prerequisite for building-interior editing, not just a picker nicety.
    /// </summary>
    public IReadOnlyList<string> LocationNames
        => _saveFile.Root.Element("locations")?.Elements("GameLocation")
            .Select(e => (string?)e.Element("name"))
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToList()
        ?? new List<string>();

    /// <summary>The raw &lt;GameLocation&gt; element for a location by its real, unique name -
    /// confirmed how Building.NonInstancedIndoorsName references its interior (e.g. Greenhouse's
    /// building points at the top-level location literally named "Greenhouse", not by xsi:type).</summary>
    public XElement? FindLocationElement(string locationName)
        => _saveFile.Root.Element("locations")?.Elements("GameLocation")
            .FirstOrDefault(e => (string?)e.Element("name") == locationName);

    /// <summary>Prefix for the synthetic, per-building-instance "location name" used to resolve a
    /// Shed/Barn/Coop's own nested &lt;indoors&gt; element (see BuildingInteriorLocationName/
    /// GetLocationMap) - these aren't in the top-level &lt;locations&gt; list at all (each placed
    /// building has its own private interior, not a shared named one like Greenhouse), so they
    /// need a lookup key distinct from any real location name. Keyed by the building's own real
    /// &lt;id&gt; GUID (confirmed unique - see BuildingEditor.Id), not by type/position, so it
    /// keeps resolving correctly even if the building is later moved.</summary>
    private const string BuildingInteriorPrefix = "__indoors__";

    /// <summary>The synthetic lookup key for a specific placed building's own &lt;indoors&gt;
    /// interior - see BuildingInteriorPrefix remarks. Null if the building has no real &lt;id&gt;
    /// (shouldn't happen for anything AddBuilding/AddFarmhouse constructs, but real save data from
    /// outside this tool could in principle predate the field).</summary>
    public static string? BuildingInteriorLocationName(BuildingEditor building)
        => building.Id is { } id ? BuildingInteriorPrefix + id : null;

    /// <summary>Reverse of BuildingInteriorLocationName - given a synthetic per-building-instance
    /// location key, finds the real Building it belongs to (e.g. so callers can look up its
    /// buildingType for things BuildingInteriorLocationName's key alone can't answer, like which
    /// real Data/Buildings.json IndoorMap art asset to load - see MapTabViewModel's
    /// IndoorMapAssetNames). Null if locationName isn't one of these synthetic keys, or the
    /// building it names no longer exists.</summary>
    public BuildingEditor? FindBuildingForInteriorLocationName(string locationName)
    {
        if (!locationName.StartsWith(BuildingInteriorPrefix, StringComparison.Ordinal))
            return null;

        var buildingId = locationName[BuildingInteriorPrefix.Length..];
        var element = Map.Buildings.FirstOrDefault(b => b.Id == buildingId)?.Element;
        return element is null ? null : new BuildingEditor(element);
    }

    /// <summary>
    /// A FarmMapEditor for any top-level location by its real name, not just Farm - confirmed
    /// that Greenhouse's and FarmHouse's &lt;GameLocation&gt; elements are structurally identical
    /// to Farm's for every field FarmMapEditor reads (objects/terrainFeatures/
    /// largeTerrainFeatures/buildings/resourceClumps, same order), so no changes to FarmMapEditor
    /// itself were needed to support this. Falls back to resolving a per-building-instance
    /// &lt;indoors&gt; element (see BuildingInteriorLocationName) when the name isn't a top-level
    /// location - FarmMapEditor's constructor has no tag-name/xsi:type check, so it already works
    /// unmodified on a nested &lt;indoors&gt; element the same way it does on a top-level one, as
    /// long as that element has the same GameLocation-shaped children (confirmed true for
    /// BuildingIndoorsEditor's own output). Null when neither resolves.
    /// </summary>
    public FarmMapEditor? GetLocationMap(string locationName)
    {
        if (FindLocationElement(locationName) is { } element)
            return new FarmMapEditor(element);

        if (!locationName.StartsWith(BuildingInteriorPrefix, StringComparison.Ordinal))
            return null;

        var buildingId = locationName[BuildingInteriorPrefix.Length..];
        var indoors = Map.Buildings.FirstOrDefault(b => b.Id == buildingId)?.Element.Element("indoors");
        return indoors is not null ? new FarmMapEditor(indoors) : null;
    }

    public string Season
    {
        get => _saveFile.Root.GetChildText("currentSeason");
        set => _saveFile.Root.SetChildText("currentSeason", value);
    }

    public int DayOfMonth
    {
        get => _saveFile.Root.GetChildInt("dayOfMonth");
        set => _saveFile.Root.SetChildInt("dayOfMonth", value);
    }

    public int Year
    {
        get => _saveFile.Root.GetChildInt("year");
        set => _saveFile.Root.SetChildInt("year", value);
    }

    public static SaveGameEditor Load(string path) => new(SaveFile.Load(path));

    public void Save(string path) => _saveFile.Save(path);
}
