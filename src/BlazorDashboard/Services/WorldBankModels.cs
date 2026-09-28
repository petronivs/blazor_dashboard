using System.Text.Json.Serialization;

namespace BlazorDashboard.Services;

public sealed record WorldBankCountry(string Iso3Code, string Name);

public sealed record WorldBankObservation(string CountryIso3Code, int Year, decimal Value);

internal sealed record WorldBankPage<T>(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pages")] int Pages,
    [property: JsonPropertyName("per_page")] string PerPage,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("rows")] IReadOnlyList<T>? Rows);

internal sealed record WorldBankCountryRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("region")] WorldBankRegion? Region);

internal sealed record WorldBankRegion(
    [property: JsonPropertyName("value")] string? Value);

internal sealed record WorldBankIndicatorRow(
    [property: JsonPropertyName("countryiso3code")] string? CountryIso3Code,
    [property: JsonPropertyName("date")] string? Date,
    [property: JsonPropertyName("value")] decimal? Value);
