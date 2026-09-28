using BlazorDashboard.Tests.TestSupport;
using Bunit;
using BlazorDashboard.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorDashboard.Tests;

public class AppTests : BunitContext
{
    public AppTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; // FocusOnNavigate calls into JS
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero)));
        Services.AddSingleton(_ => new FakeFrankfurterApi().CreateClient());
        Services.AddSingleton(_ => new FakeWorldBankApi().CreateClient());
        Services.AddScoped<IDashboardStateStore, CookieDashboardStateStore>();
    }

    [Fact]
    public void StaticHostPage_UsesWorldFinanceTitle()
    {
        var indexHtmlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "BlazorDashboard", "wwwroot", "index.html"));

        Assert.Contains("<title>World Finance Dashboard</title>", File.ReadAllText(indexHtmlPath));
    }

    [Fact]
    public void RootRoute_RendersWorldFinanceDashboardShellInsideMainLayout()
    {
        var cut = Render<App>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("World Finance Dashboard", cut.Find("main.container h1").TextContent);

            var tab = cut.Find(".dashboard-tabs .dashboard-tab-active");
            Assert.Equal("Foreign exchange", tab.TextContent.Trim());
            Assert.Equal("FX Dashboard", cut.Find("main.container h2").TextContent);
        });
    }

    [Fact]
    public void UnknownRoute_RendersNotFoundPage()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/no-such-page");

        var cut = Render<App>();

        cut.WaitForAssertion(() => Assert.Equal("Not Found", cut.Find("main.container h3").TextContent));
    }
}
