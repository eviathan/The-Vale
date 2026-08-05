using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewTools.Ipc;

namespace StardewTools.Trainer;

/// <summary>
/// Local-only IPC server the desktop app connects to. Runs entirely on background
/// threads; every incoming command is queued and applied to the game on the main
/// thread from ModEntry's UpdateTicked handler, since Farmer/Game1 state should
/// only ever be touched from the game's own update loop.
/// </summary>
public sealed class PipeServer
{
    private readonly IMonitor _monitor;
    private readonly ConcurrentQueue<TrainerCommand> _incoming;
    private readonly Func<TrainerState> _getState;
    private readonly CancellationTokenSource _cts = new();

    public PipeServer(IMonitor monitor, ConcurrentQueue<TrainerCommand> incoming, Func<TrainerState> getState)
    {
        _monitor = monitor;
        _incoming = incoming;
        _getState = getState;
    }

    public void Start()
    {
        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void Stop() => _cts.Cancel();

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(
                    PipeNames.Trainer, PipeDirection.InOut, maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(token);
                await HandleClientAsync(pipe, token);
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
            catch (Exception ex)
            {
                _monitor.Log($"Trainer pipe server error: {ex.Message}", LogLevel.Warn);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        using var reader = new StreamReader(pipe);
        using var writer = new StreamWriter(pipe) { AutoFlush = true };

        while (pipe.IsConnected && !token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
                break; // client disconnected

            var command = TrainerCommand.FromJsonLine(line);
            if (command is null)
                continue;

            if (command.Type == TrainerCommandType.GetState)
            {
                await writer.WriteLineAsync(_getState().ToJsonLine());
            }
            else
            {
                _incoming.Enqueue(command);
            }
        }
    }
}
