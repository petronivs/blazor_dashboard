using System.Globalization;
using System.Text.Json;

namespace BlazorDashboard.Services;

/// <summary>Typed client for the World Bank Indicators API (https://api.worldbank.org/v2).</summary>
public sealed class WorldBankClient(HttpClient http)
{
    public const string BaseUrl = "https://api.worldbank.org/v2/";

    public async Task<IReadOnlyList<WorldBankCountry>> GetCountriesAsync(CancellationToken ct = default)
    {
        var rows = await GetRowsAsync<WorldBankCountryRow>("country?format=json&per_page=400", ct);

        return rows
            .Where(r => !string.Equals(r.Region?.Value, "Aggregates", StringComparison.Ordinal))
            .Select(r => new WorldBankCountry(r.Id, r.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<WorldBankObservation>> GetIndicatorAsync(
        IReadOnlyList<string> countryIso3Codes,
        string indicatorId,
        int fromYear,
        int toYear,
        CancellationToken ct = default)
    {
        var countries = string.Join(';', countryIso3Codes.Select(Uri.EscapeDataString));
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"country/{countries}/indicator/{Uri.EscapeDataString(indicatorId)}?format=json&date={fromYear}:{toYear}&per_page=1000");

        var rows = await GetRowsAsync<WorldBankIndicatorRow>(url, ct);

        return rows
            .Where(r => r.Value is not null &&
                        r.CountryIso3Code is not null &&
                        int.TryParse(r.Date, CultureInfo.InvariantCulture, out _))
            .Select(r => new WorldBankObservation(
                r.CountryIso3Code!,
                int.Parse(r.Date!, CultureInfo.InvariantCulture),
                r.Value!.Value))
            .ToList();
    }

    private async Task<IReadOnlyList<T>> GetRowsAsync<T>(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() < 2)
        {
            return [];
        }

        var rows = doc.RootElement[1];
        if (rows.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        return rows.Deserialize<List<T>>() ?? [];
    }
}
