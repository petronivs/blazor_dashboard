using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorDashboard.Services;

public sealed class CookieDashboardStateStore(IJSRuntime jsRuntime, NavigationManager navigation) : IDashboardStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<DashboardState?> LoadAsync()
    {
        try
        {
            var json = await jsRuntime.InvokeAsync<string?>("dashboardCookies.get");
            return json is null ? null : JsonSerializer.Deserialize<DashboardState>(json, JsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async ValueTask SaveAsync(DashboardState state)
    {
        try
        {
            var path = new Uri(navigation.BaseUri).AbsolutePath;
            await jsRuntime.InvokeVoidAsync("dashboardCookies.set", JsonSerializer.Serialize(state, JsonOptions), path);
        }
        catch (Exception)
        {
        }
    }
}
