using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Velonixs.Connect.Persistence.Security;

namespace Velonixs.Connect.Persistence.Persistence;

public sealed class RestaurantConnectDbContextFactory : IDesignTimeDbContextFactory<RestaurantConnectDbContext>
{
    public RestaurantConnectDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__RestaurantConnect")
            ?? Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__RestaurantConnect before running EF design-time commands.");
        var encryptionKey =
            Environment.GetEnvironmentVariable("DataEncryption__Key")
            ?? Environment.GetEnvironmentVariable("DATA_ENCRYPTION_KEY")
            ?? throw new InvalidOperationException(
                "Set DataEncryption__Key before running EF design-time commands.");

        var options = new DbContextOptionsBuilder<RestaurantConnectDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new RestaurantConnectDbContext(options, new AesGcmFieldEncryptionService(encryptionKey));
    }
}
