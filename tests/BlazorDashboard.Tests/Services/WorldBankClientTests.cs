using System.Net;
using System.Text.Json;
using BlazorDashboard.Services;
using BlazorDashboard.Tests.TestSupport;

namespace BlazorDashboard.Tests.Services;

public class WorldBankClientTests
{
    private static (WorldBankClient Client, StubHttpHandler Handler) Create(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHttpHandler((_, _) => Task.FromResult(StubHttpHandler.Json(json, status)));
        var client = new WorldBankClient(new HttpClient(handler) { BaseAddress = new Uri(WorldBankClient.BaseUrl) });
        return (client, handler);
    }

    [Fact]
    public async Task GetCountriesAsync_RequestsCountryEndpoint()
    {
        var (client, handler) = Create("[]");

        await client.GetCountriesAsync();

        Assert.Equal("https://api.worldbank.org/v2/country?format=json&per_page=400", Assert.Single(handler.Requests).RequestUri!.ToString());
    }

    [Fact]
    public async Task GetCountriesAsync_FiltersOutAggregates()
    {
        var (client, _) = Create(FakeWorldBankApi.DefaultCountriesJson);

        var countries = await client.GetCountriesAsync();

        Assert.Equal(
            [
                new WorldBankCountry("USA", "United States"),
                new WorldBankCountry("DEU", "Germany"),
                new WorldBankCountry("JPN", "Japan"),
                new WorldBankCountry("GBR", "United Kingdom"),
            ],
            countries);
    }

    [Fact]
    public async Task GetCountriesAsync_ReturnsEmptyListWhenRowsAreNull()
    {
        var (client, _) = Create("""
            [
              {"page":1,"pages":1,"per_page":"400","total":0},
              null
            ]
            """);

        Assert.Empty(await client.GetCountriesAsync());
    }

    [Fact]
    public async Task GetIndicatorAsync_RequestsCountriesAndIndicatorInOneCall()
    {
        var (client, handler) = Create("[]");

        await client.GetIndicatorAsync(["USA", "DEU", "JPN"], "FP.CPI.TOTL.ZG", 2010, 2025);

        Assert.Equal(
            "https://api.worldbank.org/v2/country/USA;DEU;JPN/indicator/FP.CPI.TOTL.ZG?format=json&date=2010:2025&per_page=1000",
            Assert.Single(handler.Requests).RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GetIndicatorAsync_SkipsRowsWithNullValue()
    {
        var (client, _) = Create("""
            [
              {"page":1,"pages":1,"per_page":"1000","total":4},
              [
                {"countryiso3code":"USA","date":"2025","value":null},
                {"countryiso3code":"USA","date":"2024","value":3.4},
                {"countryiso3code":"DEU","date":"2024","value":2.2},
                {"countryiso3code":"DEU","date":"2023","value":6.0}
              ]
            ]
            """);

        var rows = await client.GetIndicatorAsync(["USA", "DEU"], "FP.CPI.TOTL.ZG", 2023, 2025);

        Assert.Equal(
            [
                new WorldBankObservation("USA", 2024, 3.4m),
                new WorldBankObservation("DEU", 2024, 2.2m),
                new WorldBankObservation("DEU", 2023, 6.0m),
            ],
            rows);
    }

    [Fact]
    public async Task GetIndicatorAsync_ReturnsEmptyListWhenRowsAreNull()
    {
        var (client, _) = Create("""
            [
              {"page":1,"pages":1,"per_page":"1000","total":0},
              null
            ]
            """);

        Assert.Empty(await client.GetIndicatorAsync(["USA"], "FP.CPI.TOTL.ZG", 2020, 2025));
    }

    [Fact]
    public async Task GetIndicatorAsync_PassesCancellationToHandler()
    {
        var handler = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return StubHttpHandler.Json("[]");
        });
        var client = new WorldBankClient(new HttpClient(handler) { BaseAddress = new Uri(WorldBankClient.BaseUrl) });
        using var cts = new CancellationTokenSource();

        var call = client.GetIndicatorAsync(["USA"], "FP.CPI.TOTL.ZG", 2020, 2025, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    [Fact]
    public async Task MalformedJson_ThrowsJsonException()
    {
        var (client, _) = Create("not json");

        await Assert.ThrowsAsync<JsonException>(() => client.GetCountriesAsync());
    }
}
