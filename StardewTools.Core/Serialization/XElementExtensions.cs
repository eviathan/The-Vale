using System.Globalization;
using System.Xml.Linq;

namespace StardewTools.Core.Serialization;

internal static class XElementExtensions
{
    public static string GetChildText(this XElement parent, string childName)
        => parent.Element(childName)?.Value
           ?? throw new InvalidDataException($"<{parent.Name.LocalName}> has no <{childName}> child.");

    public static void SetChildText(this XElement parent, string childName, string value)
    {
        var child = parent.Element(childName)
            ?? throw new InvalidDataException($"<{parent.Name.LocalName}> has no <{childName}> child.");
        child.Value = value;
    }

    public static int GetChildInt(this XElement parent, string childName)
        => int.Parse(parent.GetChildText(childName), CultureInfo.InvariantCulture);

    public static void SetChildInt(this XElement parent, string childName, int value)
        => parent.SetChildText(childName, value.ToString(CultureInfo.InvariantCulture));

    public static double GetChildDouble(this XElement parent, string childName)
        => double.Parse(parent.GetChildText(childName), CultureInfo.InvariantCulture);

    public static void SetChildDouble(this XElement parent, string childName, double value)
        => parent.SetChildText(childName, value.ToString(CultureInfo.InvariantCulture));

    public static bool GetChildBool(this XElement parent, string childName)
        => bool.Parse(parent.GetChildText(childName));

    public static void SetChildBool(this XElement parent, string childName, bool value)
        => parent.SetChildText(childName, value ? "true" : "false");

    /// <summary>
    /// Stardew's save XML frequently duplicates a field under two names (e.g. a lowercase
    /// backing field and a PascalCase compatibility property - "stack"/"Stack",
    /// "itemsShipped"/"ItemsShipped"). Reads the first present, writes to every present
    /// variant so duplicates never drift out of sync with each other.
    /// </summary>
    public static int GetChildIntAny(this XElement parent, params string[] candidateNames)
    {
        foreach (var name in candidateNames)
        {
            var child = parent.Element(name);
            if (child is not null)
                return int.Parse(child.Value, CultureInfo.InvariantCulture);
        }

        throw new InvalidDataException(
            $"<{parent.Name.LocalName}> has none of: {string.Join(", ", candidateNames)}.");
    }

    public static void SetChildIntAny(this XElement parent, int value, params string[] candidateNames)
    {
        var found = false;
        foreach (var name in candidateNames)
        {
            var child = parent.Element(name);
            if (child is null)
                continue;

            child.Value = value.ToString(CultureInfo.InvariantCulture);
            found = true;
        }

        if (!found)
            throw new InvalidDataException(
                $"<{parent.Name.LocalName}> has none of: {string.Join(", ", candidateNames)}.");
    }

    public static int? TryGetChildInt(this XElement parent, string childName)
    {
        var child = parent.Element(childName);
        return child is null ? null : int.Parse(child.Value, CultureInfo.InvariantCulture);
    }

    public static bool? TryGetChildBool(this XElement parent, string childName)
    {
        var child = parent.Element(childName);
        return child is null ? null : bool.Parse(child.Value);
    }

    /// <summary>
    /// For newer/optional fields that a real save element might genuinely not have yet (e.g. a
    /// 1.6 field on an element shape last confirmed pre-1.6) - creates the child if missing
    /// instead of throwing, so editing a field that happens to be absent doesn't corrupt the
    /// element or blow up. Prefer the throwing SetChildBool/SetChildText for fields already
    /// confirmed present in every real save example.
    /// </summary>
    public static void SetChildBoolCreateIfMissing(this XElement parent, string childName, bool value)
    {
        var child = parent.Element(childName);
        if (child is null)
        {
            child = new XElement(childName);
            parent.Add(child);
        }

        child.Value = value ? "true" : "false";
    }

    /// <summary>See SetChildBoolCreateIfMissing remarks - same rationale, for int fields.</summary>
    public static void SetChildIntCreateIfMissing(this XElement parent, string childName, int value)
    {
        var child = parent.Element(childName);
        if (child is null)
        {
            child = new XElement(childName);
            parent.Add(child);
        }

        child.Value = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Real Color field shape (StardewValley.NetColor - Chest.playerChoiceColor, ColoredObject.
    /// color, ...) - confirmed via a real placed Chest's own &lt;playerChoiceColor&gt; (see
    /// ChestXmlBuilder): child elements B/G/R/A (that exact order, not RGBA) plus a PackedValue
    /// int mirroring them (XNA's Color.PackedValue = R | G&lt;&lt;8 | B&lt;&lt;16 | A&lt;&lt;24) -
    /// kept in sync on write even though nothing observed here proves the game re-reads
    /// PackedValue rather than recomputing it, since matching the real writer's shape exactly
    /// costs nothing and avoids being the one save tool that writes a stale value.
    /// </summary>
    public static (byte R, byte G, byte B, byte A)? TryGetChildColor(this XElement parent, string childName)
    {
        var child = parent.Element(childName);
        if (child is null)
            return null;

        byte Get(string name) => (byte)(child.Element(name)?.Value is { } v ? int.Parse(v, CultureInfo.InvariantCulture) : 0);
        return (Get("R"), Get("G"), Get("B"), Get("A"));
    }

    public static void SetChildColorCreateIfMissing(this XElement parent, string childName, byte r, byte g, byte b, byte a)
    {
        var child = parent.Element(childName);
        if (child is null)
        {
            child = new XElement(childName);
            parent.Add(child);
        }
        else
        {
            child.RemoveNodes();
        }

        var packedValue = (uint)r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
        child.Add(
            new XElement("B", b),
            new XElement("G", g),
            new XElement("R", r),
            new XElement("A", a),
            new XElement("PackedValue", packedValue));
    }
}
