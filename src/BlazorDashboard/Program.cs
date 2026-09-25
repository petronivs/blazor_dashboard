using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorDashboard;
using BlazorDashboard.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new FrankfurterClient(new HttpClient { BaseAddress = new Uri(FrankfurterClient.BaseUrl) }));

await builder.Build().RunAsync();
