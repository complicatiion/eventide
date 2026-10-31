using Eventide.Core.Color;

namespace Eventide.Platform.Windows;

/// <summary>Result of applying a temperature to all displays.</summary>
public readonly record struct GammaResult(
    int RequestedKelvin,
    int AppliedKelvin,
    int DisplayCount,
    bool Limited,
    bool Unsupported);

/// <summary>
/// Sets the colour temperature through the GDI gamma ramp of every display attached to the desktop
/// (<c>SetDeviceGammaRamp</c>, as f.lux does).
/// <list type="bullet">
/// <item>The ramp found at start is kept per display (a calibration stays intact: night ramp = original × factors) and restored on exit.</item>
/// <item>Windows refuses ramps far away from the identity unless <c>GdiIcmGammaRange</c> = 256 is set. Then the warmest
/// accepted value is found by bisection, cached per display and reported as <see cref="GammaResult.Limited"/>.</item>
/// </list>
/// </summary>
public sealed class GammaController : IDisposable
{
    private readonly Dictionary<string, Display> _displays = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public int DisplayCount
    {
        get
        {
            lock (_lock)
            {
                return _displays.Count;
            }
        }
    }

    /// <summary>Re-enumerates the displays (start, display change). New displays get their original ramp captured.</summary>
    public void RefreshDisplays()
    {
        lock (_lock)
        {
            var current = EnumerateDisplayNames();
            foreach (var name in _displays.Keys.Where(k => !current.Contains(k)).ToList())
            {
                _displays.Remove(name);
            }

            foreach (var name in current)
            {
                if (!_displays.ContainsKey(name))
                {
                    _displays[name] = new Display(name, CaptureOriginal(name));
                }
            }
        }
    }

    /// <summary>
    /// Forgets the cached limits (after the gamma range setting changed or a display reconnected).
    /// </summary>
    public void ResetLimits()
    {
        lock (_lock)
        {
            foreach (var d in _displays.Values)
            {
                d.WarmestAccepted = null;
            }
        }
    }

    public GammaResult Apply(int kelvin)
    {
        kelvin = ColorTemperature.Clamp(kelvin);
        lock (_lock)
        {
            if (_displays.Count == 0)
            {
                RefreshDisplaysUnlocked();
            }

            var applied = kelvin;
            var limited = false;
            var anyOk = false;

            foreach (var display in _displays.Values)
            {
                var target = display.WarmestAccepted is { } warmest ? Math.Max(kelvin, warmest) : kelvin;
                if (TrySet(display, target))
                {
                    anyOk = true;
                    if (target != kelvin)
                    {
                        limited = true;
                    }

                    applied = Math.Max(applied, target);
                    continue;
                }

                // Refused: find the warmest value this display accepts.
                var accepted = FindWarmestAccepted(display, target);
                if (accepted is { } k)
                {
                    display.WarmestAccepted = k;
                    TrySet(display, k);
                    anyOk = true;
                    limited = true;
                    applied = Math.Max(applied, k);
                }
            }

            return new GammaResult(kelvin, applied, _displays.Count, limited, _displays.Count > 0 && !anyOk);
        }
    }

    /// <summary>Puts every display back to the ramp found at start.</summary>
    public void Restore()
    {
        lock (_lock)
        {
            foreach (var display in _displays.Values)
            {
                WithDc(display.Name, hdc => NativeMethods.SetDeviceGammaRamp(hdc, display.Original));
            }
        }
    }

    public void Dispose() => Restore();

    private void RefreshDisplaysUnlocked()
    {
        foreach (var name in EnumerateDisplayNames())
        {
            _displays[name] = new Display(name, CaptureOriginal(name));
        }
    }

    private static bool TrySet(Display display, int kelvin)
    {
        var ramp = ColorTemperature.BuildRamp(ColorTemperature.ToFactors(kelvin), display.Original);
        return WithDc(display.Name, hdc => NativeMethods.SetDeviceGammaRamp(hdc, ramp));
    }

    /// <summary>Bisection between the refused value and 6500 K (always accepted) – about 8 calls, once per display.</summary>
    private static int? FindWarmestAccepted(Display display, int refused)
    {
        if (!TrySet(display, ColorTemperature.NeutralKelvin))
        {
            return null;
        }

        var lo = refused; // refused
        var hi = ColorTemperature.NeutralKelvin; // accepted
        while (hi - lo > 50)
        {
            var mid = (lo + hi) / 2;
            if (TrySet(display, mid))
            {
                hi = mid;
            }
            else
            {
                lo = mid;
            }
        }

        return hi;
    }

    private static ushort[] CaptureOriginal(string name)
    {
        var ramp = new ushort[ColorTemperature.RampSize * 3];
        var ok = WithDc(name, hdc => NativeMethods.GetDeviceGammaRamp(hdc, ramp));

        // A warm ramp at start is most likely left over from a crash of a filter tool – use the identity instead.
        return ok && !ColorTemperature.LooksTinted(ramp) ? ramp : ColorTemperature.IdentityRamp();
    }

    private static bool WithDc(string deviceName, Func<IntPtr, bool> action)
    {
        var hdc = NativeMethods.CreateDCW(null, deviceName, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return action(hdc);
        }
        finally
        {
            NativeMethods.DeleteDC(hdc);
        }
    }

    private static HashSet<string> EnumerateDisplayNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var device = new NativeMethods.DISPLAY_DEVICE { cb = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
        for (uint i = 0; NativeMethods.EnumDisplayDevicesW(null, i, ref device, 0); i++)
        {
            if ((device.StateFlags & NativeMethods.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0 && !string.IsNullOrEmpty(device.DeviceName))
            {
                names.Add(device.DeviceName);
            }

            device = new NativeMethods.DISPLAY_DEVICE { cb = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
        }

        return names;
    }

    private sealed class Display(string name, ushort[] original)
    {
        public string Name { get; } = name;

        public ushort[] Original { get; } = original;

        /// <summary>Warmest temperature Windows accepted for this display (null = no limit found).</summary>
        public int? WarmestAccepted { get; set; }
    }
}
