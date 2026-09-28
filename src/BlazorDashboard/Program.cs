using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorDashboard;
using BlazorDashboard.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IDashboardStateStore, CookieDashboardStateStore>();
builder.Services.AddScoped(_ => new FrankfurterClient(new HttpClient { BaseAddress = new Uri(FrankfurterClient.BaseUrl) }));
builder.Services.AddScoped(_ => new WorldBankClient(new HttpClient { BaseAddress = new Uri(WorldBankClient.BaseUrl) }));

await builder.Build().RunAsync();
