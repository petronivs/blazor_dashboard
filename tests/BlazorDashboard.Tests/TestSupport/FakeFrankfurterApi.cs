using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Web;
using BlazorDashboard.Services;

namespace BlazorDashboard.Tests.TestSupport;

/// <summary>
/// In-memory stand-in for the Frankfurter API. Serves /v2/currencies and /v2/rates
/// and records each rates query so tests can assert on what the page asked for.
/// </summary>
internal sealed class FakeFrankfurterApi
{
    public const string DefaultCurrenciesJson = """
        [
          {"iso_code":"CHF","iso_numeric":"756","name":"Swiss Franc","symbol":"CHF"},
          {"iso_code":"EUR","iso_numeric":"978","name":"Euro","symbol":"€"},
          {"iso_code":"GBP","iso_numeric":"826","name":"British Pound","symbol":"£"},
          {"iso_code":"JPY","iso_numeric":"392","name":"Japanese Yen","symbol":"¥"},
          {"iso_code":"USD","iso_numeric":"840","name":"United States Dollar","symbol":"$"},
          {"iso_code":"XAU","iso_numeric":"959","name":"Gold (Troy Ounce)","symbol":"oz t"}
        ]
        """;

    /// <summary>Start/end rate per quote. Quotes not listed return no rows.</summary>
    public Dictionary<string, (decimal Start, decimal End)> Series { get; } = new()
    {
        ["EUR"] = (0.90m, 0.918m),
        ["GBP"] = (0.80m, 0.78m),
        ["JPY"] = (150m, 148.5m),
        ["CHF"] = (0.85m, 0.85m),
    };

    public string CurrenciesJson { get; set; } = DefaultCurrenciesJson;

    /// <summary>Overrides the /v2/currencies response.</summary>
    public Func<CancellationToken, Task<HttpResponseMessage>>? CurrenciesResponse { get; set; }

    /// <summary>Overrides the /v2/rates response. Receives the parsed query.</summary>
    public Func<RatesQuery, CancellationToken, Task<HttpResponseMessage>>? RatesResponse { get; set; }

    public List<RatesQuery> RateQueries { get; } = [];

    public StubHttpHandler Handler { get; }

    public FakeFrankfurterApi() => Handler = new StubHttpHandler(RespondAsync);

    public FrankfurterClient CreateClient() =>
        new(new HttpClient(Handler) { BaseAddress = new Uri(FrankfurterClient.BaseUrl) });

    private Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/v2/currencies")
        {
            return CurrenciesResponse?.Invoke(ct) ?? Task.FromResult(StubHttpHandler.Json(CurrenciesJson));
        }
        if (path == "/v2/rates")
        {
            var query = RatesQuery.Parse(request.RequestUri);
            lock (RateQueries)
            {
                RateQueries.Add(query);
            }
            return RatesResponse?.Invoke(query, ct) ?? Task.FromResult(StubHttpHandler.Json(BuildRatesJson(query)));
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    /// <summary>Two rows per known quote (on From and To), deliberately newest first.</summary>
    public string BuildRatesJson(RatesQuery query)
    {
        var rows = query.Quotes
            .Where(Series.ContainsKey)
            .SelectMany(q => new[]
            {
                new Rate(query.To, query.Base, q, Series[q].End),
                new Rate(query.From, query.Base, q, Series[q].Start),
            });
        return JsonSerializer.Serialize(rows.Select(r => new
        {
            date = r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            @base = r.Base,
            quote = r.Quote,
            rate = r.Value,
        }));
    }
}

internal sealed record RatesQuery(string Base, IReadOnlyList<string> Quotes, DateOnly From, DateOnly To)
{
    public static RatesQuery Parse(Uri uri)
    {
        var q = HttpUtility.ParseQueryString(uri.Query);
        return new RatesQuery(
            q["base"]!,
            q["quotes"]!.Split(','),
            DateOnly.ParseExact(q["from"]!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateOnly.ParseExact(q["to"]!, "yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
