using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eventide.Core.Color;
using Eventide.Core.Ipc;
using Eventide.Core.Localization;
using Eventide.Core.Scheduling;
using Eventide.Core.Settings;
using Eventide.Core.Solar;
using Eventide.Platform.Windows;

namespace Eventide.App.SettingsUi;

public enum SettingsPage
{
    Status,
    Schedule,
    Filter,
    General,
    About,
}

/// <summary>A hint on the status page (warning or information).</summary>
public sealed record Notice(string Text, bool IsWarning);

/// <summary>
/// View model of the settings window.
/// <list type="bullet">
/// <item>Schedule and filter values are edited and written with <b>Save</b> (read-modify-write, the tray owns on/off and pause).</item>
/// <item>Autostart, language and the gamma range apply immediately.</item>
/// <item>The status page polls the tray every 2 s over the named pipe.</item>
/// </list>
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _store = new();
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(2) };
    private EventideSettings _saved;
    private bool _loading;
    private bool _previewSent;
    private int? _pendingPreview;
    private bool _previewQueued;
    private bool _previewSending;
    private TrayStatus? _status;

    public SettingsViewModel()
    {
        _saved = _store.Load();
        Loc.SetLanguage(_saved.Language);
        LoadFrom(_saved);
#pragma warning disable MVVMTK0034 // initial values without change handlers (no registry / file writes at start)
        _languageIndex = _saved.Language switch { "en" => 1, "de" => 2, _ => 0 };
        _autostart = Platform.Windows.Autostart.IsEnabled();
        InitPause(_saved.PauseMinutes);
#pragma warning restore MVVMTK0034
        RefreshGammaState();

        Loc.Changed += RefreshTexts;
        _poll.Tick += async (_, _) =>
        {
            // Keep the preview alive (the tray drops a preview after 5 minutes without update) and repair lost messages.
            if (Page == SettingsPage.Filter && _previewSent)
            {
                QueuePreview(NightKelvin);
            }

            await RefreshStatusAsync();
        };
        _poll.Start();
        _ = RefreshStatusAsync();
    }

    // ================================================================== Navigation

    [ObservableProperty]
    private SettingsPage _page = SettingsPage.Status;

    public bool IsStatusPage { get => Page == SettingsPage.Status; set => Go(value, SettingsPage.Status); }

    public bool IsSchedulePage { get => Page == SettingsPage.Schedule; set => Go(value, SettingsPage.Schedule); }

    public bool IsFilterPage { get => Page == SettingsPage.Filter; set => Go(value, SettingsPage.Filter); }

    public bool IsGeneralPage { get => Page == SettingsPage.General; set => Go(value, SettingsPage.General); }

    public bool IsAboutPage { get => Page == SettingsPage.About; set => Go(value, SettingsPage.About); }

    public string PageTitle => Loc.T(Page switch
    {
        SettingsPage.Schedule => "NavSchedule",
        SettingsPage.Filter => "NavFilter",
        SettingsPage.General => "NavGeneral",
        SettingsPage.About => "NavAbout",
        _ => "NavStatus",
    });

    public string PageSubtitle => Loc.T(Page switch
    {
        SettingsPage.Schedule => "ScheduleSub",
        SettingsPage.Filter => "FilterSub",
        SettingsPage.General => "GeneralSub",
        SettingsPage.About => "AboutSub",
        _ => "StatusSub",
    });

    /// <summary>Save / Discard only make sense on the pages with edited values.</summary>
    public bool ShowFooter => Page is SettingsPage.Schedule or SettingsPage.Filter;

    private void Go(bool value, SettingsPage page)
    {
        if (value)
        {
            Page = page;
        }
    }

    partial void OnPageChanged(SettingsPage value)
    {
        OnPropertyChanged(nameof(IsStatusPage));
        OnPropertyChanged(nameof(IsSchedulePage));
        OnPropertyChanged(nameof(IsFilterPage));
        OnPropertyChanged(nameof(IsGeneralPage));
        OnPropertyChanged(nameof(IsAboutPage));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(ShowFooter));

        // Live preview only while the filter page is open.
        if (value == SettingsPage.Filter)
        {
            SendPreview();
        }
        else
        {
            EndPreview();
        }

        if (value == SettingsPage.General)
        {
            RefreshGammaState();
            Autostart = Platform.Windows.Autostart.IsEnabled();
        }
    }

    // ================================================================== Editable values (Save / Discard)

    [ObservableProperty]
    private FilterMode _mode;

    [ObservableProperty]
    private IntensityPreset _intensity;

    [ObservableProperty]
    private double _customKelvin;

    [ObservableProperty]
    private string _latitudeText = string.Empty;

    [ObservableProperty]
    private string _longitudeText = string.Empty;

    [ObservableProperty]
    private TimeSpan? _scheduleStart;

    [ObservableProperty]
    private TimeSpan? _scheduleEnd;

    [ObservableProperty]
    private double _transitionMinutes;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string _locationMessage = string.Empty;

    [ObservableProperty]
    private bool _locationBusy;

    [ObservableProperty]
    private bool _savedRecently;

    public bool IsModeAuto { get => Mode == FilterMode.Auto; set => SetMode(value, FilterMode.Auto); }

    public bool IsModeSchedule { get => Mode == FilterMode.Schedule; set => SetMode(value, FilterMode.Schedule); }

    public bool IsModeAlways { get => Mode == FilterMode.AlwaysOn; set => SetMode(value, FilterMode.AlwaysOn); }

    public bool IsSoft { get => Intensity == IntensityPreset.Soft; set => SetIntensity(value, IntensityPreset.Soft); }

    public bool IsMedium { get => Intensity == IntensityPreset.Medium; set => SetIntensity(value, IntensityPreset.Medium); }

    public bool IsWarm { get => Intensity == IntensityPreset.Warm; set => SetIntensity(value, IntensityPreset.Warm); }

    public bool IsStrong { get => Intensity == IntensityPreset.Strong; set => SetIntensity(value, IntensityPreset.Strong); }

    public bool IsExtreme { get => Intensity == IntensityPreset.Extreme; set => SetIntensity(value, IntensityPreset.Extreme); }

    public bool IsCustom { get => Intensity == IntensityPreset.Custom; set => SetIntensity(value, IntensityPreset.Custom); }

    public bool ShowLocation => Mode == FilterMode.Auto;

    public bool ShowTimes => Mode == FilterMode.Schedule;

    public bool ShowTransition => Mode != FilterMode.AlwaysOn;

    public int NightKelvin => Core.Color.Intensity.KelvinOf(Intensity, (int)Math.Round(CustomKelvin));

    public string NightKelvinText => $"{NightKelvin} K";

    public string TransitionText => Loc.F("MinutesFmt", (int)TransitionMinutes);

    /// <summary>The night colour as it roughly looks on a white area.</summary>
    public IBrush PreviewBrush
    {
        get
        {
            var (r, g, b) = ColorTemperature.PreviewColor(NightKelvin);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
    }

    public bool ShowLimitHint => _status is { GammaLimited: true, LimitKelvin: not null } && NightKelvin < _status.LimitKelvin;

    public string LimitHint => _status?.LimitKelvin is { } limit ? Loc.F("LimitedHintFmt", limit) : string.Empty;

    public string SunPreview
    {
        get
        {
            var settings = BuildSettings(_saved.Clone());
            var scheduler = new FilterScheduler(settings);
            var today = DateOnly.FromDateTime(DateTime.Now);
            var sun = scheduler.SunTimesOf(today);
            if (sun.Kind == SunDayKind.PolarNight)
            {
                return Loc.T("SunPolarNight");
            }

            if (sun.Kind == SunDayKind.MidnightSun)
            {
                return Loc.T("SunMidnight");
            }

            var tomorrow = scheduler.SunTimesOf(today.AddDays(1));
            var text = Loc.F("SunPreviewFmt", Loc.Time(sun.Sunset!.Value), tomorrow.Sunrise is { } r ? Loc.Time(r) : "–");
            return scheduler.LocationApproximate ? text + " · ≈" : text;
        }
    }

    private void SetMode(bool value, FilterMode mode)
    {
        if (value)
        {
            Mode = mode;
        }
    }

    private void SetIntensity(bool value, IntensityPreset preset)
    {
        if (value)
        {
            Intensity = preset;
        }
    }

    partial void OnModeChanged(FilterMode value)
    {
        OnPropertyChanged(nameof(IsModeAuto));
        OnPropertyChanged(nameof(IsModeSchedule));
        OnPropertyChanged(nameof(IsModeAlways));
        OnPropertyChanged(nameof(ShowLocation));
        OnPropertyChanged(nameof(ShowTimes));
        OnPropertyChanged(nameof(ShowTransition));
        Changed();
    }

    partial void OnIntensityChanged(IntensityPreset value)
    {
        OnPropertyChanged(nameof(IsSoft));
        OnPropertyChanged(nameof(IsMedium));
        OnPropertyChanged(nameof(IsWarm));
        OnPropertyChanged(nameof(IsStrong));
        OnPropertyChanged(nameof(IsExtreme));
        OnPropertyChanged(nameof(IsCustom));
        KelvinChanged();
    }

    partial void OnCustomKelvinChanged(double value)
    {
        if (!_loading && Intensity != IntensityPreset.Custom)
        {
            Intensity = IntensityPreset.Custom; // moving the slider selects "Custom"
            return;
        }

        KelvinChanged();
    }

    partial void OnLatitudeTextChanged(string value) => Changed();

    partial void OnLongitudeTextChanged(string value) => Changed();

    partial void OnScheduleStartChanged(TimeSpan? value) => Changed();

    partial void OnScheduleEndChanged(TimeSpan? value) => Changed();

    partial void OnTransitionMinutesChanged(double value)
    {
        OnPropertyChanged(nameof(TransitionText));
        Changed();
    }

    private void KelvinChanged()
    {
        OnPropertyChanged(nameof(NightKelvin));
        OnPropertyChanged(nameof(NightKelvinText));
        OnPropertyChanged(nameof(PreviewBrush));
        OnPropertyChanged(nameof(ShowLimitHint));
        Changed();
        if (Page == SettingsPage.Filter)
        {
            SendPreview();
        }
    }

    private void Changed()
    {
        if (_loading)
        {
            return;
        }

        IsDirty = !SameAs(_saved);
        SavedRecently = false;
        OnPropertyChanged(nameof(SunPreview));
    }

    private void LoadFrom(EventideSettings s)
    {
        _loading = true;
        try
        {
            Mode = s.Mode;
            Intensity = s.Intensity;
            CustomKelvin = s.CustomKelvin;
            LatitudeText = s.Latitude?.ToString("0.##", Loc.Culture) ?? string.Empty;
            LongitudeText = s.Longitude?.ToString("0.##", Loc.Culture) ?? string.Empty;
            ScheduleStart = s.ScheduleStart.ToTimeSpan();
            ScheduleEnd = s.ScheduleEnd.ToTimeSpan();
            TransitionMinutes = s.TransitionMinutes;
            SetLocationStatus(string.Empty, LocationStatus.None);
        }
        finally
        {
            _loading = false;
        }

        IsDirty = false;
        KelvinChangedSilently();
        OnPropertyChanged(nameof(SunPreview));
        OnPropertyChanged(nameof(TransitionText));
    }

    private void KelvinChangedSilently()
    {
        OnPropertyChanged(nameof(NightKelvin));
        OnPropertyChanged(nameof(NightKelvinText));
        OnPropertyChanged(nameof(PreviewBrush));
        OnPropertyChanged(nameof(ShowLimitHint));
    }

    /// <summary>Writes the edited values into <paramref name="target"/> (invalid coordinates are left unchanged).</summary>
    private EventideSettings BuildSettings(EventideSettings target)
    {
        target.Mode = Mode;
        target.Intensity = Intensity;
        target.CustomKelvin = (int)Math.Round(CustomKelvin);
        target.TransitionMinutes = (int)Math.Round(TransitionMinutes);
        if (ScheduleStart is { } start)
        {
            target.ScheduleStart = TimeOnly.FromTimeSpan(start);
        }

        if (ScheduleEnd is { } end)
        {
            target.ScheduleEnd = TimeOnly.FromTimeSpan(end);
        }

        if (TryParseLocation(out var lat, out var lon))
        {
            target.Latitude = lat;
            target.Longitude = lon;
        }

        return target;
    }

    private bool SameAs(EventideSettings s)
    {
        var edited = BuildSettings(s.Clone());
        return edited.Mode == s.Mode
               && edited.Intensity == s.Intensity
               && edited.CustomKelvin == s.CustomKelvin
               && edited.TransitionMinutes == s.TransitionMinutes
               && edited.ScheduleStart == s.ScheduleStart
               && edited.ScheduleEnd == s.ScheduleEnd
               && edited.Latitude == s.Latitude
               && edited.Longitude == s.Longitude
               && LocationTextMatches(s);
    }

    private bool LocationTextMatches(EventideSettings s) =>
        TryParseLocation(out var lat, out var lon) && lat == s.Latitude && lon == s.Longitude;

    /// <summary>Both empty → null/null (approximation); both valid → values; anything else → false.</summary>
    private bool TryParseLocation(out double? latitude, out double? longitude)
    {
        latitude = null;
        longitude = null;
        if (string.IsNullOrWhiteSpace(LatitudeText) && string.IsNullOrWhiteSpace(LongitudeText))
        {
            return true;
        }

        if (ParseDegree(LatitudeText) is { } lat && ParseDegree(LongitudeText) is { } lon && SolarCalculator.IsValid(lat, lon))
        {
            latitude = Math.Round(lat, 2);
            longitude = Math.Round(lon, 2);
            return true;
        }

        return false;
    }

    private static double? ParseDegree(string text)
    {
        var normalized = text.Trim().Replace(',', '.').Replace('−', '-');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!TryParseLocation(out _, out _))
        {
            SetLocationStatus(Loc.T("LocInvalid"), LocationStatus.Error);
            Page = SettingsPage.Schedule;
            return;
        }

        _saved = _store.Update(s => BuildSettings(s));
        LoadFrom(_saved);
        SavedRecently = true;
        await TrayClient.SendAsync(new TrayRequest(TrayCommand.Reload));
        if (Page == SettingsPage.Filter)
        {
            SendPreview();
        }

        await RefreshStatusAsync();
    }

    [RelayCommand]
    private void Discard()
    {
        _saved = _store.Load();
        LoadFrom(_saved);
        if (Page == SettingsPage.Filter)
        {
            SendPreview();
        }
    }

    // ================================================================== Location

    [RelayCommand]
    private async Task UseWindowsLocationAsync()
    {
        LocationBusy = true;
        SetLocationStatus(Loc.T("LocBusy"), LocationStatus.Busy);
        try
        {
            var access = await global::Windows.Devices.Geolocation.Geolocator.RequestAccessAsync();
            if (access != global::Windows.Devices.Geolocation.GeolocationAccessStatus.Allowed)
            {
                SetLocationStatus(Loc.T("LocDenied"), LocationStatus.Error);
                return;
            }

            var locator = new global::Windows.Devices.Geolocation.Geolocator
            {
                DesiredAccuracy = global::Windows.Devices.Geolocation.PositionAccuracy.Default,
            };
            var position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(15));
            var point = position.Coordinate.Point.Position;

            // Rounded to two decimals (≈ 1 km) – enough for sun times, nothing more is stored.
            var lat = Math.Round(point.Latitude, 2);
            var lon = Math.Round(point.Longitude, 2);
            LatitudeText = lat.ToString("0.##", Loc.Culture);
            LongitudeText = lon.ToString("0.##", Loc.Culture);
            SetLocationStatus(Loc.F("LocOkFmt", LatitudeText, LongitudeText), LocationStatus.Ok);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or TimeoutException
                                   or System.Runtime.InteropServices.COMException or OperationCanceledException)
        {
            SetLocationStatus(Loc.T("LocFailed"), LocationStatus.Error);
        }
        finally
        {
            LocationBusy = false;
        }
    }

    /// <summary>Status pill next to "Use Windows location": green = set, red = failed / invalid, grey = requesting.</summary>
    public enum LocationStatus
    {
        None,
        Busy,
        Ok,
        Error,
    }

    [ObservableProperty]
    private LocationStatus _locationState;

    public bool HasLocationMessage => LocationState != LocationStatus.None && !string.IsNullOrEmpty(LocationMessage);

    public bool LocationOk => LocationState == LocationStatus.Ok;

    public bool LocationError => LocationState == LocationStatus.Error;

    public bool LocationPending => LocationState == LocationStatus.Busy;

    partial void OnLocationStateChanged(LocationStatus value)
    {
        OnPropertyChanged(nameof(HasLocationMessage));
        OnPropertyChanged(nameof(LocationOk));
        OnPropertyChanged(nameof(LocationError));
        OnPropertyChanged(nameof(LocationPending));
    }

    partial void OnLocationMessageChanged(string value) => OnPropertyChanged(nameof(HasLocationMessage));

    private void SetLocationStatus(string message, LocationStatus state)
    {
        LocationMessage = message;
        LocationState = state;
    }

    // ================================================================== Live preview

    private void SendPreview()
    {
        _previewSent = true;
        QueuePreview(NightKelvin);
    }

    public void EndPreview()
    {
        if (!_previewSent)
        {
            return;
        }

        _previewSent = false;
        QueuePreview(null);
    }

    /// <summary>
    /// Preview messages go out one at a time, newest wins (slider drags produce many changes; parallel pipe
    /// connections could arrive out of order or time out). null = end of preview.
    /// </summary>
    private void QueuePreview(int? kelvin)
    {
        _pendingPreview = kelvin;
        _previewQueued = true;
        if (!_previewSending)
        {
            _ = PumpPreviewAsync();
        }
    }

    private async Task PumpPreviewAsync()
    {
        _previewSending = true;
        try
        {
            while (_previewQueued)
            {
                _previewQueued = false;
                var request = _pendingPreview is { } k
                    ? new TrayRequest(TrayCommand.Preview, k)
                    : new TrayRequest(TrayCommand.PreviewEnd);
                await TrayClient.SendAsync(request, 1500);
            }
        }
        finally
        {
            _previewSending = false;
        }
    }

    /// <summary>Window closes: end the preview synchronously (the process ends right after).</summary>
    public void Shutdown()
    {
        _poll.Stop();
        if (_previewSent)
        {
            _previewSent = false;
            TrayClient.Send(new TrayRequest(TrayCommand.PreviewEnd), 1500);
        }
    }

    // ================================================================== Status page

    [ObservableProperty]
    private bool _trayRunning = true;

    [ObservableProperty]
    private string _statusHeadline = string.Empty;

    [ObservableProperty]
    private string _statusDetail = string.Empty;

    [ObservableProperty]
    private bool _filterOn;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _currentKelvinText = "–";

    [ObservableProperty]
    private string _sunsetText = "–";

    [ObservableProperty]
    private string _sunriseText = "–";

    [ObservableProperty]
    private string _nextChangeText = "–";

    [ObservableProperty]
    private string _displaysText = "–";

    [ObservableProperty]
    private IBrush _currentBrush = Brushes.White;

    public ObservableCollection<Notice> Notices { get; } = [];

    public string ToggleText => Loc.T(FilterOn ? "BtnTurnOff" : "BtnTurnOn");

    public bool CanPause => TrayRunning && FilterOn && !IsPaused;

    partial void OnFilterOnChanged(bool value)
    {
        OnPropertyChanged(nameof(ToggleText));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(PauseHintText));
    }

    partial void OnIsPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(PauseHintText));
    }

    partial void OnTrayRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(PauseHintText));
    }

    private static Notice Warn(string text) => new(Loc.T("NoticeWarnPrefix") + text, true);

    private static Notice Info(string text) => new(Loc.T("NoticeInfoPrefix") + text, false);

    private async Task RefreshStatusAsync()
    {
        var response = await TrayClient.SendAsync(new TrayRequest(TrayCommand.Status));
        ApplyStatus(response?.Status);
    }

    private void ApplyStatus(TrayStatus? status)
    {
        _status = status;
        TrayRunning = status is not null;
        OnPropertyChanged(nameof(ShowLimitHint));
        OnPropertyChanged(nameof(LimitHint));

        if (status is null)
        {
            StatusHeadline = Loc.T("StOff");
            StatusDetail = Loc.T("TrayNotRunning");
            FilterOn = false;
            IsPaused = false;
            PausedTime = string.Empty;
            CurrentKelvinText = "–";
            NextChangeText = "–";
            DisplaysText = "–";
            CurrentBrush = Brushes.White;
            var sun = new FilterScheduler(_saved).SunTimesOf(DateOnly.FromDateTime(DateTime.Now));
            SunsetText = sun.Sunset is { } ss ? Loc.Time(ss) : "–";
            SunriseText = new FilterScheduler(_saved).SunTimesOf(DateOnly.FromDateTime(DateTime.Now).AddDays(1)).Sunrise is { } sr ? Loc.Time(sr) : "–";
            Notices.Clear();
            return;
        }

        var now = DateTimeOffset.Now;
        IsPaused = status.Phase == FilterPhase.Paused;
        PausedTime = IsPaused ? Loc.When(status.PausedUntil ?? now, now) : string.Empty;
        FilterOn = status.Enabled && !IsPaused;
        StatusHeadline = status.RemoteSession ? Loc.T("StRemote") : status.Phase switch
        {
            FilterPhase.Off => Loc.T("StOff"),
            FilterPhase.Paused => Loc.F("StPausedFmt", Loc.When(status.PausedUntil ?? now, now)),
            FilterPhase.Day => Loc.T("StDay"),
            FilterPhase.Dusk => Loc.T("StDusk"),
            FilterPhase.Dawn => Loc.T("StDawn"),
            _ when status.Mode == FilterMode.AlwaysOn => Loc.T("StAlways"),
            _ => Loc.T("StNight"),
        };
        StatusDetail = status.PreviewActive
            ? $"{Loc.T("StPreview")} · {status.AppliedKelvin} K"
            : status.NextChange is { } next && status.Phase is not (FilterPhase.Off or FilterPhase.Paused)
                ? $"{status.TargetKelvin} K · {Loc.F("StNextFmt", Loc.When(next, now))}"
                : $"{status.TargetKelvin} K";

        CurrentKelvinText = $"{status.AppliedKelvin} K";
        var (r, g, b) = ColorTemperature.PreviewColor(status.AppliedKelvin);
        CurrentBrush = new SolidColorBrush(Color.FromRgb(r, g, b));
        SunsetText = status.Sunset is { } sunset ? Loc.Time(sunset) : "–";
        SunriseText = status.Sunrise is { } sunrise ? Loc.Time(sunrise) : "–";
        NextChangeText = status.NextChange is { } nc ? Loc.When(nc, now) : "–";
        DisplaysText = status.DisplayCount.ToString(Loc.Culture);

        Notices.Clear();
        if (status.RemoteSession)
        {
            Notices.Add(Warn(Loc.T("NoticeRemote")));
        }
        else if (status.GammaUnsupported)
        {
            Notices.Add(Warn(Loc.T("NoticeUnsupported")));
        }

        if (status.GammaLimited && status.LimitKelvin is { } limit)
        {
            Notices.Add(Warn(Loc.F("NoticeLimitedFmt", limit)));
        }

        if (status.HdrActive)
        {
            Notices.Add(Warn(Loc.T("NoticeHdr")));
        }

        if (status.NightLightActive)
        {
            Notices.Add(Warn(Loc.T("NoticeNightLight")));
        }

        if (status.LocationApproximate && status.Mode == FilterMode.Auto)
        {
            Notices.Add(Info(Loc.T("NoticeApproxLocation")));
        }
    }

    [RelayCommand]
    private async Task ToggleFilterAsync() => ApplyStatus((await TrayClient.SendAsync(new TrayRequest(TrayCommand.Toggle)))?.Status);

    // ================================================================== Pause card

    /// <summary>Selected / default pause in minutes (<see cref="EventideSettings.PauseUntilMorning"/> = until tomorrow morning).</summary>
    [ObservableProperty]
    private int _pauseMinutes = 60;

    [ObservableProperty]
    private bool _pauseCustomSelected;

    [ObservableProperty]
    private decimal? _customPauseAmount = 45;

    /// <summary>0 = minutes, 1 = hours.</summary>
    [ObservableProperty]
    private int _customPauseUnitIndex;

    /// <summary>End of the pause ("22:15", or "Mon 07:00" on another day) – shown in a pill.</summary>
    [ObservableProperty]
    private string _pausedTime = string.Empty;

    private bool _pauseLoading;

    public bool IsPause15 { get => IsPreset(15); set => PickPause(value, 15); }

    public bool IsPause30 { get => IsPreset(30); set => PickPause(value, 30); }

    public bool IsPause60 { get => IsPreset(60); set => PickPause(value, 60); }

    public bool IsPause120 { get => IsPreset(120); set => PickPause(value, 120); }

    public bool IsPause240 { get => IsPreset(240); set => PickPause(value, 240); }

    public bool IsPauseMorning { get => IsPreset(EventideSettings.PauseUntilMorning); set => PickPause(value, EventideSettings.PauseUntilMorning); }

    public bool IsPauseCustom
    {
        get => PauseCustomSelected;
        set
        {
            if (value)
            {
                PauseCustomSelected = true;
                PauseMinutes = CustomPauseMinutes();
            }
        }
    }

    public string Pause15Text => Loc.Duration(15);

    public string Pause30Text => Loc.Duration(30);

    public string Pause60Text => Loc.Duration(60);

    public string Pause120Text => Loc.Duration(120);

    public string Pause240Text => Loc.Duration(240);

    public string PauseHintText => Loc.T(TrayRunning && FilterOn || IsPaused ? "PauseHint" : "PauseOnlyWhenOn");

    private bool IsPreset(int minutes) => !PauseCustomSelected && PauseMinutes == minutes;

    private void PickPause(bool value, int minutes)
    {
        if (!value)
        {
            return;
        }

        PauseCustomSelected = false;
        PauseMinutes = minutes;
    }

    private int CustomPauseMinutes()
    {
        var amount = (double)(CustomPauseAmount ?? 1);
        var minutes = CustomPauseUnitIndex == 1 ? amount * 60 : amount;
        return Math.Clamp((int)Math.Round(minutes), 1, 1440);
    }

    /// <summary>Start values from the settings file (no write-back).</summary>
    private void InitPause(int minutes)
    {
        _pauseLoading = true;
        try
        {
            PauseMinutes = minutes;
            var isPreset = minutes == EventideSettings.PauseUntilMorning || EventideSettings.PausePresets.Contains(minutes);
            PauseCustomSelected = !isPreset;
            if (!isPreset)
            {
                CustomPauseUnitIndex = minutes % 60 == 0 ? 1 : 0;
                CustomPauseAmount = minutes % 60 == 0 ? minutes / 60 : minutes;
            }
        }
        finally
        {
            _pauseLoading = false;
        }

        NotifyPauseChoice();
    }

    partial void OnPauseMinutesChanged(int value)
    {
        NotifyPauseChoice();
        if (_pauseLoading)
        {
            return;
        }

        // The chosen duration becomes the default – also for the tray menu.
        _saved = _store.Update(s => s.PauseMinutes = value);
        _ = TrayClient.SendAsync(new TrayRequest(TrayCommand.Reload));
    }

    partial void OnPauseCustomSelectedChanged(bool value) => NotifyPauseChoice();

    partial void OnCustomPauseAmountChanged(decimal? value) => CustomPauseChanged();

    partial void OnCustomPauseUnitIndexChanged(int value) => CustomPauseChanged();

    /// <summary>Typing an own duration selects "Custom".</summary>
    private void CustomPauseChanged()
    {
        if (_pauseLoading)
        {
            return;
        }

        PauseCustomSelected = true;
        PauseMinutes = CustomPauseMinutes();
    }

    private void NotifyPauseChoice()
    {
        OnPropertyChanged(nameof(IsPause15));
        OnPropertyChanged(nameof(IsPause30));
        OnPropertyChanged(nameof(IsPause60));
        OnPropertyChanged(nameof(IsPause120));
        OnPropertyChanged(nameof(IsPause240));
        OnPropertyChanged(nameof(IsPauseMorning));
        OnPropertyChanged(nameof(IsPauseCustom));
    }

    [RelayCommand]
    private async Task PauseAsync()
    {
        var request = PauseMinutes == EventideSettings.PauseUntilMorning
            ? new TrayRequest(TrayCommand.PauseUntilMorning)
            : new TrayRequest(TrayCommand.PauseFor, Minutes: PauseMinutes);
        ApplyStatus((await TrayClient.SendAsync(request))?.Status);
    }

    [RelayCommand]
    private async Task ResumeAsync() => ApplyStatus((await TrayClient.SendAsync(new TrayRequest(TrayCommand.Resume)))?.Status);

    [RelayCommand]
    private async Task StartTrayAsync()
    {
        try
        {
            Process.Start(new ProcessStartInfo(ExePath()) { UseShellExecute = false })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return;
        }

        await Task.Delay(1200);
        await RefreshStatusAsync();
        if (Page == SettingsPage.Filter)
        {
            SendPreview();
        }
    }

    // ================================================================== General page

    [ObservableProperty]
    private bool _autostart;

    [ObservableProperty]
    private int _languageIndex;

    [ObservableProperty]
    private bool _gammaExtended;

    [ObservableProperty]
    private bool _gammaOwned;

    [ObservableProperty]
    private string _gammaMessage = string.Empty;

    [ObservableProperty]
    private bool _gammaBusy;

    public string GammaStateText => Loc.T(GammaExtended ? "GammaOn" : "GammaOff");

    public bool CanEnableGamma => !GammaExtended && !GammaBusy;

    public bool CanDisableGamma => GammaExtended && GammaOwned && !GammaBusy;

    public string SettingsFilePath => _store.FilePath;

    public bool IsPortable => SettingsStore.IsPortable;

    partial void OnAutostartChanged(bool value)
    {
        if (Platform.Windows.Autostart.IsEnabled() != value)
        {
            Platform.Windows.Autostart.Set(value);
        }

        // An explicit choice: the tray must not switch it on again at its first start.
        _saved = _store.Update(s => s.AutostartInitialized = true);
    }

    /// <summary>Language list with flags (names stay English: "English", "German").</summary>
    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new LanguageOption("LangAuto", null),
        new LanguageOption("LangEn", "avares://Eventide/Assets/flag-gb.png"),
        new LanguageOption("LangDe", "avares://Eventide/Assets/flag-de.png"),
    ];

    partial void OnLanguageIndexChanged(int value)
    {
        var language = value switch { 1 => "en", 2 => "de", _ => "auto" };
        _saved = _store.Update(s => s.Language = language);
        _saved.Language = language;
        Loc.SetLanguage(language);
        _ = TrayClient.SendAsync(new TrayRequest(TrayCommand.Reload));
    }

    partial void OnGammaExtendedChanged(bool value) => GammaFlagsChanged();

    partial void OnGammaOwnedChanged(bool value) => GammaFlagsChanged();

    partial void OnGammaBusyChanged(bool value) => GammaFlagsChanged();

    private void GammaFlagsChanged()
    {
        OnPropertyChanged(nameof(GammaStateText));
        OnPropertyChanged(nameof(CanEnableGamma));
        OnPropertyChanged(nameof(CanDisableGamma));
    }

    private void RefreshGammaState()
    {
        GammaExtended = GammaRange.IsExtended();
        GammaOwned = GammaRange.IsSetByEventide();
        if (GammaExtended && !GammaOwned && string.IsNullOrEmpty(GammaMessage))
        {
            GammaMessage = Loc.T("GammaNotOwned");
        }
    }

    [RelayCommand]
    private Task EnableGammaAsync() => RunElevatedAsync("--enable-gamma-range");

    [RelayCommand]
    private Task DisableGammaAsync() => RunElevatedAsync("--disable-gamma-range");

    private async Task RunElevatedAsync(string argument)
    {
        GammaBusy = true;
        try
        {
            using var process = Process.Start(new ProcessStartInfo(ExePath(), argument) { UseShellExecute = true, Verb = "runas" });
            if (process is null)
            {
                GammaMessage = Loc.T("GammaFailed");
                return;
            }

            await process.WaitForExitAsync();
            GammaMessage = Loc.T(process.ExitCode == 0 ? "GammaRestartHint" : process.ExitCode == 2 ? "GammaNotOwned" : "GammaFailed");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            GammaMessage = Loc.T("GammaCancelled"); // UAC prompt declined
        }
        catch (System.ComponentModel.Win32Exception)
        {
            GammaMessage = Loc.T("GammaFailed");
        }
        finally
        {
            GammaBusy = false;
            GammaExtended = GammaRange.IsExtended();
            GammaOwned = GammaRange.IsSetByEventide();
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        Directory.CreateDirectory(_store.Directory);
        Open(_store.Directory);
    }

    // ================================================================== About page

    public string VersionText => Loc.F("VersionFmt", Version);

    public static string Version =>
        typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.1";

    [RelayCommand]
    private void OpenLicenses() => Open(Path.Combine(AppContext.BaseDirectory, "licenses"));

    [RelayCommand]
    private void OpenWebsite() => Open("https://sksdesign.de");

    [RelayCommand]
    private void OpenGitHub() => Open("https://github.com/complicatiion/Eventide");

    // ================================================================== Helpers

    private void RefreshTexts()
    {
        foreach (var language in Languages)
        {
            language.Refresh();
        }

        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(ToggleText));
        OnPropertyChanged(nameof(TransitionText));
        OnPropertyChanged(nameof(SunPreview));
        OnPropertyChanged(nameof(LimitHint));
        OnPropertyChanged(nameof(GammaStateText));
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(Pause15Text));
        OnPropertyChanged(nameof(Pause30Text));
        OnPropertyChanged(nameof(Pause60Text));
        OnPropertyChanged(nameof(Pause120Text));
        OnPropertyChanged(nameof(Pause240Text));
        OnPropertyChanged(nameof(PauseHintText));
        ApplyStatus(_status);
    }

    private static string ExePath() => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Eventide.exe");

    private static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // nothing to open
        }
    }
}
