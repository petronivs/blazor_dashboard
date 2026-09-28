using BlazorDashboard.Components;
using BlazorDashboard.Pages;
using Bunit;
using BlazorDashboard.Services;
using BlazorDashboard.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorDashboard.Tests.Pages;

public class HomeTests : BunitContext
{
    public HomeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero)));
        Services.AddSingleton(_ => new FakeFrankfurterApi().CreateClient());
        Services.AddSingleton(_ => new FakeWorldBankApi().CreateClient());
        Services.AddScoped<IDashboardStateStore, CookieDashboardStateStore>();
    }

    [Fact]
    public void RendersWorldFinanceShellWithTabs()
    {
        var cut = Render<Home>();

        Assert.Equal("World Finance Dashboard", cut.Find("h1").TextContent);

        var tabs = cut.FindAll(".dashboard-tabs .dashboard-tab");
        Assert.Equal(["Foreign exchange", "Inflation"], tabs.Select(t => t.TextContent.Trim()));
        Assert.Contains("dashboard-tab-active", tabs[0].ClassName);
        Assert.DoesNotContain("dashboard-tab-active", tabs[1].ClassName);
    }

    [Fact]
    public void TabSwitch_TogglesDashboardPanels()
    {
        var cut = Render<Home>();

        Assert.Single(cut.FindComponents<FxDashboard>());
        Assert.Empty(cut.FindComponents<InflationDashboard>());

        cut.Find("button[data-tab='inflation']").Click();

        Assert.Single(cut.FindComponents<InflationDashboard>());
        Assert.Empty(cut.FindComponents<FxDashboard>());
    }
}
