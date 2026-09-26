using System.Globalization;
using System.Text.Json;
using BlazorDashboard.Pages;
using BlazorDashboard.Services;
using BlazorDashboard.Tests.TestSupport;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorDashboard.Tests.Pages;

public class HomeCookiePersistenceTests : BunitContext
{
    private readonly FakeFrankfurterApi api = new();

    public HomeCookiePersistenceTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<string?>("dashboardCookies.get").SetResult(null);
        JSInterop.SetupVoid("dashboardCookies.set");
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero)));
        Services.AddSingleton(_ => api.CreateClient());
        Services.AddScoped<IDashboardStateStore, CookieDashboardStateStore>();
    }

    [Fact]
    public void ChangingOnlyAmount_DoesNotPersistAbsoluteDefaultDates()
    {
        var cut = Render<Home>();
        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("section.cards").GetAttribute("aria-busy")));

        cut.Find("input[type=number]").Change("250");

        var saved = JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "dashboardCookies.set")
            .Select(invocation => invocation.Arguments[0]?.ToString())
            .Last();

        using var document = JsonDocument.Parse(saved!);
        Assert.False(document.RootElement.TryGetProperty("from", out _));
        Assert.False(document.RootElement.TryGetProperty("to", out _));
    }
}
