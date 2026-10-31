using Eventide.Core.Color;
using Eventide.Core.Ipc;
using Eventide.Core.Scheduling;
using Eventide.Core.Settings;
using Eventide.Platform.Windows;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace Eventide.App.Tray;

/// <summary>
/// Drives the gamma ramps: evaluates the schedule every 2 s, fades jumps (toggle, mode change, preview) over about
/// one second, re-applies the ramp every 30 s (games, display changes, sleep and UAC reset it) and restores the
/// original ramps when it stops. Runs on the UI thread of the tray.
/// </summary>
internal sealed class FilterEngine : IDisposable
{
    private const int TickMs = 2000;
    private const int FadeStepMs = 40;
    private const int FadeSteps = 25;
    private static readonly TimeSpan ReapplyInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan EnvironmentInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PreviewTimeout = TimeSpan.FromMinutes(5);

    private readonly GammaController _gamma = new();
    private readonly WinFormsTimer _tick = new() { Interval = TickMs };
    private readonly WinFormsTimer _fade = new() { Interval = FadeStepMs };

    private FilterScheduler _scheduler;
    private FilterState _state;
    private GammaResult _lastResult;
    private int _appliedKelvin = ColorTemperature.NeutralKelvin;
    private int _requestedKelvin = ColorTemperature.NeutralKelvin;
    private int _fadeFrom;
    private int _fadeTo;
    private int _fadeStep;
    private int? _previewKelvin;
    private DateTimeOffset _previewSince;
    private DateTimeOffset _lastApply = DateTimeOffset.MinValue;
    private DateTimeOffset _lastEnvironmentCheck = DateTimeOffset.MinValue;
    private DateTimeOffset _nextComputed = DateTimeOffset.MinValue;
    private bool _remote;
    private bool _hdr;
    private bool _nightLight;
    private bool _extendedRange;
    private bool _stopped;

    public FilterEngine(EventideSettings settings)
    {
        Settings = settings;
        _scheduler = new FilterScheduler(settings);
        _tick.Tick += (_, _) => Tick(force: false);
        _fade.Tick += (_, _) => FadeStep();
    }

    /// <summary>Raised after every evaluation that changed something visible (phase, temperature, flags).</summary>
    public event Action? StateChanged;

    public EventideSettings Settings { get; private set; }

    public FilterState State => _state;

    public FilterScheduler Scheduler => _scheduler;

    public int AppliedKelvin => _appliedKelvin;

    public bool PreviewActive => _previewKelvin is not null;

    public void Start()
    {
        _gamma.RefreshDisplays();
        CheckEnvironment();
        _appliedKelvin = ColorTemperature.NeutralKelvin;
        Tick(force: true);
        _tick.Start();
    }

    /// <summary>New settings (menu change or "reload" from the settings window).</summary>
    public void Apply(EventideSettings settings)
    {
        Settings = settings;
        _scheduler = new FilterScheduler(settings);
        Tick(force: true);
    }

    public void SetPreview(int kelvin)
    {
        _previewKelvin = ColorTemperature.Clamp(kelvin);
        _previewSince = DateTimeOffset.Now;
        Tick(force: true);
    }

    public void EndPreview()
    {
        if (_previewKelvin is null)
        {
            return;
        }

        _previewKelvin = null;
        Tick(force: true);
    }

    /// <summary>Display configuration changed, resume from sleep, session unlocked: re-read displays and re-apply.</summary>
    public void DisplaysChanged()
    {
        _gamma.RefreshDisplays();
        _gamma.ResetLimits();
        CheckEnvironment();
        _lastApply = DateTimeOffset.MinValue;
        Tick(force: true);
    }

    public TrayStatus Status()
    {
        var now = DateTimeOffset.Now;
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        var sunToday = _scheduler.SunTimesOf(today);
        var sunTomorrow = _scheduler.SunTimesOf(today.AddDays(1));
        return new TrayStatus
        {
            Enabled = Settings.Enabled,
            Mode = Settings.Mode,
            Phase = _state.Phase,
            AppliedKelvin = _appliedKelvin,
            TargetKelvin = _state.TargetKelvin,
            NightKelvin = Settings.NightKelvin,
            NextChange = _state.NextChange,
            PausedUntil = _state.PausedUntil,
            Sunset = sunToday.Sunset,
            Sunrise = sunTomorrow.Sunrise,
            LocationApproximate = _scheduler.LocationApproximate,
            GammaLimited = _lastResult.Limited,
            LimitKelvin = _lastResult.Limited ? _lastResult.AppliedKelvin : null,
            GammaUnsupported = _lastResult.Unsupported,
            ExtendedGammaRange = _extendedRange,
            HdrActive = _hdr,
            NightLightActive = _nightLight,
            RemoteSession = _remote,
            PreviewActive = PreviewActive,
            DisplayCount = _gamma.DisplayCount,
        };
    }

