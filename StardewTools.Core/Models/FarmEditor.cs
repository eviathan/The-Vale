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

    public int WeatherForTomorrow
    {
        get => _root.GetChildInt("weatherForTomorrow");
        set => _root.SetChildInt("weatherForTomorrow", value);
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
