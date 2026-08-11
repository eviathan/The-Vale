using System.Collections.Concurrent;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewTools.Ipc;
using StardewValley;
using StardewValley.Buildings;

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
        helper.Events.Player.Warped += OnWarped;

        helper.ConsoleCommands.Add("vale_animals", "Dumps live in-memory FarmAnimal state for every AnimalHouse building on the farm.", (_, _) => DumpAnimalState());
    }

    // Fires automatically on every warp so the diagnostic doesn't depend on someone typing into
    // the SMAPI console mid-playtest - just walking into (or out of) a barn/coop is enough to
    // get a fresh dump logged.
    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (e.NewLocation is AnimalHouse || e.OldLocation is AnimalHouse)
        {
            Monitor.Log($"[auto] Warped from {e.OldLocation?.NameOrUniqueName} to {e.NewLocation?.NameOrUniqueName}", LogLevel.Info);
            DumpAnimalState();
        }
    }

    private void DumpAnimalState()
    {
        var farm = Game1.getFarm();
        if (farm is null)
        {
            Monitor.Log("No farm loaded.", LogLevel.Error);
            return;
        }

        foreach (var building in farm.buildings)
        {
            var indoors = building.GetIndoors();
            if (indoors is not AnimalHouse animalHouse)
                continue;

            Monitor.Log($"Building {building.buildingType.Value} id={building.id.Value} at ({building.tileX.Value},{building.tileY.Value}) - indoor animals.Length={animalHouse.animals.Length}, animalsThatLiveHere.Count={animalHouse.animalsThatLiveHere.Count}", LogLevel.Info);

            foreach (var animal in animalHouse.animals.Values)
            {
                var data = animal.GetAnimalData();
                Monitor.Log(
                    $"  animal Name='{animal.Name}' Type='{animal.type.Value}' MyID={animal.myID.Value}" +
                    $" currentLocation={(animal.currentLocation is null ? "NULL" : animal.currentLocation.NameOrUniqueName)}" +
                    $" home={(animal.home is null ? "NULL" : animal.home.buildingType.Value)}" +
                    $" Position=({animal.Position.X},{animal.Position.Y})" +
                    $" Sprite={(animal.Sprite is null ? "NULL" : "ok")}" +
                    $" GetAnimalData={(data is null ? "NULL" : "ok")}" +
                    $" health={animal.health.Value}",
                    LogLevel.Info);
            }
        }

        Monitor.Log("Also checking farm.Animals (top-level/outdoor animals dict):", LogLevel.Info);
        foreach (var animal in farm.Animals.Values)
        {
            Monitor.Log($"  outdoor animal Name='{animal.Name}' Type='{animal.type.Value}' MyID={animal.myID.Value} currentLocation={(animal.currentLocation is null ? "NULL" : animal.currentLocation.NameOrUniqueName)}", LogLevel.Info);
        }
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
