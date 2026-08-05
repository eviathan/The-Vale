using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>A grass patch on the map. Confirmed against a real save's terrainFeatures dictionary.</summary>
public sealed class GrassEditor
{
    private readonly XElement _feature;

    public GrassEditor(TilePosition position, XElement featureElement)
    {
        Position = position;
        _feature = featureElement;
    }

    public TilePosition Position { get; }

    public int GrassType
    {
        get => _feature.GetChildInt("grassType");
        set => _feature.SetChildInt("grassType", value);
    }

    public int NumberOfWeeds
    {
        get => _feature.GetChildInt("numberOfWeeds");
        set => _feature.SetChildInt("numberOfWeeds", value);
    }

    internal XElement Item => _feature.Parent!.Parent!;
}
