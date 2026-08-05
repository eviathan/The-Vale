using System.Text.Json;

namespace StardewTools.Ipc;

/// <summary>Snapshot of live player state, sent by the mod in reply to a GetState command.</summary>
public sealed class TrainerState
{
    public string PlayerName { get; init; } = "";
    public int Money { get; init; }
    public int Health { get; init; }
    public int MaxHealth { get; init; }
    public int Stamina { get; init; }
    public bool InfiniteStamina { get; init; }
    public bool InfiniteHealth { get; init; }
    public float SpeedBonus { get; init; }

    public string ToJsonLine() => JsonSerializer.Serialize(this);

    public static TrainerState? FromJsonLine(string line)
        => JsonSerializer.Deserialize<TrainerState>(line);
}
