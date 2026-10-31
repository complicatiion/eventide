using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Eventide.Core.Ipc;

namespace Eventide.App.Tray;

/// <summary>
/// Server side of <see cref="TrayProtocol"/>: accepts one request per connection on a background thread and runs the
/// handler on the UI thread (synchronisation context of the tray).
/// </summary>
internal sealed class TrayPipeServer : IDisposable
{
    private readonly SynchronizationContext _ui;
    private readonly Func<TrayRequest, TrayResponse> _handler;
    private readonly CancellationTokenSource _cts = new();
    private int _disposed;
    private readonly string _pipeName = TrayProtocol.PipeName(Process.GetCurrentProcess().SessionId);

    public TrayPipeServer(SynchronizationContext ui, Func<TrayRequest, TrayResponse> handler)
    {
        _ui = ui;
        _handler = handler;
    }

    public void Start() => _ = Task.Run(() => LoopAsync(_cts.Token));

    /// <summary>Stops the server. Safe to call several times (Windows Forms already disposes the tray context on exit).</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // already gone
        }

        _cts.Dispose();
    }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 4, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                await HandleAsync(pipe, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                // client did not send a request in time – accept the next one
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or TimeoutException or InvalidOperationException)
            {
                // broken client – wait a moment and accept the next one
                await Task.Delay(200, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var line = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var request = JsonSerializer.Deserialize<TrayRequest>(line, TrayProtocol.Json);
        TrayResponse response;
        if (request is null)
        {
            response = new TrayResponse(false, Error: "empty request");
        }
        else
        {
            var done = new TaskCompletionSource<TrayResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ui.Post(_ =>
            {
                try
                {
                    done.SetResult(_handler(request));
                }
                catch (Exception ex)
                {
                    done.SetResult(new TrayResponse(false, Error: ex.Message));
                }
            }, null);
            response = await done.Task.WaitAsync(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
        }

        await writer.WriteLineAsync(JsonSerializer.Serialize(response, TrayProtocol.Json).AsMemory(), token).ConfigureAwait(false);
    }
}
