using System.Xml.Linq;
using StardewTools.Core.Serialization;

namespace StardewTools.Core.Models;

/// <summary>
/// Typed access over &lt;player&gt;/&lt;stats&gt;. Exposes a curated set of the fields
/// people actually want to cheat/inspect; the underlying &lt;stats&gt; element has ~50
/// more (every material/resource ever gathered) reachable via <see cref="GetRaw"/> /
/// <see cref="SetRaw"/> by the lowercase field name.
/// </summary>
public sealed class StatsEditor
{
    private readonly XElement _element;

    public StatsEditor(XElement statsElement)
    {
        _element = statsElement;
    }

    public int DaysPlayed
    {
        get => _element.GetChildIntAny("daysPlayed", "DaysPlayed");
        set => _element.SetChildIntAny(value, "daysPlayed", "DaysPlayed");
    }

    public int StepsTaken
    {
        get => _element.GetChildIntAny("stepsTaken", "StepsTaken");
        set => _element.SetChildIntAny(value, "stepsTaken", "StepsTaken");
    }

    public int MonstersKilled
    {
        get => _element.GetChildIntAny("monstersKilled", "MonstersKilled");
        set => _element.SetChildIntAny(value, "monstersKilled", "MonstersKilled");
    }

    public int ItemsShipped
    {
        get => _element.GetChildIntAny("itemsShipped", "ItemsShipped");
        set => _element.SetChildIntAny(value, "itemsShipped", "ItemsShipped");
    }

    public int ItemsCrafted
    {
        get => _element.GetChildIntAny("itemsCrafted", "ItemsCrafted");
        set => _element.SetChildIntAny(value, "itemsCrafted", "ItemsCrafted");
    }

    public int ItemsCooked
    {
        get => _element.GetChildIntAny("itemsCooked", "ItemsCooked");
        set => _element.SetChildIntAny(value, "itemsCooked", "ItemsCooked");
    }

    public int QuestsCompleted
    {
        get => _element.GetChildIntAny("questsCompleted", "QuestsCompleted");
        set => _element.SetChildIntAny(value, "questsCompleted", "QuestsCompleted");
    }

    public int TimesFished
    {
        get => _element.GetChildIntAny("timesFished", "TimesFished");
        set => _element.SetChildIntAny(value, "timesFished", "TimesFished");
    }

    public int TrufflesFound
    {
        get => _element.GetChildIntAny("trufflesFound", "TrufflesFound");
        set => _element.SetChildIntAny(value, "trufflesFound", "TrufflesFound");
    }

    public int DiamondsFound
    {
        get => _element.GetChildIntAny("diamondsFound", "DiamondsFound");
        set => _element.SetChildIntAny(value, "diamondsFound", "DiamondsFound");
    }

    public int GoldFound
    {
        get => _element.GetChildIntAny("goldFound", "GoldFound");
        set => _element.SetChildIntAny(value, "goldFound", "GoldFound");
    }

    public int IridiumFound
    {
        get => _element.GetChildIntAny("iridiumFound", "IridiumFound");
        set => _element.SetChildIntAny(value, "iridiumFound", "IridiumFound");
    }

    public int TotalMoneyGifted
    {
        get => _element.GetChildIntAny("totalMoneyGifted", "TotalMoneyGifted");
        set => _element.SetChildIntAny(value, "totalMoneyGifted", "TotalMoneyGifted");
    }

    public int IndividualMoneyEarned
    {
        get => _element.GetChildIntAny("individualMoneyEarned", "IndividualMoneyEarned");
        set => _element.SetChildIntAny(value, "individualMoneyEarned", "IndividualMoneyEarned");
    }

    /// <summary>Read any stat by its lowercase field name (e.g. "goatCheeseMade", "stoneGathered").</summary>
    public int GetRaw(string lowercaseFieldName) => _element.GetChildIntAny(lowercaseFieldName);

    /// <summary>Write any stat by its lowercase field name. Only updates that one field - see class remarks.</summary>
    public void SetRaw(string lowercaseFieldName, int value) => _element.SetChildIntAny(value, lowercaseFieldName);

    /// <summary>Every lowercase field name available under &lt;stats&gt;, for building a full list UI.</summary>
    public IReadOnlyList<string> AllFieldNames
        => _element.Elements()
            .Select(e => e.Name.LocalName)
            .Where(n => n.Length > 0 && char.IsLower(n[0]))
            .ToList();
}
