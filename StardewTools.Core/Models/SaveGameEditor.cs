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

    /// <summary>
    /// A FarmMapEditor for any top-level location by its real name, not just Farm - confirmed
    /// that Greenhouse's and FarmHouse's &lt;GameLocation&gt; elements are structurally identical
    /// to Farm's for every field FarmMapEditor reads (objects/terrainFeatures/
    /// largeTerrainFeatures/buildings/resourceClumps, same order), so no changes to FarmMapEditor
    /// itself were needed to support this. Null when the name doesn't resolve to a real location -
    /// covers both "no such location" and the per-instance-&lt;indoors&gt; case (Barn/Coop/Shed),
    /// which isn't in the top-level &lt;locations&gt; list at all and has no real save evidence to
    /// confirm its shape against, so it's deliberately not attempted here.
    /// </summary>
    public FarmMapEditor? GetLocationMap(string locationName)
        => FindLocationElement(locationName) is { } element ? new FarmMapEditor(element) : null;

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
