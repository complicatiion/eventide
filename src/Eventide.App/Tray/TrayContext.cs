using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Eventide.Core.Color;
using Eventide.Core.Ipc;
using Eventide.Core.Localization;
using Eventide.Core.Scheduling;
using Eventide.Core.Settings;
using Eventide.Platform.Windows;
using Microsoft.Win32;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace Eventide.App.Tray;

/// <summary>
/// The tray: icon (state indicator), left click = filter on/off, right click = menu with "Settings…" on top.
/// Owns the <see cref="FilterEngine"/> and the pipe server for the settings window.
/// </summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly SettingsStore _store = new();
    private readonly TrayAssets _assets = new();
    private readonly NotifyIcon _notify;
    private readonly ContextMenuStrip _menu;
    private readonly DarkMenuRenderer _renderer = new();
    private readonly FilterEngine _engine;
    private readonly TrayPipeServer _pipe;
    private readonly WinFormsTimer _delayedRefresh = new() { Interval = 1500 };
    private readonly Font _font = new("Segoe UI", 9f);
    private readonly Font _boldFont = new("Segoe UI", 9f, FontStyle.Bold);

    private ToolStripMenuItem _settingsItem = null!;
    private ToolStripMenuItem _statusItem = null!;
    private ToolStripMenuItem _enabledItem = null!;
    private ToolStripMenuItem _modeItem = null!;
    private ToolStripMenuItem _modeAuto = null!;
    private ToolStripMenuItem _modeSchedule = null!;
    private ToolStripMenuItem _modeAlways = null!;
    private ToolStripMenuItem _intensityItem = null!;
    private readonly Dictionary<IntensityPreset, ToolStripMenuItem> _intensityItems = [];
    private ToolStripMenuItem _pauseItem = null!;
    private readonly Dictionary<int, ToolStripMenuItem> _pausePresetItems = [];
    private ToolStripMenuItem _pauseCustom = null!;
    private ToolStripMenuItem _pauseMorning = null!;
    private ToolStripMenuItem _resumeItem = null!;
    private ToolStripMenuItem _autostartItem = null!;
    private ToolStripMenuItem _exitItem = null!;

    private TrayIconState? _iconState;
    private bool _exiting;
    private bool _disposed;

    public TrayContext()
    {
        var firstRun = !File.Exists(_store.FilePath);
        var settings = _store.Load();
        Loc.SetLanguage(settings.Language);

        _menu = BuildMenu();

        // Creating the menu (a Control) installs the Windows Forms synchronisation context.
        var ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _engine = new FilterEngine(settings);
        _engine.StateChanged += UpdateUi;

        _notify = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Visible = false,
        };
        _notify.MouseClick += OnTrayClick;

        _pipe = new TrayPipeServer(ui, Handle);
        _delayedRefresh.Tick += (_, _) =>
        {
            _delayedRefresh.Stop();
            _engine.DisplaysChanged();
        };

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.SessionEnding += OnSessionEnding;
        SystemEvents.SessionEnded += OnSessionEnded;
        Loc.Changed += UpdateUi;

        _engine.Start();
        _pipe.Start();
        UpdateUi();
        _notify.Visible = true;

        // "Start with Windows" is on by default: switched on exactly once (independent of whether a settings file
        // already existed, e.g. from an older version or the settings window). Later the user's choice is kept.
        if (!settings.AutostartInitialized)
        {
            Autostart.Set(true);
            _engine.Apply(_store.Update(s => s.AutostartInitialized = true));
            UpdateUi();
        }

        if (firstRun)
        {
            _notify.ShowBalloonTip(8000, Loc.T("FirstRunTitle"), Loc.T("FirstRunText"), ToolTipIcon.None);
        }
    }

    /// <summary>Crash handler: never leave the screen tinted.</summary>
    public void EmergencyRestore() => _engine.Stop();

    public static void LogError(Exception ex)
    {
        try
        {
            var dir = SettingsStore.DefaultDirectory();
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), $"{DateTimeOffset.Now:O} {ex}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // nothing left to do
        }
    }

    /// <summary>
    /// Runs once: Windows Forms disposes the context itself when the message loop ends (ExitThread), and the host
    /// disposes it again as a safety net – the second call must do nothing.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            SystemEvents.SessionEnding -= OnSessionEnding;
            SystemEvents.SessionEnded -= OnSessionEnded;
            Loc.Changed -= UpdateUi;
            _pipe.Dispose();
            _engine.Dispose();
            _notify.Visible = false;
            _notify.Dispose();
            _menu.Dispose();
            _delayedRefresh.Dispose();
            _assets.Dispose();
            _font.Dispose();
            _boldFont.Dispose();
        }

        base.Dispose(disposing);
    }

    // ------------------------------------------------------------------ Menu

    private ContextMenuStrip BuildMenu()
    {
        var menu = NewDropDown(new ContextMenuStrip());

        _settingsItem = Item(string.Empty, "settings", (_, _) => OpenSettings());
        _settingsItem.Font = _boldFont;
        _settingsItem.Image = _assets.MenuImage("settings", accent: true);

        _statusItem = Item(string.Empty, "night", null);
        _statusItem.Enabled = false;
        _statusItem.Tag = new MenuTag(Muted: true);

        _enabledItem = Item(string.Empty, "power", (_, _) => Toggle());

        _modeAuto = Item(string.Empty, "sunset", (_, _) => ChangeSettings(s => s.Mode = FilterMode.Auto));
        _modeSchedule = Item(string.Empty, "clock", (_, _) => ChangeSettings(s => s.Mode = FilterMode.Schedule));
        _modeAlways = Item(string.Empty, "always", (_, _) => ChangeSettings(s => s.Mode = FilterMode.AlwaysOn));
        _modeItem = Item(string.Empty, "mode", null);
        AddSubItems(_modeItem, _modeAuto, _modeSchedule, _modeAlways);

        _intensityItem = Item(string.Empty, "intensity", null);
        foreach (var preset in Enum.GetValues<IntensityPreset>())
        {
            var p = preset;
            var item = Item(string.Empty, null, (_, _) => ChangeSettings(s => s.Intensity = p));
            _intensityItems[preset] = item;
        }

        AddSubItems(_intensityItem, [.. _intensityItems.Values]);

        // Pause: the default duration (settings window) is marked; an own duration appears as first entry.
        _pauseCustom = Item(string.Empty, "timer", (_, _) => PauseFor(TimeSpan.FromMinutes(_engine.Settings.PauseMinutes)));
        foreach (var minutes in EventideSettings.PausePresets)
        {
            var m = minutes;
            _pausePresetItems[m] = Item(string.Empty, "timer", (_, _) => PauseFor(TimeSpan.FromMinutes(m)));
        }

        _pauseMorning = Item(string.Empty, "night", (_, _) => PauseUntilMorning());
        _pauseItem = Item(string.Empty, "pause", null);
        AddSubItems(_pauseItem, [_pauseCustom, .. _pausePresetItems.Values, _pauseMorning]);
        _resumeItem = Item(string.Empty, "play", (_, _) => Resume());
        _resumeItem.Image = _assets.MenuImage("play", accent: true);

        _autostartItem = Item(string.Empty, "autostart", (_, _) =>
        {
            Autostart.Set(!Autostart.IsEnabled());
            _store.Update(s => s.AutostartInitialized = true);
            UpdateUi();
        });

        _exitItem = Item(string.Empty, "exit", (_, _) => ExitTray());

        menu.Items.AddRange(new ToolStripItem[]
        {
            _settingsItem,
            _statusItem,
            new ToolStripSeparator(),
            _enabledItem,
            _modeItem,
            _intensityItem,
            new ToolStripSeparator(),
            _pauseItem,
            _resumeItem,
            _autostartItem,
            new ToolStripSeparator(),
            _exitItem,
        });

        menu.Opening += (_, _) =>
        {
            ScaleImages(menu);
            UpdateUi();
        };
        return menu;
    }

    private T NewDropDown<T>(T dropDown)
        where T : ToolStripDropDown
    {
        dropDown.Renderer = _renderer;
        dropDown.Font = _font;
        dropDown.Padding = new Padding(2, 4, 2, 4);

        // Image size known before the first layout (and refreshed on every opening for the current DPI).
        ScaleImages(dropDown);
        dropDown.DropShadowEnabled = true;
        if (dropDown is ToolStripDropDownMenu m)
        {
            m.ShowImageMargin = true;
            m.ShowCheckMargin = false;
        }

        dropDown.Opened += (_, _) => SystemState.RoundCorners(dropDown.Handle);
        return dropDown;
    }

    private ToolStripMenuItem Item(string text, string? image, EventHandler? onClick)
    {
        var item = new ToolStripMenuItem(text)
        {
            // Symmetric padding: every row ≈ 30 px at 100 %; icon and text are centred by the renderer.
            Padding = new Padding(2, 4, 2, 4),
            Image = image is null ? null : _assets.MenuImage(image),
        };
        if (onClick is not null)
        {
            item.Click += onClick;
        }

        return item;
    }

    private void AddSubItems(ToolStripMenuItem parent, params ToolStripMenuItem[] children)
    {
        NewDropDown((ToolStripDropDownMenu)parent.DropDown);
        parent.DropDownItems.AddRange(children);
        parent.DropDown.Opening += (_, _) => ScaleImages(parent.DropDown);
    }

    /// <summary>Menu images are 32 px; show them at 16 logical pixels of the current DPI.</summary>
    private static void ScaleImages(ToolStrip strip)
    {
        var px = Math.Max(16, (int)Math.Round(16 * strip.DeviceDpi / 96.0));
        strip.ImageScalingSize = new Size(px, px);
    }

    private void UpdateUi()
    {
        if (_exiting)
        {
            return;
        }

        var settings = _engine.Settings;
        var state = _engine.State;
        var now = DateTimeOffset.Now;
        var paused = settings.IsPaused(now);

        _settingsItem.Text = Loc.T("TrayOpenSettings");

        _statusItem.Text = StatusText(now);
        _statusItem.Image = _assets.MenuImage(state.Phase switch
        {
            FilterPhase.Off => "power",
            FilterPhase.Paused => "pause",
            FilterPhase.Day => "day",
            _ => "night",
        });

        // One button for both directions (same as the left click).
        var active = settings.Enabled && !paused;
        _enabledItem.Text = Loc.T(active ? "TrayDisable" : "TrayEnable");
        _enabledItem.Checked = false;
        _enabledItem.Image = _assets.MenuImage("power", accent: active);

        _modeItem.Text = Loc.T("TrayMode");
        _modeAuto.Text = Loc.T("ModeAuto");
        _modeSchedule.Text = Loc.F("ModeScheduleFmt", Loc.Time(settings.ScheduleStart), Loc.Time(settings.ScheduleEnd));
        _modeAlways.Text = Loc.T("ModeAlways");
        SetCheck(_modeAuto, "sunset", settings.Mode == FilterMode.Auto);
        SetCheck(_modeSchedule, "clock", settings.Mode == FilterMode.Schedule);
        SetCheck(_modeAlways, "always", settings.Mode == FilterMode.AlwaysOn);

        _intensityItem.Text = Loc.T("TrayIntensity");
        foreach (var (preset, item) in _intensityItems)
        {
            item.Text = preset switch
            {
                IntensityPreset.Soft => Loc.T("IntSoft"),
                IntensityPreset.Medium => Loc.T("IntMedium"),
                IntensityPreset.Warm => Loc.T("IntWarm"),
                IntensityPreset.Strong => Loc.T("IntStrong"),
                IntensityPreset.Extreme => Loc.T("IntExtreme"),
                _ => Loc.F("IntCustomFmt", settings.CustomKelvin),
            };

            // Custom only appears when it is the active choice (it is set in the settings window).
            item.Visible = preset != IntensityPreset.Custom || settings.Intensity == IntensityPreset.Custom;
            var selected = settings.Intensity == preset;
            item.Checked = selected;
            item.Image = selected ? _assets.MenuImage("check", accent: true) : null;
        }

        _pauseItem.Text = Loc.T("TrayPause");
        var defaultPause = settings.PauseMinutes;
        var suffix = Loc.T("DefaultSuffix");
        foreach (var (minutes, item) in _pausePresetItems)
        {
            var isDefault = minutes == defaultPause;
            item.Text = Loc.Duration(minutes) + (isDefault ? suffix : string.Empty);
            item.Font = isDefault ? _boldFont : _font;
        }

        var customDefault = defaultPause != EventideSettings.PauseUntilMorning && !_pausePresetItems.ContainsKey(defaultPause);
        _pauseCustom.Visible = customDefault;
        if (customDefault)
        {
            _pauseCustom.Text = Loc.Duration(defaultPause) + suffix;
            _pauseCustom.Font = _boldFont;
        }

        var morningDefault = defaultPause == EventideSettings.PauseUntilMorning;
        _pauseMorning.Text = Loc.T("PauseMorning") + (morningDefault ? suffix : string.Empty);
        _pauseMorning.Font = morningDefault ? _boldFont : _font;
        _pauseItem.Visible = settings.Enabled && !paused;
        _resumeItem.Visible = paused;
        if (paused)
        {
            _resumeItem.Text = Loc.F("ResumeFmt", Loc.When(settings.PausedUntil!.Value, now));
        }

        SetCheck(_autostartItem, "autostart", Autostart.IsEnabled());
        _autostartItem.Text = Loc.T("TrayAutostart");
        _exitItem.Text = Loc.T("TrayExit");

        var iconState = !settings.Enabled ? TrayIconState.Off : paused ? TrayIconState.Paused : TrayIconState.On;
        if (_iconState != iconState)
        {
            _notify.Icon = _assets.TrayIcon(iconState);
            _iconState = iconState;
        }

        var tooltip = Loc.F("TooltipFmt", StatusText(now));
        _notify.Text = tooltip.Length > 63 ? tooltip[..62] + "…" : tooltip;
    }

    private void SetCheck(ToolStripMenuItem item, string image, bool isChecked)
    {
        item.Checked = isChecked;
        item.Image = _assets.MenuImage(image, accent: isChecked);
    }

    private string StatusText(DateTimeOffset now)
    {
        var status = _engine.Status();
        var state = _engine.State;
        if (status.RemoteSession)
        {
            return Loc.T("StRemote");
        }

        if (status.PreviewActive)
        {
            return $"{Loc.T("StPreview")} · {_engine.AppliedKelvin} K";
        }

        var next = state.NextChange is { } n ? " · " + Loc.F("StNextFmt", Loc.When(n, now)) : string.Empty;
        return state.Phase switch
        {
            FilterPhase.Off => Loc.T("StOff"),
            FilterPhase.Paused => Loc.F("StPausedFmt", Loc.When(state.PausedUntil ?? now, now)),
            FilterPhase.Day => Loc.T("StDay") + next,
            FilterPhase.Dusk => $"{Loc.T("StDusk")} · {state.TargetKelvin} K",
            FilterPhase.Dawn => $"{Loc.T("StDawn")} · {state.TargetKelvin} K",
            _ when _engine.Settings.Mode == FilterMode.AlwaysOn => $"{Loc.T("StAlways")} · {state.TargetKelvin} K",
            _ => $"{Loc.T("StNight")} · {state.TargetKelvin} K{next}",
        };
    }

    // ------------------------------------------------------------------ Actions

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Toggle();
        }
    }

    /// <summary>Left click: paused → resume, on → off, off → on.</summary>
    private void Toggle()
    {
        var now = DateTimeOffset.Now;
        ChangeSettings(s =>
        {
            if (s.Enabled && s.IsPaused(now))
            {
                s.PausedUntil = null;
            }
            else
            {
                s.Enabled = !s.Enabled;
                s.PausedUntil = null;
            }
        });
    }

    private void PauseFor(TimeSpan duration) => ChangeSettings(s => s.PausedUntil = DateTimeOffset.Now + duration);

    private void PauseUntilMorning()
    {
        var until = _engine.Scheduler.NextMorning(DateTimeOffset.Now);
        ChangeSettings(s => s.PausedUntil = until);
    }

    private void Resume() => ChangeSettings(s => s.PausedUntil = null);

    private void ChangeSettings(Action<EventideSettings> change)
    {
        var settings = _store.Update(change);
        _engine.Apply(settings);
        UpdateUi();
    }

    private void Reload()
    {
        var settings = _store.Load();
        Loc.SetLanguage(settings.Language);
        _engine.Apply(settings);
        UpdateUi();
    }

    private static void OpenSettings()
    {
        // Already open: bring it to the front.
        if (EventWaitHandle.TryOpenExisting(Program.SettingsShowEventName, out var show))
        {
            using (show)
            {
                show.Set();
            }

            return;
        }

        try
        {
            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Eventide.exe");
            Process.Start(new ProcessStartInfo(exe, "--settings") { UseShellExecute = false })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            LogError(ex);
        }
    }

    private void ExitTray()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _engine.Stop();
        _notify.Visible = false;
        ExitThread();
    }

    private TrayResponse Handle(TrayRequest request)
    {
        switch (request.Command)
        {
            case TrayCommand.Status:
                break;
            case TrayCommand.Reload:
                Reload();
                break;
            case TrayCommand.Preview when request.Kelvin is { } kelvin:
                _engine.SetPreview(kelvin);
                UpdateUi();
                break;
            case TrayCommand.PreviewEnd:
                _engine.EndPreview();
                UpdateUi();
                break;
            case TrayCommand.Toggle:
                Toggle();
                break;
            case TrayCommand.PauseFor when request.Minutes is { } minutes and > 0:
                PauseFor(TimeSpan.FromMinutes(Math.Min(minutes, 1440)));
                break;
            case TrayCommand.PauseUntilMorning:
                PauseUntilMorning();
                break;
            case TrayCommand.Resume:
                Resume();
                break;
            case TrayCommand.ShowSettings:
                OpenSettings();
                break;
            case TrayCommand.Exit:
                // Answer first, then quit.
                var timer = new WinFormsTimer { Interval = 300 };
                timer.Tick += (_, _) =>
                {
                    timer.Dispose();
                    ExitTray();
                };
                timer.Start();
                _engine.Stop();
                return new TrayResponse(true);
            default:
                return new TrayResponse(false, Error: "unknown command");
        }

        return new TrayResponse(true, _engine.Status());
    }

    // ------------------------------------------------------------------ System events

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => _delayedRefresh.Start();

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _delayedRefresh.Start();
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect
            or SessionSwitchReason.RemoteConnect or SessionSwitchReason.RemoteDisconnect or SessionSwitchReason.SessionLogon)
        {
            _delayedRefresh.Start();
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
        {
            // Taskbar switched between light and dark: reload the tray icon variant.
            _assets.ClearIcons();
            _iconState = null;
            UpdateUi();
        }
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e) => _engine.RestoreForSessionEnd();

    private void OnSessionEnded(object? sender, SessionEndedEventArgs e) => _engine.Stop();
}
