using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Velonixs.Connect.Admin.Blazor;
using Velonixs.Connect.Api.Client;
using Velonixs.Connect.Portal.Components.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped<IApiTokenStore, BrowserApiTokenStore>();
builder.Services.AddVelonixsConnectApiClient(options =>
{
    options.BaseAddress = new Uri(apiBaseAddress, UriKind.Absolute);
});

await builder.Build().RunAsync();
