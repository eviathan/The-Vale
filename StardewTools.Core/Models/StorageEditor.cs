using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// One chest (or chest-like container - stone chest, mini-fridge, junimo hut output, etc,
/// which all share the Chest class) found somewhere in the save. Wraps whatever element it
/// was found on (see <see cref="StorageEditor"/> remarks on why the element name varies),
/// so its own Name/Stack/Quality are still available via <see cref="Item"/>.
/// </summary>
public sealed class ChestEditor
{
    private readonly XElement _element;

    public ChestEditor(XElement chestElement)
    {
        _element = chestElement;
        Item = new ItemEditor(chestElement);

        // Unverified: no populated chest existed in the save file we grounded this schema
        // against, so the contents container's exact name is a best guess. Try both casings
        // and fall back to "no contents visible" rather than throwing, so a chest shaped
        // differently than expected doesn't break the whole storage listing.
        var container = chestElement.Element("items") ?? chestElement.Element("Items");
        Items = container is null ? null : new ItemListEditor(container);
    }

    public ItemEditor Item { get; }

    /// <summary>Null if this chest's contents container couldn't be located - see remarks on the class.</summary>
    public ItemListEditor? Items { get; }

    /// <summary>The color picked via right-click on a real placed Chest/Stone Chest (real field,
    /// confirmed shape/default - see ChestXmlBuilder, which already writes this on every chest
    /// this tool places: black = "no custom color", the game's own sentinel for "draw the plain
    /// unpainted lid" per decompiled Chest.draw()). Only meaningful for ParentSheetIndex 130
    /// (Chest) or 232 (Stone Chest) in the real game, but harmless to read/write on any chest
    /// variant - the disguised ones (Mini-Fridge, Mini-Shipping Bin, Hopper, Junimo Chest) simply
    /// never check it when drawing.</summary>
    public (byte R, byte G, byte B, byte A) PlayerChoiceColor
    {
        get => _element.TryGetChildColor("playerChoiceColor") ?? (0, 0, 0, 255);
        set => _element.SetChildColorCreateIfMissing("playerChoiceColor", value.R, value.G, value.B, value.A);
    }

    /// <summary>The chest's display name - a real, base-Object field (every placed Object has one,
    /// confirmed present on the real Chest example this schema was grounded against: "Chest").
    /// Not a tool-only convenience: the real game lets a player rename a chest via its own menu's
    /// rename field, which writes straight to this same element - so setting it here is exactly
    /// as "real" as renaming one in-game, just easier to do for a chest buried in a shed you'd
    /// otherwise have to walk to.</summary>
    public string Name
    {
        get => _element.GetChildText("name");
        set => _element.SetChildText("name", value);
    }

    /// <summary>Opaque identity for matching this chest against another ChestEditor instance that
    /// might wrap the very same underlying save element - e.g. one found by scanning the whole
    /// save (StorageEditor.Chests) vs. one derived from a specific placed Object on the Map tab
    /// (PlacedObjectEditor.AsChest()). Two ChestEditor instances are "the same chest" iff this
    /// matches by reference.</summary>
    public object Identity => _element;
}

/// <summary>
/// Finds every chest anywhere in the save - the player's house, sheds, the farm, anywhere -
/// by scanning for any element with xsi:type="Chest", at any depth.
///
/// The wrapping element's own name is NOT "Item" everywhere: items carried in an inventory
/// serialize as &lt;Item xsi:type="..."&gt;, but the same Object-derived classes placed in
/// the world (on a farm, in a house) serialize as &lt;Object xsi:type="..."&gt; instead -
/// confirmed against a real save, which has plain &lt;Object&gt; (no xsi:type) for a
/// bog-standard Object like Weeds, but &lt;Object xsi:type="Cask"&gt; for an Object subclass.
/// A placed Chest is a subclass the same way, so it's &lt;Object xsi:type="Chest"&gt;, not
/// &lt;Item xsi:type="Chest"&gt; - matching only on the "Item" element name (an earlier
/// version of this code did) would silently find zero chests. Matching on the xsi:type
/// attribute alone, regardless of element name, avoids depending on that convention at all.
/// </summary>
public sealed class StorageEditor
{
    private static readonly XName XsiType = XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance");

    private readonly XElement _root;

    public StorageEditor(XElement saveGameRoot)
    {
        _root = saveGameRoot;
    }

    public IReadOnlyList<ChestEditor> Chests
        => _root.Descendants()
            .Where(e => (string?)e.Attribute(XsiType) == "Chest")
            .Select(e => new ChestEditor(e))
            .ToList();
}
