using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SamlSignatureValidationApp;
using System.Net.Http.Json;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Prefer appsettings.json served from wwwroot for base URL; fallback to env var or default base
var configHttp = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
var appSettings = await configHttp.GetFromJsonAsync<AppClientSettings>("appsettings.json");
var serverApiBase = appSettings?.ServerApiBaseUrl
    ?? builder.Configuration["SERVERAPI_BASEURL"]
    ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(serverApiBase) });

await builder.Build().RunAsync();

public class AppClientSettings
{
    public string? ServerApiBaseUrl { get; set; }
}
