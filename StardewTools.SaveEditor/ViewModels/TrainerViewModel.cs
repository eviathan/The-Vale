using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StardewTools.Ipc;

namespace StardewTools.SaveEditor.ViewModels;

public partial class TrainerViewModel : ViewModelBase, IAsyncDisposable
{
    private const string Disconnected = "Waiting for the game (launch it via SMAPI with the Trainer mod installed)...";

    private readonly TrainerPipeClient _client = new();
    private CancellationTokenSource? _pollCts;

    // Set while we're applying a snapshot received from the mod, so the OnXChanged
    // hooks below don't immediately echo the same value straight back to it.
    private bool _isSyncingFromServer;

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusMessage = Disconnected;

    [ObservableProperty] private string _playerName = "";
    [ObservableProperty] private int _money;
    [ObservableProperty] private int _health;
    [ObservableProperty] private int _maxHealth;
    [ObservableProperty] private int _stamina;
    [ObservableProperty] private bool _infiniteStamina;
    [ObservableProperty] private bool _infiniteHealth;
    [ObservableProperty] private float _speedBonus;

    public void Start()
    {
        _pollCts = new CancellationTokenSource();
        _ = PollLoopAsync(_pollCts.Token);
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (!_client.IsConnected && !await _client.TryConnectAsync())
            {
                await SetConnected(false, Disconnected);
            }
            else
            {
                var state = await _client.GetStateAsync();
                if (state is null)
                    await SetConnected(false, "Lost connection to the game.");
                else
                    await ApplyState(state);
            }

            try
            {
                await Task.Delay(1000, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task SetConnected(bool connected, string message) => await Dispatcher.UIThread.InvokeAsync(() =>
    {
        IsConnected = connected;
        StatusMessage = message;
    });

    private async Task ApplyState(TrainerState state) => await Dispatcher.UIThread.InvokeAsync(() =>
    {
        _isSyncingFromServer = true;
        IsConnected = true;
        StatusMessage = "Connected to running game.";
        PlayerName = state.PlayerName;
        Money = state.Money;
        Health = state.Health;
        MaxHealth = state.MaxHealth;
        Stamina = state.Stamina;
        InfiniteStamina = state.InfiniteStamina;
        InfiniteHealth = state.InfiniteHealth;
        SpeedBonus = state.SpeedBonus;
        _isSyncingFromServer = false;
    });

    [RelayCommand]
    private Task ApplyMoney() => _client.SendAsync(new TrainerCommand { Type = TrainerCommandType.SetMoney, IntValue = Money });

    [RelayCommand]
    private Task ApplyHealth() => _client.SendAsync(new TrainerCommand { Type = TrainerCommandType.SetHealth, IntValue = Health });

    [RelayCommand]
    private Task ApplyMaxHealth() => _client.SendAsync(new TrainerCommand { Type = TrainerCommandType.SetMaxHealth, IntValue = MaxHealth });

    [RelayCommand]
    private Task ApplyStamina() => _client.SendAsync(new TrainerCommand { Type = TrainerCommandType.SetStamina, IntValue = Stamina });

    partial void OnInfiniteStaminaChanged(bool value)
    {
        if (!_isSyncingFromServer)
            _ = _client.SendAsync(new TrainerCommand { Type = TrainerCommandType.ToggleInfiniteStamina, BoolValue = value });
    }

    partial void OnInfiniteHealthChanged(bool value)
    {
        if (!_isSyncingFromServer)
            _ = _client.SendAsync(new TrainerCommand { Type = TrainerCommandType.ToggleInfiniteHealth, BoolValue = value });
    }

    partial void OnSpeedBonusChanged(float value)
    {
        if (!_isSyncingFromServer)
            _ = _client.SendAsync(new TrainerCommand { Type = TrainerCommandType.SetSpeedBonus, FloatValue = value });
    }

    public async ValueTask DisposeAsync()
    {
        _pollCts?.Cancel();
        await _client.DisposeAsync();
    }
}
