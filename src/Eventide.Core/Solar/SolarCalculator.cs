namespace Eventide.Core.Solar;

public enum SunDayKind
{
    /// <summary>Sun rises and sets on this day.</summary>
    Normal,

    /// <summary>Sun stays below the horizon all day.</summary>
    PolarNight,

    /// <summary>Sun stays above the horizon all day.</summary>
    MidnightSun,
}

/// <summary>Sunrise and sunset of one local day (null on polar days).</summary>
public readonly record struct SunTimes(SunDayKind Kind, DateTimeOffset? Sunrise, DateTimeOffset? Sunset);

/// <summary>
/// Sunrise / sunset after the NOAA "General Solar Position Calculations" (public domain, NOAA Global Monitoring
/// Division). Accuracy about ±2 minutes between the polar circles – more than enough for a colour filter.
/// Own implementation, no third-party code.
/// </summary>
public static class SolarCalculator
{
    /// <summary>Official sunrise/sunset zenith: 90° + 50′ (refraction and solar disc radius).</summary>
    private const double ZenithDegrees = 90.833;

    public static SunTimes Calculate(DateOnly localDate, double latitude, double longitude, TimeZoneInfo timeZone)
    {
        latitude = Math.Clamp(latitude, -89.9, 89.9);
        longitude = NormalizeLongitude(longitude);

        // Reference: local noon of the day, expressed in UTC.
        var localNoon = localDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified);
        var utcNoon = TimeZoneInfo.ConvertTimeToUtc(localNoon, timeZone);
        var utcDay = utcNoon.Date;

        var dayOfYear = utcNoon.DayOfYear;
        var daysInYear = DateTime.IsLeapYear(utcNoon.Year) ? 366 : 365;
        var gamma = 2 * Math.PI / daysInYear * (dayOfYear - 1 + ((utcNoon.Hour - 12) / 24.0));

        var eqTime = 229.18 * (0.000075
                               + (0.001868 * Math.Cos(gamma))
                               - (0.032077 * Math.Sin(gamma))
                               - (0.014615 * Math.Cos(2 * gamma))
                               - (0.040849 * Math.Sin(2 * gamma)));

        var decl = 0.006918
                   - (0.399912 * Math.Cos(gamma))
                   + (0.070257 * Math.Sin(gamma))
                   - (0.006758 * Math.Cos(2 * gamma))
                   + (0.000907 * Math.Sin(2 * gamma))
                   - (0.002697 * Math.Cos(3 * gamma))
                   + (0.00148 * Math.Sin(3 * gamma));

        var latRad = DegToRad(latitude);
        var cosHa = (Math.Cos(DegToRad(ZenithDegrees)) / (Math.Cos(latRad) * Math.Cos(decl)))
                    - (Math.Tan(latRad) * Math.Tan(decl));

        if (cosHa > 1)
        {
            return new SunTimes(SunDayKind.PolarNight, null, null);
        }

        if (cosHa < -1)
        {
            return new SunTimes(SunDayKind.MidnightSun, null, null);
        }

        var ha = RadToDeg(Math.Acos(cosHa));
        var sunriseMinutes = 720 - (4 * (longitude + ha)) - eqTime;
        var sunsetMinutes = 720 - (4 * (longitude - ha)) - eqTime;

        var baseUtc = new DateTimeOffset(utcDay, TimeSpan.Zero);
        return new SunTimes(
            SunDayKind.Normal,
            TimeZoneInfo.ConvertTime(baseUtc.AddMinutes(sunriseMinutes), timeZone),
            TimeZoneInfo.ConvertTime(baseUtc.AddMinutes(sunsetMinutes), timeZone));
    }

    /// <summary>
    /// Rough position when the user has not set a location: longitude from the standard UTC offset (15° per hour),
    /// latitude 50° N. Only used as a fallback and shown as "approximate" in the UI.
    /// </summary>
    public static (double Latitude, double Longitude) ApproximateFromTimeZone(TimeZoneInfo timeZone) =>
        (50.0, Math.Clamp(timeZone.BaseUtcOffset.TotalHours * 15.0, -180, 180));

    public static bool IsValid(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 && !double.IsNaN(latitude) && !double.IsNaN(longitude);

    private static double NormalizeLongitude(double longitude)
    {
        while (longitude > 180)
        {
            longitude -= 360;
        }

        while (longitude < -180)
        {
            longitude += 360;
        }

        return longitude;
    }

    private static double DegToRad(double deg) => deg * Math.PI / 180.0;

    private static double RadToDeg(double rad) => rad * 180.0 / Math.PI;
}
