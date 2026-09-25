namespace BlazorDashboard.Services;

public static class NumberFormat
{
    /// <summary>
    /// Formats with precision chosen by magnitude, since rates span many orders
    /// (JPY ~150, EUR ~0.9, gold ~0.0003).
    /// </summary>
    public static string Adaptive(decimal value, IFormatProvider? provider = null) => Math.Abs(value) switch
    {
        >= 100 => value.ToString("N2", provider),
        >= 1 => value.ToString("N4", provider),
        0 => "0",
        _ => value.ToString("G4", provider),
    };
}
