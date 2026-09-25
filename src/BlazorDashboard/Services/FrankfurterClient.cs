using System.Globalization;
using System.Net.Http.Json;

namespace BlazorDashboard.Services;

/// <summary>Typed client for the Frankfurter v2 API (https://frankfurter.dev). No API key; CORS is open.</summary>
public sealed class FrankfurterClient(HttpClient http)
{
    public const string BaseUrl = "https://api.frankfurter.dev/";

    public async Task<IReadOnlyList<Currency>> GetCurrenciesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<Currency>>("v2/currencies", ct) ?? [];

    /// <summary>
    /// Daily rates for each quote currency between <paramref name="from"/> and <paramref name="to"/> (inclusive).
    /// Only business days are returned, so the series has gaps.
    /// </summary>
    public async Task<IReadOnlyList<Rate>> GetRatesAsync(
        string baseCode, IEnumerable<string> quotes, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var quoteList = string.Join(',', quotes.Select(Uri.EscapeDataString));
        var url = string.Create(CultureInfo.InvariantCulture,
            $"v2/rates?base={Uri.EscapeDataString(baseCode)}&quotes={quoteList}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        return await http.GetFromJsonAsync<List<Rate>>(url, ct) ?? [];
    }
}
