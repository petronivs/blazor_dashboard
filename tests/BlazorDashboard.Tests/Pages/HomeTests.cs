using System.Globalization;
using System.Net;
using System.Text.Json;
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
        Services.AddScoped<IDashboardStateStore, CookieDashboardStateStore>();
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
    public void SavedState_InitialLoadUsesRememberedSelections()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<string?>("dashboardCookies.get").SetResult(
            """
            {"baseCode":"CHF","amount":250.5,"from":"2026-08-01","to":"2026-08-31","quotes":["JPY","EUR"]}
            """);

        var cut = RenderLoaded();

        var query = Assert.Single(api.RateQueries);
        Assert.Equal("CHF", query.Base);
        Assert.Equal(["JPY", "EUR"], query.Quotes);
        Assert.Equal(new DateOnly(2026, 8, 1), query.From);
        Assert.Equal(new DateOnly(2026, 8, 31), query.To);

        var baseSelect = cut.Find("section.controls select");
        Assert.Equal("CHF", baseSelect.GetAttribute("value"));
        Assert.Equal("250.5", cut.Find("input[type=number]").GetAttribute("value"));

        var dates = cut.FindAll("input[type=date]");
        Assert.Equal("2026-08-01", dates[0].GetAttribute("value"));
        Assert.Equal("2026-08-31", dates[1].GetAttribute("value"));
        Assert.Equal(["JPY", "EUR"], ChipCodes(cut));
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

    [Fact]
    public void ChangingSelections_SavesStateToCookie()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid("dashboardCookies.set");

        var cut = RenderLoaded();

        cut.Find("button[aria-label='Remove GBP']").Click();
        cut.WaitForAssertion(() => Assert.Equal(["EUR", "JPY"], CardCodes(cut)));
        cut.Find("input[type=number]").Change("250");

        var saved = JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "dashboardCookies.set")
            .Select(invocation => invocation.Arguments[0]?.ToString())
            .Last();

        using var document = JsonDocument.Parse(saved!);
        Assert.Equal("USD", document.RootElement.GetProperty("baseCode").GetString());
        Assert.Equal(250m, document.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("2026-08-26", document.RootElement.GetProperty("from").GetString());
        Assert.Equal("2026-09-25", document.RootElement.GetProperty("to").GetString());
        Assert.Equal(["EUR", "JPY"], document.RootElement.GetProperty("quotes").EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    // --- Chart -------------------------------------------------------------------

    private static string[] LineSlots(IRenderedComponent<Home> cut) =>
        cut.FindAll("polyline.series-line").Select(l => l.ClassList.Single(c => c.StartsWith("slot-"))).ToArray();

    private static string[] LegendSlots(IRenderedComponent<Home> cut) =>
        cut.FindAll(".chart-legend li").Select(li =>
            $"{li.TextContent.Trim()}:{li.QuerySelector(".legend-key")!.ClassList.Single(c => c.StartsWith("slot-"))}").ToArray();

    [Fact]
    public void Chart_ShowsOneLinePerLoadedQuote()
    {
        var cut = RenderLoaded();

        Assert.Equal(3, cut.FindAll("section.chart-section polyline.series-line").Count);
        Assert.Contains("against USD", cut.Find("svg.chart-svg").GetAttribute("aria-label"));
        Assert.Equal("Change since Aug 26", cut.Find(".chart-title").TextContent.Trim());
    }

    [Fact]
    public void Chart_IsHiddenWhenAReloadFails()
    {
        api.RatesResponse = (q, _) => Task.FromResult(api.RateQueries.Count == 1
            ? StubHttpHandler.Json(api.BuildRatesJson(q))
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var cut = RenderLoaded();
        Assert.Single(cut.FindAll("svg.chart-svg"));

        cut.Find("button[aria-label='Remove GBP']").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=alert]")));
        Assert.Empty(cut.FindAll("svg.chart-svg"));
    }

    [Fact]
    public void Chart_IsHiddenWithNoQuotes()
    {
        var cut = RenderLoaded();
        Assert.Single(cut.FindAll("svg.chart-svg"));

        foreach (var code in new[] { "EUR", "GBP", "JPY" })
        {
            cut.Find($"button[aria-label='Remove {code}']").Click();
        }

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("svg.chart-svg")));
    }

    [Fact]
    public void ChartSection_IsBusyWhileLoading()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>();
        api.RatesResponse = (q, ct) => api.RateQueries.Count == 1
            ? Task.FromResult(StubHttpHandler.Json(api.BuildRatesJson(q)))
            : pending.Task.WaitAsync(ct);
        var cut = RenderLoaded();
        Assert.Equal("false", cut.Find("section.chart-section").GetAttribute("aria-busy"));

        cut.Find("button[aria-label='Remove GBP']").Click();

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("section.chart-section").GetAttribute("aria-busy")));
        Assert.Equal(3, cut.FindAll("polyline.series-line").Count); // previous render held while refetching
    }

    [Fact]
    public void Colors_AreAssignedInOrderInitially()
    {
        var cut = RenderLoaded();

        Assert.Equal(["EUR:slot-1", "GBP:slot-2", "JPY:slot-3"], LegendSlots(cut));
    }

    [Fact]
    public void Colors_StayWithTheirCurrencyWhenOthersAreRemoved()
    {
        var cut = RenderLoaded();

        cut.Find("button[aria-label='Remove GBP']").Click();

        cut.WaitForAssertion(() => Assert.Equal(["EUR:slot-1", "JPY:slot-3"], LegendSlots(cut)));
    }

    [Fact]
    public void Colors_NewCurrencyTakesTheLowestFreeSlot()
    {
        var cut = RenderLoaded();
        cut.Find("button[aria-label='Remove GBP']").Click();

        cut.Find("select[aria-label='Add currency']").Change("CHF");

        cut.WaitForAssertion(() => Assert.Equal(["EUR:slot-1", "JPY:slot-3", "CHF:slot-2"], LegendSlots(cut)));
    }

    [Fact]
    public void Colors_BaseChangeFreesTheRemovedQuotesSlot()
    {
        var cut = RenderLoaded();

        cut.Find("section.controls select").Change("EUR");
        cut.WaitForAssertion(() => Assert.Equal(["GBP:slot-2", "JPY:slot-3"], LegendSlots(cut)));

        cut.Find("select[aria-label='Add currency']").Change("CHF");
        cut.WaitForAssertion(() => Assert.Equal(["GBP:slot-2", "JPY:slot-3", "CHF:slot-1"], LegendSlots(cut)));
    }

    [Fact]
    public void Quotes_AreCappedAtEight()
    {
        string[] extra = ["AUD", "CAD", "CHF", "CNY", "HKD", "NZD"];
        api.CurrenciesJson = "[" + string.Join(",", extra.Concat(["EUR", "GBP", "JPY", "USD"])
            .Select(c => $$"""{"iso_code":"{{c}}","name":"{{c}} name","symbol":null}""")) + "]";
        foreach (var code in extra)
        {
            api.Series[code] = (1m, 1.01m);
        }
        var cut = RenderLoaded();

        foreach (var code in extra.Take(5))
        {
            Assert.False(cut.Find("select[aria-label='Add currency']").HasAttribute("disabled"));
            cut.Find("select[aria-label='Add currency']").Change(code);
        }

        cut.WaitForAssertion(() => Assert.Equal(8, ChipCodes(cut).Length));
        Assert.True(cut.Find("select[aria-label='Add currency']").HasAttribute("disabled"));
        Assert.Contains("Up to 8 currencies", cut.Find(".chips").TextContent);
        Assert.Equal(
            ["slot-1", "slot-2", "slot-3", "slot-4", "slot-5", "slot-6", "slot-7", "slot-8"],
            LineSlots(cut).Order());

        cut.Find("button[aria-label='Remove EUR']").Click();
        cut.WaitForAssertion(() => Assert.False(cut.Find("select[aria-label='Add currency']").HasAttribute("disabled")));
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
