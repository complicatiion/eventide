using Microsoft.Win32;

namespace Eventide.Platform.Windows;

/// <summary>
/// The system-wide value <c>HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ICM\GdiIcmGammaRange</c> (DWORD 256)
/// lets Windows accept strong gamma ramps (below ≈ 4000 K). Writing needs administrator rights and takes effect after
/// signing in again. Eventide marks its own change in <c>HKLM\SOFTWARE\sksdesign\Eventide\GammaRangeSetByEventide</c>
/// and only ever removes the value again when it set it itself (another tool such as f.lux may rely on it).
/// The installer uses the same marker.
/// </summary>
public static class GammaRange
{
    public const string IcmKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ICM";
    public const string ValueName = "GdiIcmGammaRange";
    public const int ExtendedValue = 256;
    public const string MarkerKey = @"SOFTWARE\sksdesign\Eventide";
    public const string MarkerValue = "GammaRangeSetByEventide";

    public static bool IsExtended()
    {
        try
        {
            using var key = OpenLocalMachine().OpenSubKey(IcmKey);
            return key?.GetValue(ValueName) is int value && value >= ExtendedValue;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>True when Eventide (app or installer) set the value.</summary>
    public static bool IsSetByEventide()
    {
        try
        {
            using var key = OpenLocalMachine().OpenSubKey(MarkerKey);
            return key?.GetValue(MarkerValue) is int value && value == 1;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>Elevated helper (<c>Eventide.exe --enable-gamma-range</c>). Returns the process exit code.</summary>
    public static int Enable()
    {
        try
        {
            if (IsExtended())
            {
                return 0;
            }

            using (var key = OpenLocalMachine().CreateSubKey(IcmKey, writable: true))
            {
                key.SetValue(ValueName, ExtendedValue, RegistryValueKind.DWord);
            }

            using (var marker = OpenLocalMachine().CreateSubKey(MarkerKey, writable: true))
            {
                marker.SetValue(MarkerValue, 1, RegistryValueKind.DWord);
            }

            return 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return 5; // ERROR_ACCESS_DENIED
        }
    }

    /// <summary>Elevated helper (<c>Eventide.exe --disable-gamma-range</c>): removes the value only if Eventide set it.</summary>
    public static int Disable()
    {
        try
        {
            if (!IsSetByEventide())
            {
                return 2;
            }

            using (var key = OpenLocalMachine().OpenSubKey(IcmKey, writable: true))
            {
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            using (var marker = OpenLocalMachine().OpenSubKey(MarkerKey, writable: true))
            {
                marker?.DeleteValue(MarkerValue, throwOnMissingValue: false);
            }

            return 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return 5;
        }
    }

    private static RegistryKey OpenLocalMachine() => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
}
