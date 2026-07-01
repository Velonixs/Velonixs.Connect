using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence;
using Velonixs.Connect.Persistence.Configuration;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.Configure<AuthOptions>(configuration.GetSection("Auth"));
        services.Configure<IdentitySeedOptions>(configuration.GetSection("Auth"));
        services.Configure<WhatsAppOptions>(options =>
        {
            configuration.GetSection("WhatsApp").Bind(options);
            options.AccessToken = PreferConfiguredSecret(configuration["WHATSAPP_ACCESS_TOKEN"], options.AccessToken);
            options.VerifyToken = PreferConfiguredSecret(configuration["WHATSAPP_VERIFY_TOKEN"], options.VerifyToken);
            options.AppSecret = PreferConfiguredSecret(configuration["META_APP_SECRET"], options.AppSecret);
            options.ApiVersion = PreferConfiguredSecret(configuration["WHATSAPP_API_VERSION"], options.ApiVersion) ?? options.ApiVersion;

            if (bool.TryParse(configuration["WHATSAPP_DISABLE_SENDING"], out var disableSending))
            {
                options.DisableSending = disableSending;
            }

            if (int.TryParse(configuration["WHATSAPP_SEND_TIMEOUT_SECONDS"], out var sendTimeoutSeconds))
            {
                options.SendTimeoutSeconds = sendTimeoutSeconds;
            }
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

        var authOptions = configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();

        if (string.IsNullOrWhiteSpace(authOptions.SigningKey) || authOptions.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Auth signing key is not configured or is too short. Set Auth:SigningKey to at least 32 characters.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<RestaurantConnectDbContext>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = authOptions.Issuer,
                    ValidAudience = authOptions.Audience,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();

        services.AddScoped<IRestaurantService, RestaurantService>();
        services.AddScoped<IBusinessService, BusinessService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<MenuSearchService>();
        services.AddScoped<FreeTextOrderParser>();
        services.AddScoped<OrderingCartService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuthTokenService, AuthTokenService>();
        services.AddHttpClient<IWhatsAppMessageSender, WhatsAppCloudMessageSender>(client =>
        {
            var timeoutSeconds = Math.Clamp(configuration.GetSection("WhatsApp").Get<WhatsAppOptions>()?.SendTimeoutSeconds ?? 10, 1, 30);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });

        return services;
    }

    private static string? PreferConfiguredSecret(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred)
            ? preferred
            : string.IsNullOrWhiteSpace(fallback)
                ? null
                : fallback;
}
