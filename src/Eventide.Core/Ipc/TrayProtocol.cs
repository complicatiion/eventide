using System.Text.Json;
using System.Text.Json.Serialization;
using Eventide.Core.Scheduling;
using Eventide.Core.Settings;

namespace Eventide.Core.Ipc;

/// <summary>
/// Named pipe between the tray (server) and the settings window or a second start of the EXE (client).
/// One JSON line request, one JSON line response per connection. The pipe is restricted to the current user
/// (PipeOptions.CurrentUserOnly) and the current Windows session.
/// </summary>
public static class TrayProtocol
{
    public static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string PipeName(int sessionId) => $"Eventide.Tray.{Environment.UserName}.{sessionId}";
}

public static class TrayCommand
{
    /// <summary>Returns <see cref="TrayStatus"/>.</summary>
    public const string Status = "status";

    /// <summary>Settings file changed – reload it.</summary>
    public const string Reload = "reload";

    /// <summary>Show <see cref="TrayRequest.Kelvin"/> right now (slider in the settings window).</summary>
    public const string Preview = "preview";

    /// <summary>Back to the scheduled temperature.</summary>
    public const string PreviewEnd = "previewEnd";

    public const string Toggle = "toggle";
    /// <summary>Pause for <see cref="TrayRequest.Minutes"/> minutes.</summary>
    public const string PauseFor = "pauseFor";
    public const string PauseUntilMorning = "pauseMorning";
    public const string Resume = "resume";

    /// <summary>Second start of Eventide.exe: open the settings window.</summary>
    public const string ShowSettings = "showSettings";

    /// <summary>Restore the display colours and quit (installer, update).</summary>
    public const string Exit = "exit";
}

public sealed record TrayRequest(string Command, int? Kelvin = null, int? Minutes = null);

public sealed record TrayResponse(bool Ok, TrayStatus? Status = null, string? Error = null);

/// <summary>Live state of the tray for the status page of the settings window.</summary>
public sealed record TrayStatus
{
    public bool Enabled { get; init; }

    public FilterMode Mode { get; init; }

    public FilterPhase Phase { get; init; }

    /// <summary>Temperature currently on the screens (may lag behind <see cref="TargetKelvin"/> during a fade).</summary>
    public int AppliedKelvin { get; init; }

    public int TargetKelvin { get; init; }

    public int NightKelvin { get; init; }

    public DateTimeOffset? NextChange { get; init; }

    public DateTimeOffset? PausedUntil { get; init; }

    public DateTimeOffset? Sunrise { get; init; }

    public DateTimeOffset? Sunset { get; init; }

    public bool LocationApproximate { get; init; }

    /// <summary>Windows refused the ramp: the extended gamma range is missing; <see cref="LimitKelvin"/> is the warmest reachable value.</summary>
    public bool GammaLimited { get; init; }

    public int? LimitKelvin { get; init; }

    /// <summary>No display accepted any gamma ramp (driver, remote session, virtual display).</summary>
    public bool GammaUnsupported { get; init; }

    public bool ExtendedGammaRange { get; init; }

    public bool HdrActive { get; init; }

    public bool NightLightActive { get; init; }

    public bool RemoteSession { get; init; }

    public bool PreviewActive { get; init; }

    public int DisplayCount { get; init; }
}
