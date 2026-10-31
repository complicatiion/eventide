using Eventide.Core.Color;
using Eventide.Core.Scheduling;
using Eventide.Core.Settings;
using Eventide.Core.Solar;

namespace Eventide.Core.Tests;

public class ColorTemperatureTests
{
    [Fact]
    public void Neutral_is_identity()
    {
        Assert.Equal(RgbFactors.Neutral, ColorTemperature.ToFactors(6500));
        Assert.Equal(ColorTemperature.IdentityRamp(), ColorTemperature.BuildRamp(ColorTemperature.ToFactors(6500)));
    }

    [Theory]
    [InlineData(5000)]
    [InlineData(4200)]
    [InlineData(3400)]
    [InlineData(2700)]
    public void Warmer_means_less_blue(int kelvin)
    {
        var f = ColorTemperature.ToFactors(kelvin);
        Assert.Equal(1.0, f.R, 3);
        Assert.True(f.B < f.G && f.G < 1.0);
    }

    [Fact]
    public void Ramp_is_monotonic()
    {
        var ramp = ColorTemperature.BuildRamp(ColorTemperature.ToFactors(2700));
        for (var c = 0; c < 3; c++)
        {
            for (var i = 1; i < 256; i++)
            {
                Assert.True(ramp[(c * 256) + i] >= ramp[(c * 256) + i - 1]);
            }
        }
    }

    [Fact]
    public void Interpolation_hits_the_ends()
    {
        Assert.Equal(6500, ColorTemperature.Interpolate(6500, 3400, 0));
        Assert.Equal(3400, ColorTemperature.Interpolate(6500, 3400, 1));
        var mid = ColorTemperature.Interpolate(6500, 3400, 0.5);
        Assert.InRange(mid, 3400, 6500);
    }

    [Fact]
    public void Tinted_ramp_is_detected()
    {
        Assert.False(ColorTemperature.LooksTinted(ColorTemperature.IdentityRamp()));
        Assert.True(ColorTemperature.LooksTinted(ColorTemperature.BuildRamp(ColorTemperature.ToFactors(3400))));
    }
}

public class SolarCalculatorTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "W. Europe Standard Time" : "Europe/Berlin");

    [Fact]
    public void Bremen_midsummer()
    {
        // Bremen 53.08 N, 8.80 E, 21 June 2026: sunrise ≈ 04:57, sunset ≈ 21:57 (CEST)
        var sun = SolarCalculator.Calculate(new DateOnly(2026, 6, 21), 53.08, 8.80, Berlin);
        Assert.Equal(SunDayKind.Normal, sun.Kind);
        AssertNear(new TimeOnly(4, 57), sun.Sunrise!.Value, 6);
        AssertNear(new TimeOnly(21, 57), sun.Sunset!.Value, 6);
    }

    [Fact]
    public void Bremen_midwinter()
    {
        // 21 Dec 2026: sunrise ≈ 08:37, sunset ≈ 16:05 (CET)
        var sun = SolarCalculator.Calculate(new DateOnly(2026, 12, 21), 53.08, 8.80, Berlin);
        AssertNear(new TimeOnly(8, 37), sun.Sunrise!.Value, 6);
        AssertNear(new TimeOnly(16, 5), sun.Sunset!.Value, 6);
    }

    [Fact]
    public void Polar_days()
    {
        Assert.Equal(SunDayKind.PolarNight, SolarCalculator.Calculate(new DateOnly(2026, 12, 21), 78.2, 15.6, Berlin).Kind);
        Assert.Equal(SunDayKind.MidnightSun, SolarCalculator.Calculate(new DateOnly(2026, 6, 21), 78.2, 15.6, Berlin).Kind);
    }

    private static void AssertNear(TimeOnly expected, DateTimeOffset actual, int toleranceMinutes)
    {
        var diff = Math.Abs((TimeOnly.FromDateTime(actual.DateTime) - expected).TotalMinutes);
        diff = Math.Min(diff, 1440 - diff);
        Assert.True(diff <= toleranceMinutes, $"expected {expected}, got {actual:HH:mm}");
    }
}

