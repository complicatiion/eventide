using Eventide.Core.Color;
using Eventide.Core.Settings;
using Eventide.Core.Solar;

namespace Eventide.Core.Scheduling;

public enum FilterPhase
{
    /// <summary>Master switch off.</summary>
    Off,

    /// <summary>Paused by the user (until <see cref="FilterState.PausedUntil"/>).</summary>
    Paused,

    /// <summary>Outside the night window: day temperature.</summary>
    Day,

    /// <summary>Evening transition (getting warmer).</summary>
    Dusk,

    /// <summary>Full night temperature.</summary>
    Night,

    /// <summary>Morning transition (getting cooler).</summary>
    Dawn,
}

/// <summary>Result of one evaluation.</summary>
public readonly record struct FilterState(
    FilterPhase Phase,
    double NightFactor,
    int TargetKelvin,
    DateTimeOffset? NextChange,
    DateTimeOffset? PausedUntil,
    bool LocationApproximate);

/// <summary>
/// Decides which colour temperature applies at a point in time.
/// The night window of a day starts at sunset (Auto) or the schedule start and ends at the next sunrise or the
/// schedule end. The transition runs <i>after</i> the start (getting warmer) and <i>before</i> the end (back to
/// daylight), so the configured times are the edges the user expects: at 21:00 dimming begins, at 07:00 it's day.
/// </summary>
public sealed class FilterScheduler
{
    private readonly EventideSettings _settings;
    private readonly TimeZoneInfo _timeZone;
    private readonly Dictionary<DateOnly, SunTimes> _sunCache = [];
    private readonly double _latitude;
    private readonly double _longitude;

    public FilterScheduler(EventideSettings settings, TimeZoneInfo? timeZone = null)
    {
        _settings = settings;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
        LocationApproximate = !settings.HasLocation;
        (_latitude, _longitude) = settings.HasLocation
            ? (settings.Latitude!.Value, settings.Longitude!.Value)
            : SolarCalculator.ApproximateFromTimeZone(_timeZone);
    }

    public bool LocationApproximate { get; }

    public double Latitude => _latitude;

    public double Longitude => _longitude;

    public SunTimes SunTimesOf(DateOnly date)
    {
        if (!_sunCache.TryGetValue(date, out var times))
        {
            times = SolarCalculator.Calculate(date, _latitude, _longitude, _timeZone);
            _sunCache[date] = times;
        }

        return times;
    }

    /// <summary>Full evaluation incl. phase and next change (scans ahead, call about once per tick).</summary>
    public FilterState Evaluate(DateTimeOffset now, bool includeNextChange = true)
    {
        if (!_settings.Enabled)
        {
            return new FilterState(FilterPhase.Off, 0, _settings.DayKelvin, null, null, LocationApproximate);
        }

        if (_settings.IsPaused(now))
        {
            return new FilterState(FilterPhase.Paused, 0, _settings.DayKelvin, _settings.PausedUntil, _settings.PausedUntil, LocationApproximate);
        }

        var factor = NightFactor(now);
        var kelvin = ColorTemperature.Interpolate(_settings.DayKelvin, _settings.NightKelvin, factor);
        var phase = PhaseOf(now, factor);
        var next = includeNextChange && _settings.Mode != FilterMode.AlwaysOn ? NextChange(now) : null;
        return new FilterState(phase, factor, kelvin, next, null, LocationApproximate);
    }

    /// <summary>0 = day … 1 = night (pause and master switch not considered).</summary>
    public double NightFactor(DateTimeOffset now)
    {
        if (_settings.Mode == FilterMode.AlwaysOn)
        {
            return 1;
        }

        now = TimeZoneInfo.ConvertTime(now, _timeZone);
        var today = DateOnly.FromDateTime(now.DateTime);

        if (_settings.Mode == FilterMode.Auto)
        {
            var sun = SunTimesOf(today);
            if (sun.Kind == SunDayKind.PolarNight)
            {
                return 1;
            }

            if (sun.Kind == SunDayKind.MidnightSun)
            {
                return 0;
            }
        }

        var factor = 0.0;
        foreach (var day in new[] { today.AddDays(-1), today })
        {
            if (NightWindow(day) is { } window)
            {
                factor = Math.Max(factor, WindowFactor(now, window.Start, window.End));
            }
        }

        return factor;
    }

