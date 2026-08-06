using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// A tilled soil tile - confirmed against a real save's terrainFeatures dictionary. Planting
/// is optional: a HoeDirt tile can exist tilled-but-empty with no &lt;crop&gt; child at all.
/// </summary>
public sealed class HoeDirtEditor
{
    private readonly XElement _feature;

    public HoeDirtEditor(TilePosition position, XElement featureElement)
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

    /// <summary>0 = dry, 1 = watered, 2 = paddy crop (general modding knowledge - same
    /// confidence tier as TreeEditor.TreeType's species mapping, not save-verified per value).</summary>
    public int State
    {
        get => _feature.GetChildInt("state");
        set => _feature.SetChildInt("state", value);
    }

    public int Fertilizer
    {
        get => _feature.GetChildInt("fertilizer");
        set => _feature.SetChildInt("fertilizer", value);
    }

    public bool IsGreenhouseDirt
    {
        get => _feature.GetChildBool("isGreenhouseDirt");
        set => _feature.SetChildBool("isGreenhouseDirt", value);
    }

    /// <summary>Null when this tile is tilled but nothing's planted.</summary>
    public CropEditor? Crop => _feature.Element("crop") is { } crop ? new CropEditor(crop) : null;

    /// <summary>Un-plants without un-tilling - removes just the &lt;crop&gt; child.</summary>
    public void RemoveCrop() => _feature.Element("crop")?.Remove();

    internal XElement Item => _feature.Parent!.Parent!;
}
