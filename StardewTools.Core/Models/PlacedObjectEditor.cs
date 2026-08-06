using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// An object placed in the world (crafted machine, forageable, chest, weeds, ...). Reuses
/// <see cref="ItemEditor"/> for the item-shaped fields (Name/Stack/Quality), since a placed
/// Object and a carried Item share the same underlying class hierarchy - only the wrapping
/// element name differs (see StorageEditor remarks).
/// </summary>
public sealed class PlacedObjectEditor
{
    public PlacedObjectEditor(TilePosition position, XElement objectElement)
    {
        Position = position;
        Item = new ItemEditor(objectElement);
    }

    public TilePosition Position { get; private set; }
    public ItemEditor Item { get; }

    internal XElement WrappingItem => Item.Element.Parent!.Parent!;

    /// <summary>
    /// Unlike Tree/Grass/HoeDirt, a placed Object tracks its own tile position twice - once as
    /// the objects-dictionary key (what the game uses to look it up) and again on the Object
    /// itself (tileLocation, and boundingBox.Location - confirmed via ObjectXmlBuilder, which
    /// sets both from the same tile when placing a new one). Both are kept in sync here; only
    /// updating the dictionary key would leave the Object reporting its old position to any
    /// game logic that reads tileLocation/boundingBox directly instead of going through the
    /// dictionary.
    /// </summary>
    public void Move(TilePosition newPosition)
    {
        if (WrappingItem.Element("key")?.Element("Vector2") is { } vector)
        {
            vector.SetChildInt("X", newPosition.X);
            vector.SetChildInt("Y", newPosition.Y);
        }

        var element = Item.Element;
        if (element.Element("tileLocation") is { } tileLocation)
        {
            tileLocation.SetChildInt("X", newPosition.X);
            tileLocation.SetChildInt("Y", newPosition.Y);
        }

        if (element.Element("boundingBox") is { } boundingBox)
        {
            var boundsX = newPosition.X * 64;
            var boundsY = newPosition.Y * 64;
            boundingBox.SetChildInt("X", boundsX);
            boundingBox.SetChildInt("Y", boundsY);

            if (boundingBox.Element("Location") is { } location)
            {
                location.SetChildInt("X", boundsX);
                location.SetChildInt("Y", boundsY);
            }
        }

        Position = newPosition;
    }
}
