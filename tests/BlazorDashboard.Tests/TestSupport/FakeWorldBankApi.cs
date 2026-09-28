using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Web;
using BlazorDashboard.Services;

namespace BlazorDashboard.Tests.TestSupport;

internal sealed class FakeWorldBankApi
{
    public const string DefaultCountriesJson = """
        [
          {"page":1,"pages":1,"per_page":"400","total":5},
          [
            {"id":"USA","iso2Code":"US","name":"United States","region":{"id":"NAC","value":"North America"}},
            {"id":"DEU","iso2Code":"DE","name":"Germany","region":{"id":"ECS","value":"Europe & Central Asia"}},
            {"id":"JPN","iso2Code":"JP","name":"Japan","region":{"id":"EAS","value":"East Asia & Pacific"}},
            {"id":"GBR","iso2Code":"GB","name":"United Kingdom","region":{"id":"ECS","value":"Europe & Central Asia"}},
            {"id":"WLD","iso2Code":"1W","name":"World","region":{"id":"NA","value":"Aggregates"}}
          ]
        ]
        """;

    public string CountriesJson { get; set; } = DefaultCountriesJson;

    public Func<CancellationToken, Task<HttpResponseMessage>>? CountriesResponse { get; set; }

    public Func<IndicatorQuery, CancellationToken, Task<HttpResponseMessage>>? IndicatorResponse { get; set; }

    public List<IndicatorQuery> IndicatorQueries { get; } = [];

    public Dictionary<(string Indicator, string Country), List<(int Year, decimal? Value)>> Series { get; } = new()
    {
        [("FP.CPI.TOTL.ZG", "USA")] = [(2025, null), (2024, 3.4m), (2023, 4.1m), (2022, 8.0m)],
        [("FP.CPI.TOTL.ZG", "DEU")] = [(2024, 2.2m), (2023, 6.0m), (2022, 6.9m)],
        [("FP.CPI.TOTL.ZG", "JPN")] = [(2024, 2.8m), (2023, 3.2m), (2022, 2.5m)],
        [("FP.CPI.TOTL.ZG", "GBR")] = [(2024, 2.1m), (2023, 7.3m), (2022, 9.1m)],
        [("FP.CPI.TOTL", "USA")] = [(2024, 127.5m), (2023, 123.3m), (2022, 118.2m)],
        [("FP.CPI.TOTL", "DEU")] = [(2024, 121.1m), (2023, 118.5m), (2022, 111.8m)],
    };

    public StubHttpHandler Handler { get; }

    public FakeWorldBankApi() => Handler = new StubHttpHandler(RespondAsync);

    public WorldBankClient CreateClient() =>
        new(new HttpClient(Handler) { BaseAddress = new Uri(WorldBankClient.BaseUrl) });

    private Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/v2/country")
        {
            return CountriesResponse?.Invoke(ct) ?? Task.FromResult(StubHttpHandler.Json(CountriesJson));
        }

        if (path.Contains("/indicator/", StringComparison.Ordinal))
        {
            var query = IndicatorQuery.Parse(request.RequestUri);
            lock (IndicatorQueries)
            {
                IndicatorQueries.Add(query);
            }

            return IndicatorResponse?.Invoke(query, ct) ?? Task.FromResult(StubHttpHandler.Json(BuildIndicatorJson(query)));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    public string BuildIndicatorJson(IndicatorQuery query)
    {
        var rows = query.Countries
            .SelectMany(country =>
            {
                if (!Series.TryGetValue((query.Indicator, country), out var points))
                {
                    return [];
                }

                return points
                    .Where(p => p.Year >= query.FromYear && p.Year <= query.ToYear)
                    .Select(p => new
                    {
                        indicator = new { id = query.Indicator, value = query.Indicator },
                        country = new { id = country[..2], value = country },
                        countryiso3code = country,
                        date = p.Year.ToString(CultureInfo.InvariantCulture),
                        value = p.Value,
                    });
            })
            .OrderByDescending(r => r.date)
            .ToList();

        return JsonSerializer.Serialize(new object[]
        {
            new { page = 1, pages = 1, per_page = "1000", total = rows.Count },
            rows,
        });
    }
}

internal sealed record IndicatorQuery(string Indicator, IReadOnlyList<string> Countries, int FromYear, int ToYear)
{
    public static IndicatorQuery Parse(Uri uri)
    {
        var countryPart = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)[2];
        var indicator = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)[4];

        var q = HttpUtility.ParseQueryString(uri.Query);
        var bounds = (q["date"] ?? "").Split(':');
        return new IndicatorQuery(
            indicator,
            countryPart.Split(';'),
            int.Parse(bounds[0], CultureInfo.InvariantCulture),
            int.Parse(bounds[1], CultureInfo.InvariantCulture));
    }
}
