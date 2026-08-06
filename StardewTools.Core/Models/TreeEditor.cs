using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>A tree on the map. Confirmed against a real save's terrainFeatures dictionary.</summary>
public sealed class TreeEditor
{
    private readonly XElement _feature;

    public TreeEditor(TilePosition position, XElement featureElement)
    {
        Position = position;
        _feature = featureElement;
    }

    public TilePosition Position { get; private set; }

    /// <summary>Trees live in the terrainFeatures tile dictionary keyed by position - moving
    /// one means rewriting its &lt;key&gt;&lt;Vector2&gt; rather than any field on the tree
    /// itself (there isn't one; the key IS the position).</summary>
    public void Move(TilePosition newPosition)
    {
        if (Item.Element("key")?.Element("Vector2") is { } vector)
        {
            vector.SetChildInt("X", newPosition.X);
            vector.SetChildInt("Y", newPosition.Y);
        }

        Position = newPosition;
    }

    /// <summary>0-5: seed/sprout/sapling/bush/(unnamed)/adult - confirmed against the decompiled
    /// Tree.cs constants (seedStage=0, sproutStage=1, saplingStage=2, bushStage=3, treeStage=5;
    /// stage 4 has no named constant but is a real intermediate step - growthStage increments
    /// through it same as any other) and a real save's adult tree (growthStage 5, not 4 as
    /// earlier assumed here).</summary>
    public int GrowthStage
    {
        get => _feature.GetChildInt("growthStage");
        set => _feature.SetChildInt("growthStage", value);
    }

    /// <summary>Species id (oak/maple/pine/etc - the exact int-to-species mapping isn't verified here).</summary>
    public int TreeType
    {
        get => _feature.GetChildInt("treeType");
        set => _feature.SetChildInt("treeType", value);
    }

    public int Health
    {
        get => _feature.GetChildInt("health");
        set => _feature.SetChildInt("health", value);
    }

    public bool Stump
    {
        get => _feature.GetChildBool("stump");
        set => _feature.SetChildBool("stump", value);
    }

    public bool Tapped
    {
        get => _feature.GetChildBool("tapped");
        set => _feature.SetChildBool("tapped", value);
    }

    public bool Flipped
    {
        get => _feature.GetChildBool("flipped");
        set => _feature.SetChildBool("flipped", value);
    }

    public bool HasSeed
    {
        get => _feature.GetChildBool("hasSeed");
        set => _feature.SetChildBool("hasSeed", value);
    }

    public bool Fertilized
    {
        get => _feature.GetChildBool("fertilized");
        set => _feature.SetChildBool("fertilized", value);
    }

    public bool ShakeLeft
    {
        get => _feature.GetChildBool("shakeLeft");
        set => _feature.SetChildBool("shakeLeft", value);
    }

    /// <summary>Whether this tree has accumulated moss - a real, separately-tracked 1.6 field
    /// (confirmed via Tree.cs's own NetField list: hasMoss) that changes which sprite-sheet
    /// column the standing canopy draws from (see FarmMapControl.TryDrawTreeSprite). Read/write
    /// defensively (create-if-missing) rather than the throwing GetChildBool/SetChildBool other
    /// tree fields use - a real save examined during this tool's development predates this
    /// field and simply doesn't have the element at all.</summary>
    public bool HasMoss
    {
        get => _feature.TryGetChildBool("hasMoss") ?? false;
        set => _feature.SetChildBoolCreateIfMissing("hasMoss", value);
    }

    /// <summary>Whether the tree's seed pods were already shaken loose today - confirmed via
    /// Tree.cs's own NetField list (wasShakenToday). Combined with the tree type's data-driven
    /// UseAlternateSpriteWhenNotShaken flag (not modeled here - would need a Data/trees.json
    /// lookup), this can select a third sprite-sheet column the renderer doesn't attempt yet.
    /// Same defensive create-if-missing read/write as HasMoss.</summary>
    public bool WasShakenToday
    {
        get => _feature.TryGetChildBool("wasShakenToday") ?? false;
        set => _feature.SetChildBoolCreateIfMissing("wasShakenToday", value);
    }

    internal XElement Item => _feature.Parent!.Parent!;
}
