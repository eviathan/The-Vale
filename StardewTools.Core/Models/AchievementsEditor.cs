using System.Xml.Linq;

namespace StardewTools.Core.Models;

/// <summary>
/// Typed access over &lt;player&gt;/&lt;achievements&gt;, a plain list of achievement IDs
/// (e.g. &lt;achievements&gt;&lt;int&gt;3&lt;/int&gt;...&lt;/achievements&gt;). We don't ship
/// a verified ID-to-name table - the achievement list wasn't populated in the save file we
/// grounded this schema against - so IDs are exposed raw; a name lookup can be layered on
/// top once verified against a save with achievements actually unlocked.
/// </summary>
public sealed class AchievementsEditor
{
    private readonly XElement _element;

    public AchievementsEditor(XElement achievementsElement)
    {
        _element = achievementsElement;
    }

    public IReadOnlyList<int> Ids => _element.Elements("int").Select(e => (int)e).ToList();

    public bool Contains(int id) => Ids.Contains(id);

    public void Add(int id)
    {
        if (Contains(id))
            return;

        _element.Add(new XElement("int", id));
    }

    public void Remove(int id)
    {
        _element.Elements("int").FirstOrDefault(e => (int)e == id)?.Remove();
    }
}
