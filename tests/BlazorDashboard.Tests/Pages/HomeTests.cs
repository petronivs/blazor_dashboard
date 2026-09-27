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
        Services.AddScoped<IDashboardStateStore, CookieDashboardStateStore>();
    }

    [Fact]
    public void RendersWorldFinanceShell()
    {
        var cut = Render<Home>();

        Assert.Equal("World Finance Dashboard", cut.Find("h1").TextContent);

        var tab = cut.Find(".dashboard-tabs .dashboard-tab-active");
        Assert.Equal("Foreign exchange", tab.TextContent.Trim());
        Assert.Equal("#panel-foreign-exchange", tab.GetAttribute("href"));
    }

    [Fact]
    public void RendersFxDashboardInsideForeignExchangePanel()
    {
        var cut = Render<Home>();

        Assert.Equal("panel-foreign-exchange", cut.Find(".dashboard-panel").Id);
        Assert.NotNull(cut.FindComponent<FxDashboard>());
    }
}
