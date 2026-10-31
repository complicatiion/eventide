namespace Eventide.Core.Color;

/// <summary>Per-channel multipliers (0..1) applied to the display gamma ramp.</summary>
public readonly record struct RgbFactors(double R, double G, double B)
{
    public static RgbFactors Neutral { get; } = new(1, 1, 1);
}

/// <summary>
/// Colour temperature → RGB multipliers.
/// Approximation of the black-body colour after Tanner Helland's published curve fit
/// (https://tannerhelland.com/2012/09/18/convert-temperature-rgb-algorithm-code.html), own implementation,
/// normalised so that <see cref="NeutralKelvin"/> (6500 K, sRGB white point) is exactly 1/1/1.
/// </summary>
public static class ColorTemperature
{
    public const int MinKelvin = 1900;
    public const int NeutralKelvin = 6500;

    /// <summary>Gamma ramp length per channel (Windows: 256 entries of 16 bit).</summary>
    public const int RampSize = 256;

    private static readonly (double R, double G, double B) NeutralRaw = Raw(NeutralKelvin);

    public static int Clamp(int kelvin) => Math.Clamp(kelvin, MinKelvin, NeutralKelvin);

    public static RgbFactors ToFactors(int kelvin)
    {
        kelvin = Clamp(kelvin);
        if (kelvin >= NeutralKelvin)
        {
            return RgbFactors.Neutral;
        }

        var raw = Raw(kelvin);
        return new RgbFactors(
            Math.Min(1.0, raw.R / NeutralRaw.R),
            Math.Min(1.0, raw.G / NeutralRaw.G),
            Math.Min(1.0, raw.B / NeutralRaw.B));
    }

    /// <summary>
    /// Interpolates between day and night temperature in mired space (perceptually more even than Kelvin).
    /// <paramref name="nightFactor"/> 0 = day, 1 = night.
    /// </summary>
    public static int Interpolate(int dayKelvin, int nightKelvin, double nightFactor)
    {
        nightFactor = Math.Clamp(nightFactor, 0, 1);
        if (nightFactor <= 0)
        {
            return dayKelvin;
        }

        if (nightFactor >= 1)
        {
            return nightKelvin;
        }

        var dayMired = 1_000_000.0 / dayKelvin;
        var nightMired = 1_000_000.0 / nightKelvin;
        var mired = dayMired + ((nightMired - dayMired) * nightFactor);
        return (int)Math.Round(1_000_000.0 / mired);
    }

    /// <summary>
    /// Builds a 3×256 gamma ramp (R, G, B) by scaling <paramref name="baseRamp"/> (the display's own ramp, e.g. a
    /// calibration) – or the identity ramp when null – with the channel factors.
    /// </summary>
    public static ushort[] BuildRamp(RgbFactors factors, ushort[]? baseRamp = null)
    {
        var ramp = new ushort[RampSize * 3];
        for (var i = 0; i < RampSize; i++)
        {
            ramp[i] = Scale(Base(baseRamp, 0, i), factors.R);
            ramp[RampSize + i] = Scale(Base(baseRamp, 1, i), factors.G);
            ramp[(RampSize * 2) + i] = Scale(Base(baseRamp, 2, i), factors.B);
        }

        return ramp;
    }

    /// <summary>The identity ramp (no correction).</summary>
    public static ushort[] IdentityRamp() => BuildRamp(RgbFactors.Neutral);

    /// <summary>
    /// True when a ramp read from the display looks like a warm tint left behind (e.g. after a crash of a filter tool)
    /// rather than a calibration: blue clearly below red at full level.
    /// </summary>
    public static bool LooksTinted(ushort[] ramp)
    {
        if (ramp.Length < RampSize * 3)
        {
            return true;
        }

        var red = ramp[RampSize - 1];
        var blue = ramp[(RampSize * 3) - 1];
        return red == 0 || blue < red * 0.9;
    }

    /// <summary>sRGB preview colour of a temperature (for the swatch in the settings window).</summary>
    public static (byte R, byte G, byte B) PreviewColor(int kelvin)
    {
        var f = ToFactors(kelvin);
        return ((byte)Math.Round(255 * f.R), (byte)Math.Round(255 * f.G), (byte)Math.Round(255 * f.B));
    }

    private static ushort Base(ushort[]? baseRamp, int channel, int index) =>
        baseRamp is { Length: >= RampSize * 3 } ? baseRamp[(channel * RampSize) + index] : (ushort)(index * 257);

    private static ushort Scale(ushort value, double factor) =>
        (ushort)Math.Clamp(Math.Round(value * factor), 0, ushort.MaxValue);

    private static (double R, double G, double B) Raw(double kelvin)
    {
        var t = kelvin / 100.0;
        double r, g, b;

        if (t <= 66)
        {
            r = 255;
            g = (99.4708025861 * Math.Log(t)) - 161.1195681661;
            b = t <= 19 ? 0 : (138.5177312231 * Math.Log(t - 10)) - 305.0447927307;
        }
        else
        {
            r = 329.698727446 * Math.Pow(t - 60, -0.1332047592);
            g = 288.1221695283 * Math.Pow(t - 60, -0.0755148492);
            b = 255;
        }

        return (Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
    }
}
