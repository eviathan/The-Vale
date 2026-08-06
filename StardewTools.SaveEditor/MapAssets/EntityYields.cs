using System.Collections.Generic;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.ViewModels;

namespace StardewTools.SaveEditor.MapAssets;

/// <summary>
/// What "Collect" deposits into the player's inventory before removing an entity - real
/// item indices/quantities pulled from the decompiled source (TerrainFeatures/ResourceClump.cs
/// performToolAction, TerrainFeatures/Tree.cs), not guessed. Quantities for ResourceClump and
/// Crop are exact; Tree quantity is an approximation (the real chop-down RNG range wasn't
/// pinned down) - the item *identity* is what's verified there, not the count.
///
/// PlacedObject resolves to itself (its own ParentSheetIndex/Stack) rather than special-cased
/// via ItemListEditor.AddCopy as originally planned - AddCopy clones the source element
/// verbatim, but a placed Object's element is named &lt;Object&gt; while the inventory list
/// only recognizes &lt;Item&gt; children (PlacedObjectEditor remarks), so the clone would
/// silently become invisible to Items. Confirmed with a real isolated AddCopy call: the
/// inventory count didn't change. Going through the same AddNew path as every other kind
/// here sidesteps that entirely.
/// </summary>
public static class EntityYields
{
    /// <summary>ParentSheetIndex -> (yielded index, stack) pairs. Verified against
    /// ResourceClump.performToolAction: 600/602 are stumps/logs (Hardwood), 622 is a
    /// meteorite (Copper Ore + Stone + Iridium Ore), 672/752/754/756/758 are boulder
    /// variants (Stone).</summary>
    private static readonly IReadOnlyDictionary<int, (int Index, int Stack)[]> ClumpYields = new Dictionary<int, (int, int)[]>
    {
        [600] = new[] { (709, 2) },
        [602] = new[] { (709, 8) },
        [622] = new[] { (386, 6), (390, 6), (535, 2) },
        [672] = new[] { (390, 15) },
        [752] = new[] { (390, 10) },
        [754] = new[] { (390, 10) },
        [756] = new[] { (390, 10) },
        [758] = new[] { (390, 10) },
    };

    public static IReadOnlyList<(int Index, int Stack)> Resolve(MapEntitySummary entity) => entity.Source switch
    {
        ResourceClumpEditor clump when ClumpYields.TryGetValue(clump.ParentSheetIndex, out var yield) => yield,

        // Wood (388) by default, Hardwood (709) for Mahogany (treeType 8) - confirmed in
        // Tree.cs. A stump has already been chopped - nothing left to collect from it.
        TreeEditor { Stump: false } tree => new[] { (tree.TreeType == 8 ? 709 : 388, 1) },

        // CropEditor.IndexOfHarvest/MinHarvest are real save fields, not a guess.
        HoeDirtEditor { Crop: { } crop } => new[] { (crop.IndexOfHarvest, crop.MinHarvest) },

        PlacedObjectEditor { Item.ParentSheetIndex: int index } placed => new[] { (index, placed.Item.Stack) },

        _ => System.Array.Empty<(int, int)>(),
    };
}
