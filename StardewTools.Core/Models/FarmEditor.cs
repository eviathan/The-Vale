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

    /// <summary>The vanilla farm-type id used for 7=Meadowlands (added via the same
    /// Data/AdditionalFarms.json mod-farm-type system third-party farms use, confirmed via
    /// AdditionalFarms.json's own "MeadowlandsFarm" entry).</summary>
    public const string MeadowlandsFarmId = "MeadowlandsFarm";

    /// <summary>0=Standard, 1=Riverland, 2=Forest, 3=Hilltop, 4=Wilderness, 5=FourCorners,
    /// 6=Beach, 7=Meadowlands. The real save field (SaveGame.whichFarm) is a STRING, not an int -
    /// confirmed via decompiled SaveGame.cs: it writes Game1.whichFarm.ToString() for values 0-6,
    /// but the literal id string "MeadowlandsFarm" for 7 (a mod-farm-type id, since Meadowlands
    /// was added through the same extensible AdditionalFarms system real farm mods use, even
    /// though it ships vanilla). A plain GetChildInt/SetChildInt would silently corrupt a
    /// Meadowlands save - parsing "MeadowlandsFarm" as an int fails and defaults to 0 on read,
    /// and writing a bare "7" back on set would break the real id the game expects to find.</summary>
    public int WhichFarm
    {
        get
        {
            var raw = _root.GetChildText("whichFarm");
            if (raw == MeadowlandsFarmId)
                return 7;
            return int.TryParse(raw, out var value) ? value : 0;
        }
        set => _root.SetChildText("whichFarm", value == 7 ? MeadowlandsFarmId : value.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
