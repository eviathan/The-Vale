using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using StardewTools.Core.Models;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>One row in the Stats tab's "All stats" section - covers every real scalar stat field
/// (StatsEditor.AllFieldNames/GetRaw/SetRaw) not already broken out as its own named property on
/// StatsTabViewModel, so nothing in the real &lt;stats&gt; element is unreachable from the UI.</summary>
public partial class StatRowViewModel : ViewModelBase
{
    private readonly StatsEditor _stats;
    private readonly string _fieldName;
    private bool _isBound;

    public string Label { get; }

    [ObservableProperty] private int _value;

    public StatRowViewModel(string fieldName, StatsEditor stats)
    {
        _fieldName = fieldName;
        _stats = stats;
        Label = Humanize(fieldName);
        _value = stats.GetRaw(fieldName);
        _isBound = true;
    }

    partial void OnValueChanged(int value) { if (_isBound) _stats.SetRaw(_fieldName, value); }

    /// <summary>"fishCaught" -> "Fish Caught" - purely cosmetic, no game-data lookup needed since
    /// the field names themselves are already real and descriptive.</summary>
    private static string Humanize(string camelCaseFieldName)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < camelCaseFieldName.Length; i++)
        {
            var c = camelCaseFieldName[i];
            if (i == 0)
            {
                sb.Append(char.ToUpperInvariant(c));
                continue;
            }

            if (char.IsUpper(c))
                sb.Append(' ');

            sb.Append(c);
        }

        return sb.ToString();
    }
}
