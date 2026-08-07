using System.Collections.Generic;
using System.Linq;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>An (id, display name) pair for a dropdown backed by a raw int field.</summary>
public sealed record NamedValue(int Value, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// Display names for save fields that are really enums stored as raw ints. Kept in the
/// app layer rather than Core since Core deliberately sticks to what's verified against
/// real save data - these are display-only lookups, not part of the read/write schema.
/// </summary>
public static class GameEnums
{
    /// <summary>Confirmed against a real save's &lt;whichFarm&gt; field (0 = Standard).</summary>
    public static readonly IReadOnlyList<NamedValue> FarmTypes = new[]
    {
        new NamedValue(0, "Standard Farm"),
        new NamedValue(1, "Riverland Farm"),
        new NamedValue(2, "Forest Farm"),
        new NamedValue(3, "Hill-top Farm"),
        new NamedValue(4, "Wilderness Farm"),
        new NamedValue(5, "Four Corners Farm"),
        new NamedValue(6, "Beach Farm"),
        new NamedValue(7, "Meadowlands Farm"),
    };

    /// <summary>
    /// Not verified against real data beyond confirming the field is an int (the reference
    /// save only ever had a value of 1 = Rain) - the rest come from general Stardew modding
    /// knowledge, not something we've cross-checked here.
    /// </summary>
    public static readonly IReadOnlyList<NamedValue> WeatherTypes = new[]
    {
        new NamedValue(0, "Sunny"),
        new NamedValue(1, "Rain"),
        new NamedValue(2, "Debris"),
        new NamedValue(3, "Lightning"),
        new NamedValue(4, "Festival"),
        new NamedValue(5, "Snow"),
        new NamedValue(6, "Wedding"),
    };

    /// <summary>Item quality - stable and well-documented; note 3 is intentionally unused.</summary>
    public static readonly IReadOnlyList<NamedValue> ItemQualities = new[]
    {
        new NamedValue(0, "Normal"),
        new NamedValue(1, "Silver"),
        new NamedValue(2, "Gold"),
        new NamedValue(4, "Iridium"),
    };

    /// <summary>
    /// Real achievement names, extracted directly from the installed game's own
    /// Content/Data/Achievements.xnb (not a guess) - each entry there is
    /// "Name^Description^IsSecret^PrerequisiteId^SortOrder"; we only need the Name.
    /// IDs 8, 10, 14, 23 and 33 are genuinely absent from that file (not a bug here) -
    /// they don't get a name and fall back to "Achievement #N" in the UI.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, string> AchievementNames = new Dictionary<int, string>
    {
        [0] = "Greenhorn (15k)",
        [1] = "Cowpoke (50k)",
        [2] = "Homesteader (250k)",
        [3] = "Millionaire (1mil)",
        [4] = "Legend (10mil)",
        [5] = "A Complete Collection",
        [6] = "A New Friend",
        [7] = "Best Friends",
        [9] = "The Beloved Farmer",
        [11] = "Cliques",
        [12] = "Networking",
        [13] = "Popular",
        [15] = "Cook",
        [16] = "Sous Chef",
        [17] = "Gourmet Chef",
        [18] = "Moving Up",
        [19] = "Living Large",
        [20] = "D.I.Y.",
        [21] = "Artisan",
        [22] = "Craft Master",
        [24] = "Fisherman",
        [25] = "Ol' Mariner",
        [26] = "Master Angler",
        [27] = "Mother Catch",
        [28] = "Treasure Trove",
        [29] = "Gofer",
        [30] = "A Big Help",
        [31] = "Polyculture",
        [32] = "Monoculture",
        [34] = "Full Shipment",
    };

    /// <summary>
    /// Not verified against real save data beyond confirming treeType is an int (TreeEditor
    /// remarks) - only 1/2/3 are confirmed to have real sprite art (TreeSprites.KnownTypeFiles).
    /// Everything else here is general Stardew modding knowledge, not cross-checked.
    /// </summary>
    public static readonly IReadOnlyList<NamedValue> TreeTypes = new[]
    {
        new NamedValue(1, "Oak"),
        new NamedValue(2, "Maple"),
        new NamedValue(3, "Pine"),
        new NamedValue(6, "Desert palm"),
        new NamedValue(7, "Mushroom tree"),
        new NamedValue(8, "Mahogany"),
        new NamedValue(9, "Green rain (Oak)"),
        new NamedValue(10, "Green rain (Maple)"),
        new NamedValue(11, "Green rain (Pine)"),
        new NamedValue(13, "Mystic tree"),
    };

    /// <summary>Confirmed field (HoeDirtEditor.State is an int); the 0/1/2 meanings are
    /// general modding knowledge, not save-verified per value.</summary>
    public static readonly IReadOnlyList<NamedValue> HoeDirtStates = new[]
    {
        new NamedValue(0, "Dry"),
        new NamedValue(1, "Watered"),
        new NamedValue(2, "Paddy"),
    };

    /// <summary>Cross-references GrassSprites.SourceOffsetY's already sprite-verified mapping.</summary>
    public static readonly IReadOnlyList<NamedValue> GrassTypes = new[]
    {
        new NamedValue(1, "Normal (outdoor)"),
        new NamedValue(2, "Cave"),
        new NamedValue(3, "Frost"),
        new NamedValue(4, "Lava"),
        new NamedValue(5, "Cave 2"),
        new NamedValue(6, "Cobweb"),
    };

    /// <summary>Confirmed against Farmer.cs (maxItems, default 12) - vanilla only ever sets this
    /// via increaseBackpackSize (12 starting, +12 for the Backpack, +12 again for the Deluxe
    /// Backpack = 36 max). The field itself is a plain int, not a real enum, so this is a picker
    /// convenience, not an enforced limit - PlayerEditor.MaxItems accepts any value.</summary>
    public static readonly IReadOnlyList<NamedValue> BackpackSizes = new[]
    {
        new NamedValue(12, "Starting (12)"),
        new NamedValue(24, "Backpack (24)"),
        new NamedValue(36, "Deluxe Backpack (36)"),
    };

    /// <summary>Confirmed against the decompiled Bush.cs constants (smallBush=0, mediumBush=1,
    /// largeBush=2, greenTeaBush=3, walnutBush=4).</summary>
    public static readonly IReadOnlyList<NamedValue> BushSizes = new[]
    {
        new NamedValue(0, "Small"),
        new NamedValue(1, "Medium (berry)"),
        new NamedValue(2, "Large"),
        new NamedValue(3, "Tea"),
        new NamedValue(4, "Walnut"),
    };

    /// <summary>Confirmed against the decompiled FriendshipStatus enum (Friendship.cs) - the
    /// real values a &lt;Status&gt; element can hold.</summary>
    public static readonly IReadOnlyList<string> FriendshipStatuses = new[] { "Friendly", "Dating", "Engaged", "Married", "Divorced" };

    public static string AchievementLabel(int id)
        => AchievementNames.TryGetValue(id, out var name) ? $"{id} - {name}" : $"{id} - Achievement #{id}";

    public static NamedValue FindOrFirst(IReadOnlyList<NamedValue> options, int value)
        => options.FirstOrDefault(o => o.Value == value) ?? options[0];
}
