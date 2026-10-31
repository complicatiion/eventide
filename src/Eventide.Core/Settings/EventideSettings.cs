using Eventide.Core.Color;

namespace Eventide.Core.Settings;

public enum FilterMode
{
    /// <summary>Sunset → sunrise at the configured location.</summary>
    Auto,

    /// <summary>Fixed time window, e.g. 21:00 – 07:00.</summary>
    Schedule,

    /// <summary>Permanent filter.</summary>
    AlwaysOn,
}

/// <summary>User settings, stored as JSON (see <see cref="SettingsStore"/>).</summary>
public sealed class EventideSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Master switch (left click on the tray icon).</summary>
    public bool Enabled { get; set; } = true;

    public FilterMode Mode { get; set; } = FilterMode.Auto;

    public IntensityPreset Intensity { get; set; } = IntensityPreset.Medium;

    /// <summary>Night temperature for <see cref="IntensityPreset.Custom"/>.</summary>
    public int CustomKelvin { get; set; } = 3800;

    /// <summary>Temperature outside the night window (6500 K = no change).</summary>
    public int DayKelvin { get; set; } = ColorTemperature.NeutralKelvin;

    /// <summary>Location for <see cref="FilterMode.Auto"/>; null = not set (approximation from the time zone).</summary>
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>Optional display name of the location.</summary>
    public string? LocationName { get; set; }

    public TimeOnly ScheduleStart { get; set; } = new(21, 0);

    public TimeOnly ScheduleEnd { get; set; } = new(7, 0);

    /// <summary>Length of the soft transition at dusk and dawn (0 – 120 minutes).</summary>
    public int TransitionMinutes { get; set; } = 45;

    /// <summary>Default pause length in minutes (tray menu and settings window); <see cref="PauseUntilMorning"/> = until tomorrow morning.</summary>
    public int PauseMinutes { get; set; } = 60;

    /// <summary>Value of <see cref="PauseMinutes"/> that means "until tomorrow morning".</summary>
    public const int PauseUntilMorning = -1;

    /// <summary>Fixed pause choices (minutes) of the tray menu and the settings window.</summary>
    public static readonly int[] PausePresets = [15, 30, 60, 120, 240];

    /// <summary>Filter paused until this time (null = not paused).</summary>
    public DateTimeOffset? PausedUntil { get; set; }

    /// <summary>
    /// The tray has switched on "Start with Windows" once (first start). Afterwards the user's choice is kept,
    /// also when it is off.
    /// </summary>
    public bool AutostartInitialized { get; set; }

    /// <summary>"auto", "en" or "de".</summary>
    public string Language { get; set; } = "auto";

    public bool HasLocation => Latitude is not null && Longitude is not null;

    public int NightKelvin => Eventide.Core.Color.Intensity.KelvinOf(Intensity, CustomKelvin);

    public bool IsPaused(DateTimeOffset now) => PausedUntil is { } until && until > now;

    /// <summary>Repairs out-of-range values after loading (hand-edited or older files).</summary>
    public EventideSettings Normalize()
    {
        Version = CurrentVersion;
        CustomKelvin = ColorTemperature.Clamp(CustomKelvin);
        DayKelvin = ColorTemperature.Clamp(DayKelvin);
        TransitionMinutes = Math.Clamp(TransitionMinutes, 0, 120);
        if (!Enum.IsDefined(Mode))
        {
            Mode = FilterMode.Auto;
        }

        if (!Enum.IsDefined(Intensity))
        {
            Intensity = IntensityPreset.Medium;
        }

        if (PauseMinutes != PauseUntilMorning && PauseMinutes is < 1 or > 1440)
        {
            PauseMinutes = 60;
        }

        if (Latitude is { } lat && Longitude is { } lon && !Solar.SolarCalculator.IsValid(lat, lon))
        {
            Latitude = null;
            Longitude = null;
        }

        if (Latitude is null || Longitude is null)
        {
            Latitude = null;
            Longitude = null;
        }

        if (Language is not ("auto" or "en" or "de"))
        {
            Language = "auto";
        }

        return this;
    }

    public EventideSettings Clone() => (EventideSettings)MemberwiseClone();
}
