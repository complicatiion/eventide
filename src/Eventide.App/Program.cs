using System.Runtime.CompilerServices;
using Eventide.Core.Ipc;
using Eventide.Platform.Windows;

namespace Eventide.App;

/// <summary>
/// Entry point. One EXE, several modes:
/// <list type="table">
/// <item><term>(no argument), --autostart</term><description>Tray (Windows Forms). A second start opens the settings window of the running tray.</description></item>
/// <item><term>--settings</term><description>Settings window (Avalonia) – own process, ends with the window.</description></item>
/// <item><term>--enable-gamma-range / --disable-gamma-range</term><description>Elevated helper for GdiIcmGammaRange (UAC).</description></item>
/// <item><term>--register-autostart / --unregister-autostart</term><description>Installer: Run key of the current user.</description></item>
/// <item><term>--exit</term><description>Installer / update: ask the running tray to restore the colours and quit.</description></item>
/// </list>
/// Tray and settings code live in separate classes so that the tray process never loads Avalonia.
/// </summary>
internal static class Program
{
    public const string TrayMutexName = @"Local\Eventide.Tray";
    public const string SettingsMutexName = @"Local\Eventide.Settings";
    public const string SettingsShowEventName = @"Local\Eventide.Settings.Show";

    [STAThread]
    public static int Main(string[] args)
    {
        bool Has(string flag) => args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

        if (Has("--enable-gamma-range"))
        {
            return GammaRange.Enable();
        }

        if (Has("--disable-gamma-range"))
        {
            return GammaRange.Disable();
        }

        if (Has("--register-autostart"))
        {
            return Autostart.Set(true) ? 0 : 1;
        }

        if (Has("--unregister-autostart"))
        {
            return Autostart.Set(false) ? 0 : 1;
        }

        if (Has("--exit"))
        {
            return TrayClient.Send(new TrayRequest(TrayCommand.Exit), 2000) is { Ok: true } ? 0 : 1;
        }

        if (Has("--settings"))
        {
            return RunSettings(args);
        }

        return RunTray(Has(Autostart.Argument));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunTray(bool fromAutostart)
    {
        using var mutex = new Mutex(true, TrayMutexName, out var createdNew);
        if (!createdNew)
        {
            // Already running: a manual second start opens the settings window; autostart stays silent.
            if (!fromAutostart)
            {
                TrayClient.Send(new TrayRequest(TrayCommand.ShowSettings), 1500);
            }

            return 0;
        }

        return Tray.TrayHost.Run();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunSettings(string[] args) => SettingsUi.SettingsHost.Run(args);
}
