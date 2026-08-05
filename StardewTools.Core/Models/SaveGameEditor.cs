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

    public PlayerEditor Player { get; }
    public StatsEditor Stats { get; }
    public AchievementsEditor Achievements { get; }
    public FriendshipsEditor Friendships { get; }
    public StorageEditor Storage { get; }
    public FarmEditor Farm { get; }
    public FarmMapEditor Map { get; }

    /// <summary>
    /// Every location this save tracks (Farm, FarmHouse, Town, Beach, Mine, ...) by its
    /// xsi:type - confirmed real (this save alone has dozens). Only Farm has a typed editor
    /// with placed-entity access so far; this is what a location picker can offer to at
    /// least *view* (real tile art, no entity overlay) for everywhere else.
    /// </summary>
    public IReadOnlyList<string> LocationNames
        => _saveFile.Root.Element("locations")?.Elements("GameLocation")
            .Select(e => (string?)e.Attribute(XsiType))
            .Where(name => name is not null)
            .Select(name => name!)
            .Distinct()
            .ToList()
        ?? new List<string>();

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
