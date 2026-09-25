using System.Globalization;
using System.Net;
using BlazorDashboard.Pages;
using BlazorDashboard.Services;
using BlazorDashboard.Tests.TestSupport;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorDashboard.Tests.Pages;

public class HomeTests : BunitContext
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeFrankfurterApi api = new();

    public HomeTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        Services.AddSingleton(_ => api.CreateClient());
    }

    private IRenderedComponent<Home> RenderLoaded()
    {
        var cut = Render<Home>();
        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("section.cards").GetAttribute("aria-busy")));
        return cut;
    }

    private static string[] CardCodes(IRenderedComponent<Home> cut) =>
        cut.FindAll("article.card .code").Select(e => e.TextContent).ToArray();

    private static string[] ChipCodes(IRenderedComponent<Home> cut) =>
        cut.FindAll(".chip").Select(e => e.FirstChild!.TextContent.Trim()).ToArray();

    private static string Card(IRenderedComponent<Home> cut, string code) =>
        cut.FindAll("article.card").Single(c => c.QuerySelector(".code")!.TextContent == code).TextContent;

    // --- Initial load -------------------------------------------------------

    [Fact]
    public void ShowsLoadingMessageUntilCurrenciesArrive()
    {
        var currencies = new TaskCompletionSource<HttpResponseMessage>();
        api.CurrenciesResponse = _ => currencies.Task;

        var cut = Render<Home>();

        Assert.Contains("Loading currencies…", cut.Markup);
        Assert.Empty(cut.FindAll("section.controls"));

        currencies.SetResult(StubHttpHandler.Json(FakeFrankfurterApi.DefaultCurrenciesJson));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("article.card").Count));
    }

    [Fact]
    public void InitialLoad_RequestsLast30DaysOfDefaultQuotesInUsd()
    {
        RenderLoaded();

        var query = Assert.Single(api.RateQueries);
        Assert.Equal("USD", query.Base);
        Assert.Equal(["EUR", "GBP", "JPY"], query.Quotes);
        Assert.Equal(new DateOnly(2026, 8, 26), query.From);
        Assert.Equal(new DateOnly(2026, 9, 25), query.To);
    }

    [Fact]
    public void InitialLoad_PopulatesInputs()
    {
        var cut = RenderLoaded();

        var baseSelect = cut.Find("section.controls select");
        Assert.Equal("USD", baseSelect.GetAttribute("value"));
        Assert.Equal(6, baseSelect.QuerySelectorAll("option").Length);
        Assert.Equal("1000", cut.Find("input[type=number]").GetAttribute("value"));

        var dates = cut.FindAll("input[type=date]");
        Assert.Equal("2026-08-26", dates[0].GetAttribute("value"));
        Assert.Equal("2026-09-25", dates[0].GetAttribute("max"));
        Assert.Equal("2026-09-25", dates[1].GetAttribute("value"));
        Assert.Equal("2026-08-26", dates[1].GetAttribute("min"));
        Assert.Equal("2026-09-25", dates[1].GetAttribute("max"));

        Assert.Equal(["EUR", "GBP", "JPY"], ChipCodes(cut));
    }

    [Fact]
    public void InitialLoad_RendersOneCardPerQuoteInChipOrder()
    {
        var cut = RenderLoaded();

        Assert.Equal(["EUR", "GBP", "JPY"], CardCodes(cut));
    }

    [Fact]
    public void Card_ShowsNameLatestRateChangeAndConversion()
    {
        var cut = RenderLoaded();

        var eur = Card(cut, "EUR");
        Assert.Contains("Euro", eur);
        Assert.Equal("0.918", cut.Find("article.card .rate").TextContent);
        Assert.Contains("EUR per 1 USD · Sep 25, 2026", eur);
        Assert.Contains("▲ +2.00%", eur);
        Assert.Contains("since Aug 26", eur);
        Assert.Contains("1,000.00 USD = 918.00 EUR", eur);
    }

    [Fact]
    public void Card_ShowsDownArrowForFallingRate()
    {
        var cut = RenderLoaded();

        Assert.Contains("▼ -2.50%", Card(cut, "GBP"));
        Assert.Contains("▼ -1.00%", Card(cut, "JPY"));
        Assert.Contains("148.50", Card(cut, "JPY"));
        Assert.Contains("1,000.00 USD = 148,500.00 JPY", Card(cut, "JPY"));
    }

    [Fact]
    public void Card_FallsBackToCodeWhenCurrencyNameUnknown()
    {
        api.CurrenciesJson = """[{"iso_code":"USD","name":"United States Dollar","symbol":"$"}]""";

        var cut = RenderLoaded();

        var header = cut.FindAll("article.card .card-header .muted").Select(e => e.TextContent);
        Assert.Equal(["EUR", "GBP", "JPY"], header);
    }

    [Fact]
    public void NoRatesMessageHiddenWhenAllQuotesHaveData()
    {
        var cut = RenderLoaded();

        Assert.DoesNotContain("No rates published", cut.Markup);
        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    // --- Errors -------------------------------------------------------------

    [Fact]
    public void CurrencyLoadFailure_ShowsErrorAndSkipsRates()
    {
        api.CurrenciesResponse = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("Couldn't load currencies:", cut.Markup));
        Assert.Empty(api.RateQueries);
        Assert.Empty(cut.FindAll("section.controls"));
    }

    [Fact]
    public void CurrencyLoadWithMalformedJson_ShowsError()
    {
        api.CurrenciesJson = "<html>";

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("Couldn't load currencies:", cut.Markup));
    }

    [Fact]
    public void RateLoadFailure_ShowsAlertAndNoCards()
    {
        api.RatesResponse = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var cut = RenderLoaded();

        Assert.StartsWith("Couldn't load rates:", cut.Find("[role=alert]").TextContent);
        Assert.Empty(cut.FindAll("article.card"));
    }

    [Fact]
    public void RateLoadWithMalformedJson_ShowsAlert()
    {
        api.RatesResponse = (_, _) => Task.FromResult(StubHttpHandler.Json("{ nope"));

        var cut = RenderLoaded();

        Assert.StartsWith("Couldn't load rates:", cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public void RateError_ClearsAfterSuccessfulReload()
    {
        var fail = true;
        api.RatesResponse = (q, _) => Task.FromResult(fail
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : StubHttpHandler.Json(api.BuildRatesJson(q)));
        var cut = RenderLoaded();
        Assert.Single(cut.FindAll("[role=alert]"));

        fail = false;
        cut.Find("button[aria-label='Remove GBP']").Click();

        cut.WaitForAssertion(() => Assert.Equal(["EUR", "JPY"], CardCodes(cut)));
        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    // --- Quote chips ----------------------------------------------------------

    [Fact]
    public void AddCurrencyOptions_ExcludeBaseAndSelectedQuotes()
    {
        var cut = RenderLoaded();

        var options = cut.Find("select[aria-label='Add currency']").QuerySelectorAll("option")
            .Select(o => o.GetAttribute("value"));
        Assert.Equal(["", "CHF", "XAU"], options);
    }

    [Fact]
    public void AddingCurrency_AddsChipAndRefetches()
    {
        var cut = RenderLoaded();

        cut.Find("select[aria-label='Add currency']").Change("CHF");

        cut.WaitForAssertion(() => Assert.Equal(["EUR", "GBP", "JPY", "CHF"], CardCodes(cut)));
        Assert.Equal(["EUR", "GBP", "JPY", "CHF"], ChipCodes(cut));
        Assert.Equal(["EUR", "GBP", "JPY", "CHF"], api.RateQueries[^1].Quotes);
        Assert.Contains("▲ +0.00%", Card(cut, "CHF"));
    }

    [Fact]
    public void AddingPlaceholder_DoesNothing()
    {
        var cut = RenderLoaded();

        cut.Find("select[aria-label='Add currency']").Change("");

        Assert.Single(api.RateQueries);
        Assert.Equal(["EUR", "GBP", "JPY"], ChipCodes(cut));
    }

    [Fact]
    public void AddingCurrencyWithNoData_ListsItAsMissing()
    {
        var cut = RenderLoaded();

        cut.Find("select[aria-label='Add currency']").Change("XAU");

        cut.WaitForAssertion(() => Assert.Contains("No rates published for XAU in this date range.", cut.Markup));
        Assert.Equal(["EUR", "GBP", "JPY"], CardCodes(cut));
    }

    [Fact]
    public void RemovingCurrency_RemovesChipAndRefetches()
    {
        var cut = RenderLoaded();

        cut.Find("button[aria-label='Remove GBP']").Click();

        cut.WaitForAssertion(() => Assert.Equal(["EUR", "JPY"], CardCodes(cut)));
        Assert.Equal(["EUR", "JPY"], ChipCodes(cut));
        Assert.Equal(["EUR", "JPY"], api.RateQueries[^1].Quotes);
    }

    [Fact]
    public void RemovingAllCurrencies_ShowsHintWithoutRequesting()
    {
        var cut = RenderLoaded();

        foreach (var code in new[] { "EUR", "GBP", "JPY" })
        {
            cut.Find($"button[aria-label='Remove {code}']").Click();
        }

        cut.WaitForAssertion(() => Assert.Contains("Add a currency to compare against USD.", cut.Markup));
        Assert.Empty(cut.FindAll("article.card"));
        Assert.Equal(3, api.RateQueries.Count); // initial load + two removals; the last removal doesn't fetch
    }

    // --- Base currency, dates, amount ----------------------------------------

    [Fact]
    public void ChangingBase_DropsItFromQuotesAndRefetches()
    {
        var cut = RenderLoaded();

        cut.Find("section.controls select").Change("EUR");

        cut.WaitForAssertion(() => Assert.Equal(["GBP", "JPY"], CardCodes(cut)));
        Assert.Equal(["GBP", "JPY"], ChipCodes(cut));
        var query = api.RateQueries[^1];
        Assert.Equal("EUR", query.Base);
        Assert.Equal(["GBP", "JPY"], query.Quotes);
        Assert.Contains("GBP per 1 EUR", Card(cut, "GBP"));
    }

    [Fact]
    public void ChangingBaseToUnselectedCurrency_KeepsQuotes()
    {
        var cut = RenderLoaded();

        cut.Find("section.controls select").Change("CHF");

        cut.WaitForAssertion(() => Assert.Equal("CHF", api.RateQueries[^1].Base));
        Assert.Equal(["EUR", "GBP", "JPY"], ChipCodes(cut));
    }

    [Fact]
    public void ChangingDates_RefetchesWithNewRange()
    {
        var cut = RenderLoaded();

        cut.FindAll("input[type=date]")[0].Change("2026-01-02");
        cut.WaitForAssertion(() => Assert.Equal(new DateOnly(2026, 1, 2), api.RateQueries[^1].From));

        cut.FindAll("input[type=date]")[1].Change("2026-06-30");
        cut.WaitForAssertion(() => Assert.Equal(new DateOnly(2026, 6, 30), api.RateQueries[^1].To));

        Assert.Equal(new DateOnly(2026, 1, 2), api.RateQueries[^1].From);
        Assert.Contains("since Jan 2", Card(cut, "EUR"));
        Assert.Contains("Jun 30, 2026", Card(cut, "EUR"));
    }

    [Fact]
    public void FromAfterTo_ShowsValidationErrorWithoutRequesting()
    {
        var cut = RenderLoaded();

        cut.FindAll("input[type=date]")[0].Change("2026-09-26");

        cut.WaitForAssertion(() =>
            Assert.Equal("The From date must be on or before the To date.", cut.Find("[role=alert]").TextContent));
        Assert.Single(api.RateQueries);
        Assert.Empty(cut.FindAll("article.card"));
    }

    [Fact]
    public void ChangingAmount_UpdatesConversionWithoutRefetching()
    {
        var cut = RenderLoaded();

        cut.Find("input[type=number]").Change("250");

        Assert.Contains("250.00 USD = 229.50 EUR", Card(cut, "EUR"));
        Assert.Single(api.RateQueries);
    }

    // --- Concurrency -----------------------------------------------------------

    [Fact]
    public void MarksCardsBusyWhileLoading()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>();
        api.RatesResponse = (_, ct) => pending.Task.WaitAsync(ct);

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("section.cards").GetAttribute("aria-busy")));
        pending.SetResult(StubHttpHandler.Json(api.BuildRatesJson(api.RateQueries[0])));
        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("section.cards").GetAttribute("aria-busy")));
    }

    [Fact]
    public void NewerRequestSupersedesOneInFlight()
    {
        var first = new TaskCompletionSource<HttpResponseMessage>();
        CancellationToken firstToken = default;
        api.RatesResponse = (q, ct) =>
        {
            if (api.RateQueries.Count == 1)
            {
                firstToken = ct;
                return first.Task; // ignores cancellation, like a slow server
            }
            return Task.FromResult(StubHttpHandler.Json(api.BuildRatesJson(q)));
        };
        var cut = Render<Home>();
        cut.WaitForAssertion(() => Assert.Single(api.RateQueries));

        cut.Find("button[aria-label='Remove GBP']").Click();
        cut.WaitForAssertion(() => Assert.Equal(["EUR", "JPY"], CardCodes(cut)));
        Assert.True(firstToken.IsCancellationRequested);

        // The stale response arrives late with the old quote list; the page must ignore it.
        first.SetResult(StubHttpHandler.Json(api.BuildRatesJson(api.RateQueries[0])));

        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("section.cards").GetAttribute("aria-busy")));
        Assert.Equal(["EUR", "JPY"], CardCodes(cut));
        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    [Fact]
    public void StaleFailureDoesNotOverwriteNewerResults()
    {
        var first = new TaskCompletionSource<HttpResponseMessage>();
        api.RatesResponse = (q, _) => api.RateQueries.Count == 1
            ? first.Task
            : Task.FromResult(StubHttpHandler.Json(api.BuildRatesJson(q)));
        var cut = Render<Home>();
        cut.WaitForAssertion(() => Assert.Single(api.RateQueries));

        cut.Find("button[aria-label='Remove GBP']").Click();
        cut.WaitForAssertion(() => Assert.Equal(["EUR", "JPY"], CardCodes(cut)));

        first.SetResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("section.cards").GetAttribute("aria-busy")));
        Assert.Equal(["EUR", "JPY"], CardCodes(cut));
        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    [Fact]
    public async Task Dispose_CancelsInFlightRequest()
    {
        CancellationToken token = default;
        api.RatesResponse = (_, ct) =>
        {
            token = ct;
            return new TaskCompletionSource<HttpResponseMessage>().Task.WaitAsync(ct);
        };
        var cut = Render<Home>();
        cut.WaitForAssertion(() => Assert.Single(api.RateQueries));

        await DisposeComponentsAsync();

        Assert.True(token.IsCancellationRequested);
    }
}