    /// <summary>
    /// Sign-out announced: put the original ramps back but keep running – if the user cancels the sign-out,
    /// the next tick applies the filter again.
    /// </summary>
    public void RestoreForSessionEnd()
    {
        try
        {
            _gamma.Restore();
            _requestedKelvin = ColorTemperature.NeutralKelvin;
            _appliedKelvin = ColorTemperature.NeutralKelvin;
        }
        catch (Exception)
        {
            // best effort
        }
    }

    /// <summary>Puts the original ramps back (exit, sign-out, crash handler). Safe to call several times.</summary>
    public void Stop()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        _tick.Stop();
        _fade.Stop();
        try
        {
            _gamma.Restore();
        }
        catch (Exception)
        {
            // best effort on shutdown
        }
    }

    public void Dispose()
    {
        Stop();
        _tick.Dispose();
        _fade.Dispose();
    }

    private void Tick(bool force)
    {
        if (_stopped)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        if (now - _lastEnvironmentCheck > EnvironmentInterval)
        {
            CheckEnvironment();
        }

        if (_previewKelvin is not null && now - _previewSince > PreviewTimeout)
        {
            _previewKelvin = null; // settings window vanished without ending the preview
        }

        // A pause that ran out: clear it in the file as well, so the menu shows the right state.
        if (Settings.PausedUntil is { } until && until <= now)
        {
            Settings.PausedUntil = null;
        }

        var previous = _state;
        var needNext = force || now - _nextComputed > TimeSpan.FromMinutes(1) || previous.NextChange is not { } n || n <= now;
        _state = _scheduler.Evaluate(now, includeNextChange: needNext);
        if (needNext)
        {
            _nextComputed = now;
        }
        else if (_state.Phase == previous.Phase)
        {
            _state = _state with { NextChange = previous.NextChange };
        }
        var target = _previewKelvin ?? _state.TargetKelvin;

        if (_remote)
        {
            if (previous.Phase != _state.Phase || force)
            {
                StateChanged?.Invoke();
            }

            return;
        }

        // Periodic re-apply only while a tint is wanted: at 6500 K other colour tools keep their ramps.
        var reapplyDue = target != ColorTemperature.NeutralKelvin && now - _lastApply > ReapplyInterval;
        if (_fade.Enabled)
        {
            // fading: just retarget
            _fadeTo = target;
        }
        else if (Math.Abs(target - _requestedKelvin) > 150 && force)
        {
            BeginFade(target);
        }
        else if (target != _requestedKelvin || reapplyDue || force)
        {
            ApplyNow(target);
        }

        if (force || previous.Phase != _state.Phase || previous.TargetKelvin != _state.TargetKelvin || previous.NextChange != _state.NextChange)
        {
            StateChanged?.Invoke();
        }
    }

    private void BeginFade(int target)
    {
        _fadeFrom = _requestedKelvin;
        _fadeTo = target;
        _fadeStep = 0;
        _fade.Start();
    }

    private void FadeStep()
    {
        _fadeStep++;
        var t = Math.Min(1.0, _fadeStep / (double)FadeSteps);
        var eased = t * t * (3 - (2 * t));
        var kelvin = ColorTemperature.Interpolate(_fadeFrom, _fadeTo, eased);
        ApplyNow(kelvin);
        if (t >= 1)
        {
            _fade.Stop();
            ApplyNow(_fadeTo);
            StateChanged?.Invoke();
        }
    }

    private void ApplyNow(int kelvin)
    {
        if (_stopped)
        {
            return;
        }

        _requestedKelvin = kelvin;
        _lastResult = _gamma.Apply(kelvin);
        _appliedKelvin = _lastResult.AppliedKelvin;
        _lastApply = DateTimeOffset.Now;
    }

    private void CheckEnvironment()
    {
        _lastEnvironmentCheck = DateTimeOffset.Now;
        _remote = SystemState.IsRemoteSession();
        _hdr = SystemState.IsAnyHdrActive();
        _nightLight = SystemState.IsNightLightOn();
        _extendedRange = GammaRange.IsExtended();
    }
}
