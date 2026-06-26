using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Api.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public ApiWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__RestaurantConnect", "Server=(localdb)\\mssqllocaldb;Database=VelonixsConnectTests;Trusted_Connection=True;");
        Environment.SetEnvironmentVariable("DataEncryption__Key", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
        Environment.SetEnvironmentVariable("Auth__SigningKey", "integration-test-signing-key-0001");
        Environment.SetEnvironmentVariable("Auth__Issuer", "velonixs-connect-tests");
        Environment.SetEnvironmentVariable("Auth__Audience", "velonixs-connect-tests");
        Environment.SetEnvironmentVariable("Auth__RequireAuthentication", "false");
        Environment.SetEnvironmentVariable("RestaurantConnect__AutoMigrateDatabase", "false");
        Environment.SetEnvironmentVariable("RestaurantConnect__SeedDemoData", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RestaurantConnectDbContext>>();
            services.AddDbContext<RestaurantConnectDbContext>(options =>
                options.UseInMemoryDatabase($"velonixs-connect-api-tests-{Guid.NewGuid()}"));
        });
    }
}