public class FilterSchedulerTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "W. Europe Standard Time" : "Europe/Berlin");

    private static DateTimeOffset At(int month, int day, int hour, int minute)
    {
        var dt = new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(dt, Berlin.GetUtcOffset(dt));
    }

    private static EventideSettings Schedule() => new()
    {
        Mode = FilterMode.Schedule,
        ScheduleStart = new TimeOnly(21, 0),
        ScheduleEnd = new TimeOnly(7, 0),
        TransitionMinutes = 30,
        Intensity = Color.IntensityPreset.Strong,
    };

    [Fact]
    public void Schedule_day_transition_night()
    {
        var s = new FilterScheduler(Schedule(), Berlin);
        Assert.Equal(FilterPhase.Day, s.Evaluate(At(3, 10, 14, 0)).Phase);
        Assert.Equal(FilterPhase.Dusk, s.Evaluate(At(3, 10, 21, 10)).Phase);
        var night = s.Evaluate(At(3, 10, 23, 0));
        Assert.Equal(FilterPhase.Night, night.Phase);
        Assert.Equal(3200, night.TargetKelvin);
        Assert.Equal(FilterPhase.Night, s.Evaluate(At(3, 11, 3, 0)).Phase);
        Assert.Equal(FilterPhase.Dawn, s.Evaluate(At(3, 11, 6, 45)).Phase);
        Assert.Equal(FilterPhase.Day, s.Evaluate(At(3, 11, 7, 0)).Phase);
    }

    [Fact]
    public void Next_change_is_found()
    {
        var s = new FilterScheduler(Schedule(), Berlin);
        var state = s.Evaluate(At(3, 10, 14, 0));
        Assert.Equal(At(3, 10, 21, 1), state.NextChange);
    }

    [Fact]
    public void Off_and_paused_mean_day_temperature()
    {
        var off = Schedule();
        off.Enabled = false;
        Assert.Equal(6500, new FilterScheduler(off, Berlin).Evaluate(At(3, 10, 23, 0)).TargetKelvin);

        var paused = Schedule();
        paused.PausedUntil = At(3, 11, 0, 0);
        var state = new FilterScheduler(paused, Berlin).Evaluate(At(3, 10, 23, 0));
        Assert.Equal(FilterPhase.Paused, state.Phase);
        Assert.Equal(6500, state.TargetKelvin);
    }

    [Fact]
    public void Auto_mode_follows_the_sun()
    {
        var settings = new EventideSettings { Mode = FilterMode.Auto, Latitude = 53.08, Longitude = 8.80, TransitionMinutes = 45 };
        var s = new FilterScheduler(settings, Berlin);
        Assert.Equal(FilterPhase.Day, s.Evaluate(At(12, 21, 12, 0)).Phase);
        Assert.Equal(FilterPhase.Night, s.Evaluate(At(12, 21, 20, 0)).Phase);
        Assert.Equal(FilterPhase.Night, s.Evaluate(At(12, 22, 5, 0)).Phase);
    }

    [Fact]
    public void Always_on_is_night()
    {
        var s = new FilterScheduler(new EventideSettings { Mode = FilterMode.AlwaysOn, Intensity = Color.IntensityPreset.Extreme }, Berlin);
        var state = s.Evaluate(At(6, 1, 12, 0));
        Assert.Equal(FilterPhase.Night, state.Phase);
        Assert.Equal(2700, state.TargetKelvin);
        Assert.Null(state.NextChange);
    }

    [Fact]
    public void Pause_until_morning_is_the_schedule_end()
    {
        var s = new FilterScheduler(Schedule(), Berlin);
        Assert.Equal(At(3, 11, 7, 0), s.NextMorning(At(3, 10, 23, 0)));
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void Round_trip_and_update()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eventide-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(dir);
            Assert.True(store.Load().Enabled);
            store.Update(s =>
            {
                s.Mode = FilterMode.Schedule;
                s.Latitude = 53.08;
                s.Longitude = 8.8;
                s.Intensity = Color.IntensityPreset.Custom;
                s.CustomKelvin = 1000;
            });
            var loaded = store.Load();
            Assert.Equal(FilterMode.Schedule, loaded.Mode);
            Assert.Equal(53.08, loaded.Latitude);
            Assert.Equal(ColorTemperature.MinKelvin, loaded.CustomKelvin);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Damaged_file_gives_defaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eventide-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "settings.json"), "{ not json");
            var settings = new SettingsStore(dir).Load();
            Assert.Equal(FilterMode.Auto, settings.Mode);
            Assert.True(File.Exists(Path.Combine(dir, "settings.json.damaged")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}

public class DefaultsTests
{
    [Fact]
    public void New_settings_use_medium_and_one_hour_pause()
    {
        var s = new EventideSettings().Normalize();
        Assert.Equal(Color.IntensityPreset.Medium, s.Intensity);
        Assert.Equal(4200, s.NightKelvin);
        Assert.Equal(60, s.PauseMinutes);
    }

    [Fact]
    public void Warm_preset_sits_between_medium_and_strong()
    {
        Assert.Equal(3800, Color.Intensity.KelvinOf(Color.IntensityPreset.Warm, 0));
        Assert.Equal(
            [Color.IntensityPreset.Soft, Color.IntensityPreset.Medium, Color.IntensityPreset.Warm, Color.IntensityPreset.Strong, Color.IntensityPreset.Extreme],
            Color.Intensity.Presets);
        Assert.False(new EventideSettings().AutostartInitialized);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(5000, 60)]
    [InlineData(-1, -1)]
    [InlineData(45, 45)]
    public void Pause_minutes_are_normalised(int value, int expected)
    {
        var s = new EventideSettings { PauseMinutes = value }.Normalize();
        Assert.Equal(expected, s.PauseMinutes);
    }

    [Fact]
    public void Durations_are_formatted()
    {
        Localization.Loc.SetLanguage("en");
        Assert.Equal("15 min", Localization.Loc.Duration(15));
        Assert.Equal("2 h", Localization.Loc.Duration(120));
        Assert.Equal("1 h 30 min", Localization.Loc.Duration(90));
        Localization.Loc.SetLanguage("de");
        Assert.Equal("1 Std. 30 Min.", Localization.Loc.Duration(90));
        Localization.Loc.SetLanguage("auto");
    }
}
