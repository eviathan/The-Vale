using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// A placed piece of furniture (chair, table, rug, painting, decor, ...) - confirmed against a
/// real save's own starting Bed (the only Furniture example available - a BedFurniture, whose
/// base-class fields below all matched decompiled Furniture.cs field-for-field; bedType is
/// BedFurniture-only and not exposed here, since this editor only targets the generic, non-Bed/
/// non-FishTank subset - see PlaceableFurniture remarks for why those two are scoped out).
/// Lives in its own &lt;furniture&gt; flat list (like Bush/ResourceClump/Building, not the
/// &lt;objects&gt; tile dictionary), with its own &lt;tileLocation&gt; child.
/// </summary>
public sealed class FurnitureEditor
{
    private readonly XElement _element;

    public FurnitureEditor(XElement element)
    {
        _element = element;
        var tile = element.Element("tileLocation")!;
        Position = new TilePosition(tile.GetChildInt("X"), tile.GetChildInt("Y"));
    }

    public TilePosition Position { get; private set; }

    public string FurnitureId => _element.Element("itemId")?.Value ?? "";

    /// <summary>0=chair,1=bench,2=couch,3=armchair,4=dresser,5=long table,6=painting,7=lamp,
    /// 8=decor,9=other,10=bookcase,11=table,12=rug,13=window,14=fireplace,15=bed,16=torch,
    /// 17=sconce - decompiled Furniture.getTypeNumberFromName's own table.</summary>
    public int FurnitureType
    {
        get => _element.GetChildInt("furniture_type");
        set => _element.SetChildInt("furniture_type", value);
    }

    public int CurrentRotation
    {
        get => _element.GetChildInt("currentRotation");
        set => _element.SetChildInt("currentRotation", value);
    }

    /// <summary>The texture region this piece draws (raw pixel units in TileSheets/furniture -
    /// or another sheet for a real texture-override item, not modeled by PlaceableFurniture yet).
    /// Written once at placement (matching decompiled InitializeAtTile/RecalculateBoundingBox,
    /// which compute and persist these rather than re-deriving them every draw).</summary>
    public (int X, int Y, int Width, int Height) SourceRect => ReadRect("sourceRect");

    /// <summary>The placed footprint in pixel units - always tile-aligned at Position (decompiled
    /// RecalculateBoundingBox always anchors the box origin at tileLocation*64 regardless of
    /// type), but its Width/Height can exceed one tile (e.g. a 2x2 table).</summary>
    public (int X, int Y, int Width, int Height) BoundingBox => ReadRect("boundingBox");

    /// <summary>tileLocation/boundingBox/defaultBoundingBox all shift together (decompiled
    /// Object.TileLocation's setter cascades into RecalculateBoundingBox() the same way).
    /// drawPosition is deliberately NOT touched here even though decompiled Furniture.cs marks it
    /// [XmlElement("drawPosition")] - confirmed against a real save (the starting Bed) that it's
    /// never actually written: the field itself is `protected`, and XmlSerializer only serializes
    /// public members, so the attribute is dead on a non-public field. The game recomputes it from
    /// boundingBox via updateDrawPosition() on load, so this tool doesn't need to either.</summary>
    public void Move(TilePosition newPosition)
    {
        var dx = newPosition.X - Position.X;
        var dy = newPosition.Y - Position.Y;

        if (_element.Element("tileLocation") is { } tile)
        {
            tile.SetChildInt("X", newPosition.X);
            tile.SetChildInt("Y", newPosition.Y);
        }
        ShiftRect("boundingBox", dx * 64, dy * 64);
        ShiftRect("defaultBoundingBox", dx * 64, dy * 64);

        Position = newPosition;
    }

    private (int X, int Y, int Width, int Height) ReadRect(string childName)
    {
        var rect = _element.Element(childName);
        if (rect is null)
            return (0, 0, 0, 0);
        return (rect.GetChildInt("X"), rect.GetChildInt("Y"), rect.GetChildInt("Width"), rect.GetChildInt("Height"));
    }

    private void ShiftRect(string childName, int dx, int dy)
    {
        if (_element.Element(childName) is not { } rect)
            return;

        rect.SetChildInt("X", rect.GetChildInt("X") + dx);
        rect.SetChildInt("Y", rect.GetChildInt("Y") + dy);
        if (rect.Element("Location") is { } location)
        {
            location.SetChildInt("X", location.GetChildInt("X") + dx);
            location.SetChildInt("Y", location.GetChildInt("Y") + dy);
        }
    }

    internal XElement Element => _element;
}
