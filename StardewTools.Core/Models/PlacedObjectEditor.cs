using System.Xml.Linq;

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

    public TilePosition Position { get; }
    public ItemEditor Item { get; }

    internal XElement WrappingItem => Item.Element.Parent!.Parent!;
}
