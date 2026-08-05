using System.Collections.Concurrent;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewTools.Ipc;
using StardewValley;

namespace StardewTools.Trainer;

public class ModEntry : Mod
{
    private readonly CheatState _cheats = new();
    private readonly ConcurrentQueue<TrainerCommand> _incoming = new();
    private PipeServer _pipeServer = null!;

    public override void Entry(IModHelper helper)
    {
        _pipeServer = new PipeServer(Monitor, _incoming, GetState);
        _pipeServer.Start();

        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => _pipeServer.Stop();
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        while (_incoming.TryDequeue(out var command))
            Apply(command);

        _cheats.Apply(Game1.player);
    }

    private void Apply(TrainerCommand command)
    {
        var player = Game1.player;
        if (player is null)
            return;

        switch (command.Type)
        {
            case TrainerCommandType.SetMoney when command.IntValue is int money:
                player.Money = money;
                break;
            case TrainerCommandType.SetHealth when command.IntValue is int health:
                player.health = health;
                break;
            case TrainerCommandType.SetMaxHealth when command.IntValue is int maxHealth:
                player.maxHealth = maxHealth;
                break;
            case TrainerCommandType.SetStamina when command.IntValue is int stamina:
                player.Stamina = stamina;
                break;
            case TrainerCommandType.ToggleInfiniteStamina when command.BoolValue is bool infiniteStamina:
                _cheats.InfiniteStamina = infiniteStamina;
                break;
            case TrainerCommandType.ToggleInfiniteHealth when command.BoolValue is bool infiniteHealth:
                _cheats.InfiniteHealth = infiniteHealth;
                break;
            case TrainerCommandType.SetSpeedBonus when command.FloatValue is float speedBonus:
                _cheats.SpeedBonus = speedBonus;
                break;
        }
    }

    // Called from the pipe server's background thread. Reads Game1.player's fields
    // without marshaling to the main thread - technically a data race, but these are
    // plain value-type reads for display purposes only, not writes, so a torn read
    // just means a stale UI value for one poll. Not worth the complexity of routing
    // state queries through the same tick-synchronized queue as commands.
    private TrainerState GetState()
    {
        var player = Game1.player;
        return new TrainerState
        {
            PlayerName = player?.Name ?? "",
            Money = player?.Money ?? 0,
            Health = player?.health ?? 0,
            MaxHealth = player?.maxHealth ?? 0,
            Stamina = (int)(player?.Stamina ?? 0),
            InfiniteStamina = _cheats.InfiniteStamina,
            InfiniteHealth = _cheats.InfiniteHealth,
            SpeedBonus = _cheats.SpeedBonus,
        };
    }
}
