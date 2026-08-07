using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>Typed access over farm/world-level fields at the &lt;SaveGame&gt; root.</summary>
public sealed class FarmEditor
{
    private readonly XElement _root;

    public FarmEditor(XElement saveGameRoot)
    {
        _root = saveGameRoot;
    }

    /// <summary>0=Standard, 1=Riverland, 2=Forest, 3=Hilltop, 4=Wilderness, 5=FourCorners, 6=Beach, 7=MeadowlandsFarm.</summary>
    public int WhichFarm
    {
        get => _root.GetChildInt("whichFarm");
        set => _root.SetChildInt("whichFarm", value);
    }

    /// <summary>Real field is `public static string Game1.weatherForTomorrow` - a weather
    /// condition id ("Sun", "Rain", "Wedding", ...), not an int. An earlier version of this
    /// property assumed int (confirmed only against one real save that happened to have a plain
    /// numeric string here) - a more-progressed real save proved that wrong (a genuine
    /// `<weatherForTomorrow>Rain</weatherForTomorrow>`, which crashed the old int.Parse-based
    /// getter). Kept as a plain string rather than a curated dropdown since the full real id
    /// list isn't confirmed - Data/Locations.json's own WeatherConditions lists came up empty
    /// for every location checked, so "Sun"/"Rain"/"Wedding" (the only 3 ids actually seen
    /// in real save data or literal string comparisons in the decompiled source) are the only
    /// ones with real evidence; a free-text field never blocks a value this tool hasn't
    /// enumerated, unlike a closed picker would.</summary>
    public string WeatherForTomorrow
    {
        get => _root.GetChildText("weatherForTomorrow");
        set => _root.SetChildText("weatherForTomorrow", value);
    }

    public double DailyLuck
    {
        get => _root.GetChildDouble("dailyLuck");
        set => _root.SetChildDouble("dailyLuck", value);
    }

    /// <summary>
    /// Best-effort building type listing (Coop, Barn, Shed, ...) across every location.
    /// Unverified against real data - this save had no constructed buildings yet, so the
    /// "buildingType" element name is taken from general schema knowledge, not confirmed
    /// here. Read-only regardless: editing/adding buildings needs each building's full
    /// indoor location serialized alongside it, which isn't something we can safely fabricate.
    /// </summary>
    public IReadOnlyList<string> BuildingTypes
        => _root.Descendants("buildingType")
            .Select(e => e.Value)
            .ToList();
}