    /// <summary>
    /// When the user picks "pause until tomorrow morning": the end of the current (or next) night window;
    /// for "always on" 06:00 the next morning.
    /// </summary>
    public DateTimeOffset NextMorning(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, _timeZone);
        var today = DateOnly.FromDateTime(local.DateTime);
        if (_settings.Mode != FilterMode.AlwaysOn)
        {
            foreach (var day in new[] { today.AddDays(-1), today, today.AddDays(1) })
            {
                if (NightWindow(day) is { } window && window.End > now)
                {
                    return window.End;
                }
            }
        }

        var sixAm = today.AddDays(local.Hour < 6 ? 0 : 1).ToDateTime(new TimeOnly(6, 0), DateTimeKind.Unspecified);
        return new DateTimeOffset(sixAm, _timeZone.GetUtcOffset(sixAm));
    }

    /// <summary>The night window that starts on the evening of <paramref name="day"/>, or null (midnight sun).</summary>
    public (DateTimeOffset Start, DateTimeOffset End)? NightWindow(DateOnly day)
    {
        if (_settings.Mode == FilterMode.Schedule)
        {
            var start = Local(day, _settings.ScheduleStart);
            var endDay = _settings.ScheduleEnd <= _settings.ScheduleStart ? day.AddDays(1) : day;
            var end = Local(endDay, _settings.ScheduleEnd);
            if (end <= start)
            {
                end = start.AddDays(1);
            }

            return (start, end);
        }

        var evening = SunTimesOf(day);
        var morning = SunTimesOf(day.AddDays(1));
        if (evening.Kind == SunDayKind.MidnightSun)
        {
            return null;
        }

        // Polar edge cases: fall back to the neighbour day's time shifted by one day.
        var sunset = evening.Sunset ?? (morning.Sunset?.AddDays(-1));
        var sunrise = morning.Sunrise ?? (evening.Sunrise?.AddDays(1));
        if (sunset is null || sunrise is null || sunrise <= sunset)
        {
            return null;
        }

        return (sunset.Value, sunrise.Value);
    }

    private double WindowFactor(DateTimeOffset now, DateTimeOffset start, DateTimeOffset end)
    {
        if (now < start || now >= end)
        {
            return 0;
        }

        var length = (end - start).TotalMinutes;
        var transition = Math.Min(_settings.TransitionMinutes, length / 2);
        if (transition <= 0)
        {
            return 1;
        }

        var sinceStart = (now - start).TotalMinutes;
        var untilEnd = (end - now).TotalMinutes;
        if (sinceStart < transition)
        {
            return Smooth(sinceStart / transition);
        }

        if (untilEnd < transition)
        {
            return Smooth(untilEnd / transition);
        }

        return 1;
    }

    private FilterPhase PhaseOf(DateTimeOffset now, double factor)
    {
        if (factor <= 0)
        {
            return FilterPhase.Day;
        }

        if (factor >= 1)
        {
            return FilterPhase.Night;
        }

        return NightFactor(now.AddMinutes(1)) >= factor ? FilterPhase.Dusk : FilterPhase.Dawn;
    }

    /// <summary>Next time the phase category (day / transition / night) changes, scanned minute by minute up to 48 h.</summary>
    private DateTimeOffset? NextChange(DateTimeOffset now)
    {
        static int Category(double f) => f <= 0 ? 0 : f >= 1 ? 2 : 1;

        var current = Category(NightFactor(now));
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Offset).AddMinutes(1);
        for (var i = 0; i < 48 * 60; i++)
        {
            var t = start.AddMinutes(i);
            if (Category(NightFactor(t)) != current)
            {
                return t;
            }
        }

        return null;
    }

    private DateTimeOffset Local(DateOnly day, TimeOnly time)
    {
        var dt = day.ToDateTime(time, DateTimeKind.Unspecified);

        // Skip a time that doesn't exist on a DST switch day (e.g. 02:30 in spring).
        if (_timeZone.IsInvalidTime(dt))
        {
            dt = dt.AddHours(1);
        }

        return new DateTimeOffset(dt, _timeZone.GetUtcOffset(dt));
    }

    private static double Smooth(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - (2 * x));
    }
}
