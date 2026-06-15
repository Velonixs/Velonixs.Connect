using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Velonixs.Restaurant.Application.Abstractions;
using Velonixs.Restaurant.Infrastructure.Configuration;
using Velonixs.Restaurant.Infrastructure.Persistence;
using Velonixs.Restaurant.Infrastructure.Services;

namespace Velonixs.Restaurant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("RestaurantConnect")
            ?? configuration["DATABASE_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is not configured. Set ConnectionStrings:RestaurantConnect or DATABASE_CONNECTION_STRING.");
        }

        services.AddDbContext<RestaurantConnectDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.Configure<RestaurantConnectOptions>(configuration.GetSection("RestaurantConnect"));
        services.Configure<WhatsAppOptions>(options =>
        {
            configuration.GetSection("WhatsApp").Bind(options);
            options.AccessToken ??= configuration["WHATSAPP_ACCESS_TOKEN"];
            options.VerifyToken ??= configuration["WHATSAPP_VERIFY_TOKEN"];
            options.AppSecret ??= configuration["META_APP_SECRET"];
            options.ApiVersion = configuration["WHATSAPP_API_VERSION"] ?? options.ApiVersion;
        });
        services.Configure<SmtpOptions>(options =>
        {
            configuration.GetSection("Smtp").Bind(options);
            options.Host ??= configuration["SMTP_HOST"];
            options.Username ??= configuration["SMTP_USERNAME"];
            options.Password ??= configuration["SMTP_PASSWORD"];
            options.DefaultFromEmail ??= configuration["DEFAULT_FROM_EMAIL"];
            options.AdminEmail ??= configuration["ADMIN_EMAIL"];

            if (int.TryParse(configuration["SMTP_PORT"], out var smtpPort))
            {
                options.Port = smtpPort;
            }
        });

        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddHttpClient<IWhatsAppMessageSender, WhatsAppCloudMessageSender>();

        return services;
    }
}
