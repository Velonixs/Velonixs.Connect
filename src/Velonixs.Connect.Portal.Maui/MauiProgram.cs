using System.Reflection;
using Microsoft.Extensions.Logging;
using Velonixs.Connect.Api.Client;
using Velonixs.Connect.Mobile.Abstractions;
using Velonixs.Connect.Portal.Maui.Services;

namespace Velonixs.Connect.Portal.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<ISecureTokenStorage, MauiSecureTokenStorage>();
        builder.Services.AddSingleton<IApiTokenStore, SecureApiTokenStoreAdapter>();
        builder.Services.AddSingleton<IMobileConnectivityService, MauiConnectivityService>();
        builder.Services.AddSingleton<MauiLifecycleService>();
        builder.Services.AddSingleton<IMobileLifecycleService>(serviceProvider =>
            serviceProvider.GetRequiredService<MauiLifecycleService>());
        builder.Services.AddSingleton<IMobileLocalNotificationService, AndroidLocalNotificationService>();
        builder.Services.AddSingleton<IMobileAlertSoundService, MauiAlertSoundService>();

        builder.Services.AddVelonixsConnectApiClient(options =>
        {
            options.BaseAddress = GetApiBaseAddress();
        });

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static Uri GetApiBaseAddress()
    {
        var baseAddress = typeof(MauiProgram)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "ApiBaseAddress")
            ?.Value;

        return new Uri(baseAddress ?? "https://10.0.2.2:7011/", UriKind.Absolute);
    }
}
