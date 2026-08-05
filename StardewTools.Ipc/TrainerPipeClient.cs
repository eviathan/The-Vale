using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace StardewTools.Ipc;

/// <summary>Client side of the local trainer pipe. One instance per app session; reconnects on demand.</summary>
public sealed class TrainerPipeClient : IAsyncDisposable
{
    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public bool IsConnected => _pipe?.IsConnected == true;

    /// <summary>Attempts a connection, giving up after <paramref name="timeoutMs"/> if the game/mod isn't up yet.</summary>
    public async Task<bool> TryConnectAsync(int timeoutMs = 500)
    {
        if (IsConnected)
            return true;

        try
        {
            var pipe = new NamedPipeClientStream(".", PipeNames.Trainer, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeoutMs);

            _pipe = pipe;
            _reader = new StreamReader(pipe);
            _writer = new StreamWriter(pipe) { AutoFlush = true };
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task SendAsync(TrainerCommand command)
    {
        if (!IsConnected || _writer is null)
            return;

        await _writer.WriteLineAsync(command.ToJsonLine());
    }

    public async Task<TrainerState?> GetStateAsync()
    {
        if (!IsConnected || _writer is null || _reader is null)
            return null;

        await _writer.WriteLineAsync(new TrainerCommand { Type = TrainerCommandType.GetState }.ToJsonLine());
        var line = await _reader.ReadLineAsync();
        return line is null ? null : TrainerState.FromJsonLine(line);
    }

    public ValueTask DisposeAsync()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _pipe?.Dispose();
        return ValueTask.CompletedTask;
    }
}
