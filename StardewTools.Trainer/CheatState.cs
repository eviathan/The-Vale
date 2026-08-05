using StardewValley;

namespace StardewTools.Trainer;

/// <summary>Continuous cheat toggles, re-applied to the player every tick while enabled.</summary>
public class CheatState
{
    public bool InfiniteStamina { get; set; }
    public bool InfiniteHealth { get; set; }
    public float SpeedBonus { get; set; }

    public void Apply(Farmer? player)
    {
        if (player is null)
            return;

        if (InfiniteStamina)
            player.Stamina = player.MaxStamina;

        if (InfiniteHealth)
            player.health = player.maxHealth;

        if (SpeedBonus != 0)
            player.addedSpeed = SpeedBonus;
    }
}
