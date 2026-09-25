using System.Text.Json.Serialization;

namespace BlazorDashboard.Services;

/// <summary>A currency from <c>GET /v2/currencies</c>.</summary>
public sealed record Currency(
    [property: JsonPropertyName("iso_code")] string IsoCode,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("symbol")] string? Symbol);

/// <summary>One row from <c>GET /v2/rates</c>: the price of 1 <see cref="Base"/> in <see cref="Quote"/> on <see cref="Date"/>.</summary>
public sealed record Rate(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("base")] string Base,
    [property: JsonPropertyName("quote")] string Quote,
    [property: JsonPropertyName("rate")] decimal Value);
