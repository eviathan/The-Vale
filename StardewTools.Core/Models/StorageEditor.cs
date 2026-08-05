using System.Xml.Linq;

namespace StardewTools.Core.Models;

/// <summary>
/// One chest (or chest-like container - stone chest, mini-fridge, junimo hut output, etc,
/// which all share the Chest class) found somewhere in the save. Wraps whatever element it
/// was found on (see <see cref="StorageEditor"/> remarks on why the element name varies),
/// so its own Name/Stack/Quality are still available via <see cref="Item"/>.
/// </summary>
public sealed class ChestEditor
{
    public ChestEditor(XElement chestElement)
    {
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
