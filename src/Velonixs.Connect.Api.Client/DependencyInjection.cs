using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Velonixs.Connect.Api.Client;

public static class DependencyInjection
{
    public static IServiceCollection AddVelonixsConnectApiClient(
        this IServiceCollection services,
        Action<VelonixsConnectApiClientOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IApiTokenStore, InMemoryApiTokenStore>();
        services.TryAddScoped<IOrderRealtimeClient, OrderRealtimeClient>();

        services.AddHttpClient<IVelonixsConnectApiClient, VelonixsConnectApiClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<VelonixsConnectApiClientOptions>>().Value;

            if (options.BaseAddress is not null)
            {
                client.BaseAddress = options.BaseAddress;
            }

            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 120));
        });

        return services;
    }
}
