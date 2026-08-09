using System.Linq;
using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// A planted crop - the optional &lt;crop&gt; child of a HoeDirt terrain feature. Confirmed
/// against a real save's terrainFeatures dictionary. Fields that define which crop this
/// fundamentally is (seed/harvest identity, sprite row) are left read-only, same caution
/// <see cref="ItemEditor"/> already applies to ParentSheetIndex - editing them without
/// cross-referencing Data/Crops.json risks a crop the game can't render or harvest sanely.
/// Day-to-day growth state is fully editable.
/// </summary>
public sealed class CropEditor
{
    private readonly XElement _crop;

    internal CropEditor(XElement cropElement)
    {
        _crop = cropElement;
    }

    /// <summary>Index into the crop's phaseDays list - 0 is freshly planted.</summary>
    public int CurrentPhase
    {
        get => _crop.GetChildInt("currentPhase");
        set => _crop.SetChildInt("currentPhase", value);
    }

    public int DayOfCurrentPhase
    {
        get => _crop.GetChildInt("dayOfCurrentPhase");
        set => _crop.SetChildInt("dayOfCurrentPhase", value);
    }

    public bool Dead
    {
        get => _crop.GetChildBool("dead");
        set => _crop.SetChildBool("dead", value);
    }

    public bool FullGrown
    {
        get => _crop.GetChildBool("fullGrown");
        set => _crop.SetChildBool("fullGrown", value);
    }

    public bool Flip
    {
        get => _crop.GetChildBool("flip");
        set => _crop.SetChildBool("flip", value);
    }

    /// <summary>-1 = doesn't regrow after harvest; otherwise days until the next harvest.</summary>
    public int RegrowAfterHarvest
    {
        get => _crop.GetChildInt("regrowAfterHarvest");
        set => _crop.SetChildInt("regrowAfterHarvest", value);
    }

    public int MinHarvest
    {
        get => _crop.GetChildInt("minHarvest");
        set => _crop.SetChildInt("minHarvest", value);
    }

    public int MaxHarvest
    {
        get => _crop.GetChildInt("maxHarvest");
        set => _crop.SetChildInt("maxHarvest", value);
    }

    public double ChanceForExtraCrops
    {
        get => _crop.GetChildDouble("chanceForExtraCrops");
        set => _crop.SetChildDouble("chanceForExtraCrops", value);
    }

    /// <summary>Item id this crop's seed came from - defines crop identity, read-only.</summary>
    public int SeedIndex => _crop.GetChildInt("seedIndex");

    /// <summary>Item id yielded on harvest - defines crop identity, read-only.</summary>
    public int IndexOfHarvest => _crop.GetChildInt("indexOfHarvest");

    /// <summary>Row in the crops sprite sheet - defines crop identity, read-only.</summary>
    public int RowInSpriteSheet => _crop.GetChildInt("rowInSpriteSheet");

    /// <summary>Real growth-phase count, excluding the trailing 99999 "stays forever" sentinel
    /// HoeDirtEditor.PlantCrop always appends (i.e. Data/Crops.json's own DaysInPhase.Length) -
    /// CurrentPhase == PhaseCount is "fully grown" for rendering purposes (confirmed against
    /// decompiled Crop.growCompletely()/draw() - both compare against phaseDays.Count - 1 using
    /// the RUNTIME phaseDays list, which already includes the sentinel this property excludes,
    /// so the two counts land on the same value here).</summary>
    public int PhaseCount => (_crop.Element("phaseDays")?.Elements("int").Count() ?? 1) - 1;

    /// <summary>Whether TintColor should be drawn as a colored overlay on top of the base sprite
    /// - real flowers (Tulip, Poppy, Blue Jazz, Summer Spangle, Fairy Rose) set this true with a
    /// randomly-chosen color from Data/Crops.json's TintColors list at plant time (decompiled
    /// Crop.ResetSeasonsToGrowIn/constructor); every other crop leaves it false. Only meaningful
    /// once CurrentPhase == PhaseCount and the crop isn't dead (decompiled Crop.draw()).</summary>
    public bool ProgramColored => _crop.GetChildBool("programColored");

    /// <summary>Real Color field shape - see XElementExtensions.TryGetChildColor remarks.</summary>
    public (byte R, byte G, byte B, byte A)? TintColor => _crop.TryGetChildColor("tintColor");
}
