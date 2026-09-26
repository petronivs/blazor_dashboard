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
        Services.AddScoped<IDashboardStateStore, CookieDashboardStateStore>();
    }

    [Fact]
    public void RootRoute_RendersWorldFinanceDashboardShellInsideMainLayout()
    {
        var cut = Render<App>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("World Finance Dashboard", cut.Find("main.container h1").TextContent);

            var tab = cut.Find("[role=tablist] [role=tab]");
            Assert.Equal("Foreign exchange", tab.TextContent.Trim());
            Assert.Equal("true", tab.GetAttribute("aria-selected"));
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
