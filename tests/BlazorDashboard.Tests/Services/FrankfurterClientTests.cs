using System.Net;
using System.Text.Json;
using BlazorDashboard.Services;
using BlazorDashboard.Tests.TestSupport;

namespace BlazorDashboard.Tests.Services;

public class FrankfurterClientTests
{
    private static (FrankfurterClient Client, StubHttpHandler Handler) Create(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHttpHandler((_, _) => Task.FromResult(StubHttpHandler.Json(json, status)));
        var client = new FrankfurterClient(new HttpClient(handler) { BaseAddress = new Uri(FrankfurterClient.BaseUrl) });
        return (client, handler);
    }

    [Fact]
    public async Task GetCurrenciesAsync_RequestsCurrenciesEndpoint()
    {
        var (client, handler) = Create("[]");

        await client.GetCurrenciesAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.frankfurter.dev/v2/currencies", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetCurrenciesAsync_MapsSnakeCaseFieldsAndNullSymbol()
    {
        var (client, _) = Create("""
            [
              {"iso_code":"EUR","iso_numeric":"978","name":"Euro","symbol":"€","start_date":"1999-01-04","end_date":"2026-09-25"},
              {"iso_code":"CMD","iso_numeric":null,"name":"COMESA Dollar","symbol":null}
            ]
            """);

        var currencies = await client.GetCurrenciesAsync();

        Assert.Equal(
            [new Currency("EUR", "Euro", "€"), new Currency("CMD", "COMESA Dollar", null)],
            currencies);
    }

    [Fact]
    public async Task GetCurrenciesAsync_ReturnsEmptyListForNullBody()
    {
        var (client, _) = Create("null");

        Assert.Empty(await client.GetCurrenciesAsync());
    }

    [Fact]
    public async Task GetRatesAsync_BuildsQueryWithBaseQuotesAndIsoDates()
    {
        var (client, handler) = Create("[]");

        await client.GetRatesAsync("USD", ["EUR", "GBP"], new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "https://api.frankfurter.dev/v2/rates?base=USD&quotes=EUR,GBP&from=2026-09-01&to=2026-09-30",
            request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetRatesAsync_UsesIsoDatesRegardlessOfCurrentCulture()
    {
        var (client, handler) = Create("[]");
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            await client.GetRatesAsync("USD", ["EUR"], new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }

        Assert.EndsWith("from=2026-09-01&to=2026-09-30", Assert.Single(handler.Requests).RequestUri!.ToString());
    }

    [Fact]
    public async Task GetRatesAsync_EscapesCodes()
    {
        var (client, handler) = Create("[]");

        await client.GetRatesAsync("U&D", ["E R", "G/P"], new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1));

        Assert.Equal(
            "https://api.frankfurter.dev/v2/rates?base=U%26D&quotes=E%20R,G%2FP&from=2026-09-01&to=2026-09-01",
            Assert.Single(handler.Requests).RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GetRatesAsync_DeserializesRows()
    {
        var (client, _) = Create("""
            [
              {"date":"2026-09-01","base":"USD","quote":"EUR","rate":0.862},
              {"date":"2026-09-02","base":"USD","quote":"EUR","rate":0.86287}
            ]
            """);

        var rates = await client.GetRatesAsync("USD", ["EUR"], new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2));

        Assert.Equal(
            [
                new Rate(new DateOnly(2026, 9, 1), "USD", "EUR", 0.862m),
                new Rate(new DateOnly(2026, 9, 2), "USD", "EUR", 0.86287m),
            ],
            rates);
    }

    [Fact]
    public async Task GetRatesAsync_ReturnsEmptyListForNullBody()
    {
        var (client, _) = Create("null");

        Assert.Empty(await client.GetRatesAsync("USD", ["EUR"], new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2)));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task NonSuccessStatus_ThrowsHttpRequestException(HttpStatusCode status)
    {
        var (client, _) = Create("""{"message":"nope"}""", status);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetCurrenciesAsync());
        Assert.Equal(status, ex.StatusCode);
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetRatesAsync("USD", ["EUR"], new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2)));
    }

    [Fact]
    public async Task MalformedJson_ThrowsJsonException()
    {
        var (client, _) = Create("not json");

        await Assert.ThrowsAsync<JsonException>(() => client.GetCurrenciesAsync());
    }

    [Fact]
    public async Task GetRatesAsync_PassesCancellationToHandler()
    {
        var handler = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return StubHttpHandler.Json("[]");
        });
        var client = new FrankfurterClient(new HttpClient(handler) { BaseAddress = new Uri(FrankfurterClient.BaseUrl) });
        using var cts = new CancellationTokenSource();

        var call = client.GetRatesAsync("USD", ["EUR"], new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }
}
