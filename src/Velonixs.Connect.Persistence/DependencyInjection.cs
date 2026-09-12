using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Velonixs.Connect.Persistence.Configuration;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Security;

namespace Velonixs.Connect.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
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

        var encryptionKey = configuration[$"{DataEncryptionOptions.SectionName}:Key"]
            ?? configuration["DATA_ENCRYPTION_KEY"];
        services.Configure<DataEncryptionOptions>(options =>
        {
            options.Key = encryptionKey ?? string.Empty;
        });
        services.AddSingleton<IFieldEncryptionService, AesGcmFieldEncryptionService>();

        services.AddDbContextFactory<RestaurantConnectDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.Configure<RestaurantConnectOptions>(configuration.GetSection("RestaurantConnect"));
        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}
