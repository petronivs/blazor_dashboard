using System.Globalization;
using BlazorDashboard.Components;
using BlazorDashboard.Services;
using BlazorDashboard.Tests.TestSupport;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorDashboard.Tests.Components;

public class FxDashboardTests : BunitContext
{
    public FxDashboardTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero)));
        Services.AddSingleton(_ => new FakeFrankfurterApi().CreateClient());
        Services.AddSingleton<FakeDashboardStateStore>();
        Services.AddSingleton<IDashboardStateStore>(sp => sp.GetRequiredService<FakeDashboardStateStore>());
    }

    [Fact]
    public void StandaloneComponent_RendersFxDashboardContent()
    {
        var cut = Render<FxDashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("FX Dashboard", cut.Find("h2").TextContent);
            Assert.Contains("This dashboard uses a cookie", cut.Find(".cookie-notice").TextContent);
            Assert.Equal("false", cut.Find("section.cards").GetAttribute("aria-busy"));
        });
    }
}
