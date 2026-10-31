using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Eventide.Platform.Windows;

/// <summary>Read-only checks of the Windows environment.</summary>
public static class SystemState
{
    /// <summary>RDP / Citrix / other remote session: gamma ramps are not available.</summary>
    public static bool IsRemoteSession() => NativeMethods.GetSystemMetrics(NativeMethods.SM_REMOTESESSION) != 0;

    /// <summary>Light taskbar (Windows "Choose your default Windows mode" = Light) → darker tray icon.</summary>
    public static bool IsTaskbarLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value && value == 1;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Windows Night light state (best effort): Windows keeps it in an undocumented CloudStore blob; byte 18 is 0x15
    /// when Night light is switched on. Returns false when the value is missing or has an unknown layout.
    /// </summary>
    public static bool IsNightLightOn()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.bluelightreduction.bluelightreductionstate\windows.data.bluelightreduction.bluelightreductionstate");
            return key?.GetValue("Data") is byte[] data && data.Length > 18 && data[18] == 0x15;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>True when HDR (advanced colour) is active on at least one display – gamma ramps have no effect there.</summary>
    public static bool IsAnyHdrActive()
    {
        try
        {
            if (NativeMethods.GetDisplayConfigBufferSizes(NativeMethods.QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount) != 0)
            {
                return false;
            }

            var paths = new NativeMethods.DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new NativeMethods.DISPLAYCONFIG_MODE_INFO[modeCount];
            if (NativeMethods.QueryDisplayConfig(NativeMethods.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
            {
                return false;
            }

            for (var i = 0; i < pathCount; i++)
            {
                var info = new NativeMethods.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
                {
                    header = new NativeMethods.DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                        size = (uint)Marshal.SizeOf<NativeMethods.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                        adapterId = paths[i].targetAdapterId,
                        id = paths[i].targetId,
                    },
                };

                if (NativeMethods.DisplayConfigGetDeviceInfo(ref info) == 0 && (info.value & 0x2) != 0)
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or ExternalException)
        {
            // older Windows / unusual driver: treat as "no HDR"
        }

        return false;
    }

    /// <summary>Rounded corners for a popup window (tray menu) on Windows 11; ignored on Windows 10.</summary>
    public static void RoundCorners(IntPtr hwnd, bool small = false)
    {
        if (hwnd == IntPtr.Zero || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        var preference = small ? NativeMethods.DWMWCP_ROUNDSMALL : NativeMethods.DWMWCP_ROUND;
        _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }
}
