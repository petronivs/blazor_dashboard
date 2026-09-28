using System.Globalization;
using System.Net;
using BlazorDashboard.Components;
using BlazorDashboard.Services;
using BlazorDashboard.Tests.TestSupport;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorDashboard.Tests.Components;

public class InflationDashboardTests : BunitContext
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeWorldBankApi api = new();
    private readonly FixedTimeProvider clock = new(Now);

    public InflationDashboardTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        Services.AddSingleton<TimeProvider>(clock);
        Services.AddSingleton(_ => api.CreateClient());
    }

    private IRenderedComponent<InflationDashboard> RenderLoaded()
    {
        var cut = Render<InflationDashboard>();
        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("section.cards").GetAttribute("aria-busy")));
        return cut;
    }

    [Fact]
    public void InitialLoad_UsesDefaultCountriesIndicatorAndLast15Years()
    {
        RenderLoaded();

        var query = Assert.Single(api.IndicatorQueries);
        Assert.Equal(["USA", "DEU", "JPN", "GBR"], query.Countries);
        Assert.Equal("FP.CPI.TOTL.ZG", query.Indicator);
        Assert.Equal(2011, query.FromYear);
        Assert.Equal(2025, query.ToYear);
    }

    [Fact]
    public void Cards_ShowLatestNonNullValueAndYearPerCountry()
    {
        var cut = RenderLoaded();

        Assert.Contains("USA", cut.Markup);
        Assert.Contains("3.4", cut.Markup);
        Assert.Contains("2024", cut.Markup);
        Assert.Contains("DEU", cut.Markup);
        Assert.Contains("2.2", cut.Markup);
    }

    [Fact]
    public void ListsCountriesWithoutData()
    {
        api.Series.Remove(("FP.CPI.TOTL.ZG", "GBR"));
        var cut = RenderLoaded();

        Assert.Contains("No data in this range: GBR", cut.Markup);
    }

    [Fact]
    public void NewerRequestSupersedesOneInFlight()
    {
        var first = new TaskCompletionSource<HttpResponseMessage>();
        CancellationToken firstToken = default;
        api.IndicatorResponse = (q, ct) =>
        {
            if (api.IndicatorQueries.Count == 1)
            {
                firstToken = ct;
                return first.Task;
            }

            return Task.FromResult(StubHttpHandler.Json(api.BuildIndicatorJson(q)));
        };

        var cut = Render<InflationDashboard>();
        cut.WaitForAssertion(() => Assert.Single(api.IndicatorQueries));

        cut.Find("button[aria-label='Remove GBR']").Click();
        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("section.cards").GetAttribute("aria-busy")));

        Assert.True(firstToken.IsCancellationRequested);

        first.SetResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role=alert]")));
    }

    [Fact]
    public void CountrySelection_IsCappedAtEight()
    {
        api.CountriesJson = """
            [
              {"page":1,"pages":1,"per_page":"400","total":10},
              [
                {"id":"USA","iso2Code":"US","name":"United States","region":{"id":"NAC","value":"North America"}},
                {"id":"DEU","iso2Code":"DE","name":"Germany","region":{"id":"ECS","value":"Europe & Central Asia"}},
                {"id":"JPN","iso2Code":"JP","name":"Japan","region":{"id":"EAS","value":"East Asia & Pacific"}},
                {"id":"GBR","iso2Code":"GB","name":"United Kingdom","region":{"id":"ECS","value":"Europe & Central Asia"}},
                {"id":"FRA","iso2Code":"FR","name":"France","region":{"id":"ECS","value":"Europe & Central Asia"}},
                {"id":"CAN","iso2Code":"CA","name":"Canada","region":{"id":"NAC","value":"North America"}},
                {"id":"AUS","iso2Code":"AU","name":"Australia","region":{"id":"EAS","value":"East Asia & Pacific"}},
                {"id":"CHE","iso2Code":"CH","name":"Switzerland","region":{"id":"ECS","value":"Europe & Central Asia"}},
                {"id":"SWE","iso2Code":"SE","name":"Sweden","region":{"id":"ECS","value":"Europe & Central Asia"}},
                {"id":"NOR","iso2Code":"NO","name":"Norway","region":{"id":"ECS","value":"Europe & Central Asia"}}
              ]
            ]
            """;

        var extra = new[] { "FRA", "CAN", "AUS", "CHE", "SWE", "NOR" };
        foreach (var code in extra)
        {
            api.Series[("FP.CPI.TOTL.ZG", code)] = [(2024, 2.0m), (2023, 1.0m)];
        }

        var cut = RenderLoaded();

        foreach (var code in extra.Take(4))
        {
            cut.Find("select[aria-label='Add country']").Change(code);
        }

        cut.WaitForAssertion(() => Assert.Equal(8, cut.FindAll(".chip").Count));
        Assert.True(cut.Find("select[aria-label='Add country']").HasAttribute("disabled"));
    }
}
