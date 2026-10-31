using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Eventide.App.SettingsUi;

/// <summary>Settings mode (<c>Eventide.exe --settings</c>): one window, single instance, ends with the window.</summary>
internal static class SettingsHost
{
    public static int Run(string[] args)
    {
        using var mutex = new Mutex(true, Program.SettingsMutexName, out var createdNew);
        if (!createdNew)
        {
            // Already open: bring the existing window to the front.
            if (EventWaitHandle.TryOpenExisting(Program.SettingsShowEventName, out var existing))
            {
                using (existing)
                {
                    existing.Set();
                }
            }

            return 0;
        }

        using var show = new EventWaitHandle(false, EventResetMode.AutoReset, Program.SettingsShowEventName);
        var watcher = new Thread(() =>
        {
            try
            {
                while (show.WaitOne())
                {
                    Dispatcher.UIThread.Post(BringToFront);
                }
            }
            catch (ObjectDisposedException)
            {
                // window closed, process is ending
            }
        })
        {
            IsBackground = true,
            Name = "Eventide.SettingsShow",
        };

        // Start listening only after Avalonia is set up, so the dispatcher is initialised on the UI thread.
        return BuildAvaloniaApp()
            .AfterSetup(_ => watcher.Start())
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<SettingsApp>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();

    private static void BringToFront()
    {
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            window.Topmost = true;
            window.Topmost = false;
        }
    }
}
