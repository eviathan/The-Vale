using System.Collections.ObjectModel;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>Real "Powers" (Data/Powers.json) - covers what pre-1.6 saves called "special items"
/// (Rusty Key, Skull Key, Dwarvish Translation Guide, Club Card, ...) plus Qi Walnut Room
/// rewards (skill books, masteries). See PowerRowViewModel/PowersRoster for how each kind maps
/// to its real save mechanism.</summary>
public sealed class PowersTabViewModel : ViewModelBase
{
    public ObservableCollection<PowerRowViewModel> Powers { get; } = new();

    public void Bind(SaveGameEditor save)
    {
        Powers.Clear();
        foreach (var power in PowersRoster.All)
            Powers.Add(new PowerRowViewModel(power, save.Player, save.Stats));
    }
}
