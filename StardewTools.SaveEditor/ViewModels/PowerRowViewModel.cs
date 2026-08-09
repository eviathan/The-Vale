using CommunityToolkit.Mvvm.ComponentModel;
using StardewTools.Core.Models;
using StardewTools.SaveEditor.MapAssets;

namespace StardewTools.SaveEditor.ViewModels;

/// <summary>One row in the Powers tab - see PowersRoster/PowerUnlockKind for how each kind maps
/// to a real save mechanism (mail flag, seen event, or a Stats.Values entry).</summary>
public partial class PowerRowViewModel : ViewModelBase
{
    private readonly PlayerEditor _player;
    private readonly StatsEditor _stats;
    private bool _isBound;

    public GamePower Power { get; }
    public string DisplayName => Power.DisplayName;
    public string KindLabel => Power.Kind switch
    {
        PowerUnlockKind.MailFlag => "special item",
        PowerUnlockKind.EventSeen => "seen event",
        _ => "Qi walnut room reward",
    };

    [ObservableProperty] private bool _isUnlocked;

    public PowerRowViewModel(GamePower power, PlayerEditor player, StatsEditor stats)
    {
        Power = power;
        _player = player;
        _stats = stats;
        _isUnlocked = power.Kind switch
        {
            PowerUnlockKind.MailFlag => player.HasMailFlag(power.Key),
            PowerUnlockKind.EventSeen => player.HasSeenEvent(power.Key),
            _ => stats.GetRaw(power.Key) >= 1,
        };
        _isBound = true;
    }

    partial void OnIsUnlockedChanged(bool value)
    {
        if (!_isBound)
            return;

        switch (Power.Kind)
        {
            case PowerUnlockKind.MailFlag:
                _player.SetMailFlag(Power.Key, value);
                break;
            case PowerUnlockKind.EventSeen:
                _player.SetSeenEvent(Power.Key, value);
                break;
            case PowerUnlockKind.StatDriven:
                // PLAYER_STAT Current <key> 1 - Stats.Get(key) >= 1 is "unlocked" (decompiled
                // Stats.cs's own Get/Set never store a value below 1 meaningfully: Set(key, 0)
                // actually REMOVES the key - see StatsEditor.Set mirroring that same convention).
                _stats.SetRaw(Power.Key, value ? 1 : 0);
                break;
        }
    }
}
