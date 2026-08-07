using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// A bush (berry bush, tea bush, walnut bush, decorative town bush, ...). Confirmed against 5
/// real examples in an actual save: lives in its own &lt;largeTerrainFeatures&gt; flat list (not
/// the &lt;terrainFeatures&gt; tile dictionary Tree/Grass/HoeDirt use), wrapped as
/// &lt;LargeTerrainFeature xsi:type="Bush"&gt;, with its own &lt;tilePosition&gt; child instead of
/// a wrapping &lt;key&gt;. Field order in real saves: tilePosition, size, datePlanted,
/// tileSheetOffset, health, flipped, townBush, greenhouseBush, drawShadow - sourceRect/inPot/
/// uniqueSpawnMutex (all present in the decompiled Bush.cs's AddField chain) are NOT present in
/// any real example checked, so they're deliberately not written here either (same self-healing
/// pattern already confirmed for Object/Building - the game re-derives them via setUpSourceRect()
/// on load, not from saved XML). health and greenhouseBush are plain public fields on Bush (not
/// NetFields), so they don't appear in the decompiled .AddField() list at all, yet are still
/// present in real save XML - the decompiled reference here is a slightly different game version
/// than the one that wrote the save; real save data wins over an .AddField() listing either way.
/// </summary>
public sealed class BushEditor
{
    private readonly XElement _element;

    public BushEditor(XElement element)
    {
        _element = element;
        var tile = element.Element("tilePosition")!;
        Position = new TilePosition(tile.GetChildInt("X"), tile.GetChildInt("Y"));
    }

    public TilePosition Position { get; private set; }

    /// <summary>Unlike Tree/Grass/HoeDirt, a bush's position lives on its own &lt;tilePosition&gt;
    /// child (flat list, not a tile dictionary) - moving it is a direct field edit.</summary>
    public void Move(TilePosition newPosition)
    {
        if (_element.Element("tilePosition") is { } tile)
        {
            tile.SetChildInt("X", newPosition.X);
            tile.SetChildInt("Y", newPosition.Y);
        }

        Position = newPosition;
    }

    /// <summary>0=small, 1=medium, 2=large, 3=tea bush, 4=walnut bush - confirmed against the
    /// decompiled Bush.cs constants (smallBush=0, mediumBush=1, largeBush=2, greenTeaBush=3,
    /// walnutBush=4).</summary>
    public int Size
    {
        get => _element.GetChildInt("size");
        set => _element.SetChildInt("size", value);
    }

    /// <summary>In-game day (Game1.stats.DaysPlayed) this bush was planted - used with the
    /// current day to derive age for the tea-bush growth sprite (Bush.getAge()).</summary>
    public int DatePlanted
    {
        get => _element.GetChildInt("datePlanted");
        set => _element.SetChildInt("datePlanted", value);
    }

    /// <summary>Bloom/harvest-ready state (e.g. a berry/tea bush with fruit to pick) - drives an
    /// extra sprite-sheet column offset in Bush.setUpSourceRect().</summary>
    public int TileSheetOffset
    {
        get => _element.GetChildInt("tileSheetOffset");
        set => _element.SetChildInt("tileSheetOffset", value);
    }

    /// <summary>Plain C# field on Bush, not a NetField (see class remarks) - genuinely absent
    /// on some real bushes (confirmed via a real crash: a bush on an actively-played farm had
    /// no &lt;health&gt; child at all), unlike the NetField-backed properties below. 0 is a
    /// neutral fallback, not a confirmed real default - the game re-derives a real value at
    /// runtime for bushes that were never explicitly damaged.</summary>
    public int Health
    {
        get => _element.TryGetChildInt("health") ?? 0;
        set => _element.SetChildIntCreateIfMissing("health", value);
    }

    public bool Flipped
    {
        get => _element.GetChildBool("flipped");
        set => _element.SetChildBool("flipped", value);
    }

    /// <summary>A decorative bush placed by the town map rather than the player - changes sprite
    /// selection for size 1/2 (Bush.setUpSourceRect()) and disables the size-2 vertical raise
    /// during draw.</summary>
    public bool TownBush
    {
        get => _element.GetChildBool("townBush");
        set => _element.SetChildBool("townBush", value);
    }

    /// <summary>Whether this bush is sheltered (greenhouse/indoor pot) - forces spring-season
    /// sprite selection year-round (Bush.IsSheltered()). Plain C# field on Bush, not a NetField
    /// (see class remarks) - genuinely absent on ordinary outdoor bushes (confirmed via a real
    /// crash rendering the Map tab on an actively-played farm: an outdoor bush had no
    /// &lt;greenhouseBush&gt; child, only ever present on the "5 real examples" this class was
    /// originally verified against, which all happened to be sheltered ones). Absent means not
    /// sheltered, i.e. false.</summary>
    public bool GreenhouseBush
    {
        get => _element.TryGetChildBool("greenhouseBush") ?? false;
        set => _element.SetChildBoolCreateIfMissing("greenhouseBush", value);
    }

    public bool DrawShadow
    {
        get => _element.GetChildBool("drawShadow");
        set => _element.SetChildBool("drawShadow", value);
    }

    internal XElement Element => _element;
}
