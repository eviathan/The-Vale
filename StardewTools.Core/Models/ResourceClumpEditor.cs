using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// A resource clump - large stumps, boulders, logs scattered on a fresh farm. Confirmed
/// against a real save; unlike trees/grass/objects this is a flat list, not a tile
/// dictionary, so position comes from its own &lt;tile&gt; child rather than a wrapping key.
/// </summary>
public sealed class ResourceClumpEditor
{
    private readonly XElement _element;

    public ResourceClumpEditor(XElement element)
    {
        _element = element;
        var tile = element.Element("tile")!;
        Position = new TilePosition(tile.GetChildInt("X"), tile.GetChildInt("Y"));
    }

    public TilePosition Position { get; private set; }

    /// <summary>Unlike Tree/Grass/HoeDirt, a clump's position lives on its own &lt;tile&gt;
    /// child (flat list, not a tile dictionary) - moving it is a direct field edit.</summary>
    public void Move(TilePosition newPosition)
    {
        if (_element.Element("tile") is { } tile)
        {
            tile.SetChildInt("X", newPosition.X);
            tile.SetChildInt("Y", newPosition.Y);
        }

        Position = newPosition;
    }

    /// <summary>Sprite index identifying stump/boulder/log variant.</summary>
    public int ParentSheetIndex
    {
        get => _element.GetChildInt("parentSheetIndex");
        set => _element.SetChildInt("parentSheetIndex", value);
    }

    public int Width => _element.GetChildInt("width");
    public int Height => _element.GetChildInt("height");

    public int Health
    {
        get => _element.GetChildInt("health");
        set => _element.SetChildInt("health", value);
    }

    internal XElement Element => _element;
}
