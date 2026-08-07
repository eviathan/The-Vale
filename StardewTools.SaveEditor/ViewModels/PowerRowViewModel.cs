using CommunityToolkit.Mvvm.ComponentModel;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>One row in the Powers tab - see PowersRoster/PowerUnlockKind for how each kind maps
/// to a real save mechanism (mail flag, seen event, or - not editable here - a stat_dictionary
/// entry with no verified real-example shape).</summary>
public partial class PowerRowViewModel : ViewModelBase
{
    private readonly PlayerEditor _player;
    private bool _isBound;

    public GamePower Power { get; }
    public string DisplayName => Power.DisplayName;
    public bool IsEditable => Power.Kind != PowerUnlockKind.StatDriven;
    public string KindLabel => Power.Kind switch
    {
        PowerUnlockKind.MailFlag => "special item",
        PowerUnlockKind.EventSeen => "seen event",
        _ => "Qi walnut room reward - not editable yet, see tooltip",
    };

    [ObservableProperty] private bool _isUnlocked;

    public PowerRowViewModel(GamePower power, PlayerEditor player)
    {
        Power = power;
        _player = player;
        _isUnlocked = power.Kind switch
        {
            PowerUnlockKind.MailFlag => player.HasMailFlag(power.Key),
            PowerUnlockKind.EventSeen => player.HasSeenEvent(power.Key),
            _ => false,
        };
        _isBound = true;
    }

    partial void OnIsUnlockedChanged(bool value)
    {
        if (!_isBound || !IsEditable)
            return;

        switch (Power.Kind)
        {
            case PowerUnlockKind.MailFlag:
                _player.SetMailFlag(Power.Key, value);
                break;
            case PowerUnlockKind.EventSeen:
                _player.SetSeenEvent(Power.Key, value);
                break;
        }
    }
}
