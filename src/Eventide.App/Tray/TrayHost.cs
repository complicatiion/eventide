using System.Windows.Forms;

namespace Eventide.App.Tray;

/// <summary>Tray mode: Windows Forms message loop without a window.</summary>
internal static class TrayHost
{
    public static int Run()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        TrayContext? context = null;

        // UI thread: log and keep running. Anything fatal: restore the display colours first.
        Application.ThreadException += (_, e) => TrayContext.LogError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            context?.EmergencyRestore();
            if (e.ExceptionObject is Exception ex)
            {
                TrayContext.LogError(ex);
            }
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => context?.EmergencyRestore();

        context = new TrayContext();
        try
        {
            // Windows Forms disposes the context itself when ExitThread ends the message loop.
            Application.Run(context);
        }
        finally
        {
            // Safety net (e.g. the loop ended by an exception); TrayContext.Dispose is idempotent.
            context.Dispose();
        }

        return 0;
    }
}
