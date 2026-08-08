using System.Xml.Linq;

namespace StardewTools.Core.Serialization;

/// <summary>
/// Loads and saves a Stardew Valley save file as raw XML.
///
/// Stardew's save schema is huge (thousands of fields across items, locations,
/// NPCs, buildings, etc.) and changes between game versions. Rather than modeling
/// the entire schema as POCOs and round-tripping through XmlSerializer - which
/// would silently drop any element our model doesn't know about - we keep the
/// full XDocument in memory and mutate only the specific elements an editor
/// screen cares about. Everything else passes through untouched.
/// </summary>
public sealed class SaveFile
{
    private readonly XDocument _document;

    private SaveFile(XDocument document)
    {
        _document = document;
        Root = document.Root ?? throw new InvalidDataException("Save file has no root element.");

        if (Root.Name.LocalName != "SaveGame")
            throw new InvalidDataException($"Expected a <SaveGame> root element, found <{Root.Name.LocalName}>.");
    }

    /// <summary>The root &lt;SaveGame&gt; element.</summary>
    public XElement Root { get; }

    /// <summary>The underlying document - exposed so undo/redo (StardewTools.SaveEditor.UndoManager)
    /// can subscribe to its real XObject.Changing/Changed events (confirmed via a standalone
    /// reflection test: subscribing at the document level bubbles change notifications from any
    /// descendant mutation, anywhere in the tree, not just direct children) without this class
    /// needing to know undo exists.</summary>
    public XDocument Document => _document;

    public static SaveFile Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static SaveFile Load(Stream stream)
    {
        var document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        return new SaveFile(document);
    }

    /// <summary>A snapshot of the current state, fully independent of this instance (deep-cloned) -
    /// used by undo/redo to capture a checkpoint before a mutation, without the cost/complexity of
    /// a real file round-trip.</summary>
    public SaveFile Clone() => new(new XDocument(_document));

    public void Save(string path)
    {
        using var stream = File.Create(path);
        Save(stream);
    }

    public void Save(Stream stream)
    {
        _document.Save(stream, SaveOptions.DisableFormatting);
    }
}
