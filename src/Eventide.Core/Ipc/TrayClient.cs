using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Eventide.Core.Ipc;

/// <summary>Client side of <see cref="TrayProtocol"/>. Returns null when the tray is not running.</summary>
public static class TrayClient
{
    public static TrayResponse? Send(TrayRequest request, int timeoutMs = 600)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", TrayProtocol.PipeName(Process.GetCurrentProcess().SessionId),
                PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            pipe.Connect(timeoutMs);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
            writer.WriteLine(JsonSerializer.Serialize(request, TrayProtocol.Json));
            var line = reader.ReadLine();
            return line is null ? null : JsonSerializer.Deserialize<TrayResponse>(line, TrayProtocol.Json);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static Task<TrayResponse?> SendAsync(TrayRequest request, int timeoutMs = 600) =>
        Task.Run(() => Send(request, timeoutMs));
}
