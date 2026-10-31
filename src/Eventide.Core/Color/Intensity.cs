namespace Eventide.Core.Color;

/// <summary>Night temperature presets of the tray menu and the settings window.</summary>
public enum IntensityPreset
{
    /// <summary>≈ 5000 K</summary>
    Soft,

    /// <summary>≈ 4200 K</summary>
    Medium,

    /// <summary>≈ 3800 K – warm white</summary>
    Warm,

    /// <summary>≈ 3200 K – halogen look</summary>
    Strong,

    /// <summary>≈ 2700 K – candle light / incandescent bulb</summary>
    Extreme,

    /// <summary>Own value (settings window slider)</summary>
    Custom,
}

public static class Intensity
{
    public static readonly IntensityPreset[] Presets =
        [IntensityPreset.Soft, IntensityPreset.Medium, IntensityPreset.Warm, IntensityPreset.Strong, IntensityPreset.Extreme];

    public static int KelvinOf(IntensityPreset preset, int customKelvin) => preset switch
    {
        IntensityPreset.Soft => 5000,
        IntensityPreset.Medium => 4200,
        IntensityPreset.Warm => 3800,
        IntensityPreset.Strong => 3200,
        IntensityPreset.Extreme => 2700,
        _ => ColorTemperature.Clamp(customKelvin),
    };
}
