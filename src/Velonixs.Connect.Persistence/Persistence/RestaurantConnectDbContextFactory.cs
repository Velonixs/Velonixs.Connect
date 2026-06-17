using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Velonixs.Connect.Persistence.Persistence;

public sealed class RestaurantConnectDbContextFactory : IDesignTimeDbContextFactory<RestaurantConnectDbContext>
{
    public RestaurantConnectDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? "Data Source=ANIRUDDHA;Initial Catalog=VelonixsRSDB;User ID=sa;Password=abcd@1234;Connect Timeout=30;Encrypt=False;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False;Command Timeout=30";

        var options = new DbContextOptionsBuilder<RestaurantConnectDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new RestaurantConnectDbContext(options);
    }
}
