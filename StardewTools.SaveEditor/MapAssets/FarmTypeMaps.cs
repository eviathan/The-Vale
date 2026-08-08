using System.Collections.Generic;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>Real farm-type index (SaveGame's whichFarm, see FarmEditor.WhichFarm) -&gt; the real
/// .tmx map file that farm type uses - confirmed via decompiled Farm.getMapNameFromTypeInt and
/// Data/AdditionalFarms.json's "MeadowlandsFarm" entry (MapName: "Farm_Ranching"). The game
/// derives this itself at load time (Game1.cs: `new Farm("Maps\\" +
/// Farm.getMapNameFromTypeInt(Game1.whichFarm), "Farm")`, called for both a brand-new game AND
/// loading an existing save) - mapPath itself is never persisted in the save (marked
/// [XmlIgnore] on GameLocation), so writing whichFarm alone is enough for the real game to pick
/// the right map on next load. Our own Map tab previously hardcoded "Farm.tmx" regardless of
/// whichFarm - this lookup is what FarmMapControl.FarmMapFileName is set from instead.</summary>
public static class FarmTypeMaps
{
    private static readonly Dictionary<int, string> MapNames = new()
    {
        [0] = "Farm",
        [1] = "Farm_Fishing",
        [2] = "Farm_Foraging",
        [3] = "Farm_Mining",
        [4] = "Farm_Combat",
        [5] = "Farm_FourCorners",
        [6] = "Farm_Island",
        [7] = "Farm_Ranching",
    };

    public static string MapNameFor(int whichFarm) => MapNames.TryGetValue(whichFarm, out var name) ? name : "Farm";
}
