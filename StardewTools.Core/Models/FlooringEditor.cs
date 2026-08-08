using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// A placed floor/path tile - lives in the same terrainFeatures tile dictionary as Tree/Grass/
/// HoeDirt (&lt;TerrainFeature xsi:type="Flooring"&gt;), confirmed against the decompiled
/// StardewValley.TerrainFeatures.Flooring's own NetFields (whichFloor, whichView - the base
/// TerrainFeature class itself only adds modData, same as every other terrain feature here).
/// No real placed Flooring exists in any of this project's sample saves to verify field
/// order/shape against directly (unlike Tree/HoeDirt/Bush) - this is derived from the decompiled
/// source's field declaration order, same lower-confidence tier as TreeEditor.TreeType's species
/// mapping, not a save-confirmed shape. whichFloor is the string key into Data/FloorsAndPaths.json
/// (e.g. "6" for Wood Path) - not the placed item's own id.
/// </summary>
public sealed class FlooringEditor
{
    private readonly XElement _feature;

    public FlooringEditor(TilePosition position, XElement featureElement)
    {
        Position = position;
        _feature = featureElement;
    }

    public TilePosition Position { get; private set; }

    /// <summary>See TreeEditor.Move - same terrainFeatures dictionary, same key-is-the-position shape.</summary>
    public void Move(TilePosition newPosition)
    {
        if (Item.Element("key")?.Element("Vector2") is { } vector)
        {
            vector.SetChildInt("X", newPosition.X);
            vector.SetChildInt("Y", newPosition.Y);
        }

        Position = newPosition;
    }

    /// <summary>Data/FloorsAndPaths.json key (e.g. "0" = Wood Floor, "6" = Wood Path) - which
    /// texture/connect-type/corner this tile draws as, and which neighbors it joins with (only
    /// same-WhichFloor neighbors connect - confirmed via Flooring.gatherNeighbors()'s
    /// `flooring.whichFloor == whichFloor` check).</summary>
    public string WhichFloor
    {
        get => _feature.GetChildText("whichFloor");
        set => _feature.SetChildText("whichFloor", value);
    }

    /// <summary>Only meaningful for ConnectType.Random floor types (e.g. Stepping Stone Path) -
    /// a fixed 0-15 index into the 16 non-connecting sprite variants, randomized once at
    /// placement (Flooring.ApplyFlooringFlags). Ignored by every other ConnectType, which instead
    /// derive their sprite from the live neighbor bitmask on every render.</summary>
    public int WhichView
    {
        get => _feature.TryGetChildInt("whichView") ?? 0;
        set => _feature.SetChildIntCreateIfMissing("whichView", value);
    }

    internal XElement Item => _feature.Parent!.Parent!;
}
