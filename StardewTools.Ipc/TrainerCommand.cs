using System.Text.Json;

namespace StardewTools.Ipc;

public static class TrainerCommandType
{
    /// <summary>One-shot: set player money to IntValue.</summary>
    public const string SetMoney = "SetMoney";

    /// <summary>One-shot: set player health to IntValue.</summary>
    public const string SetHealth = "SetHealth";

    /// <summary>One-shot: set player max health to IntValue.</summary>
    public const string SetMaxHealth = "SetMaxHealth";

    /// <summary>One-shot: set player stamina to IntValue.</summary>
    public const string SetStamina = "SetStamina";

    /// <summary>Continuous: re-applied every tick while true. BoolValue toggles it.</summary>
    public const string ToggleInfiniteStamina = "ToggleInfiniteStamina";

    /// <summary>Continuous: re-applied every tick while true. BoolValue toggles it.</summary>
    public const string ToggleInfiniteHealth = "ToggleInfiniteHealth";

    /// <summary>Continuous: added movement speed while connected. FloatValue is the bonus.</summary>
    public const string SetSpeedBonus = "SetSpeedBonus";

    /// <summary>Request/response: ask the mod for the current TrainerState.</summary>
    public const string GetState = "GetState";
}

public sealed class TrainerCommand
{
    public string Type { get; init; } = "";
    public int? IntValue { get; init; }
    public bool? BoolValue { get; init; }
    public float? FloatValue { get; init; }

    public string ToJsonLine() => JsonSerializer.Serialize(this);

    public static TrainerCommand? FromJsonLine(string line)
        => JsonSerializer.Deserialize<TrainerCommand>(line);
}
