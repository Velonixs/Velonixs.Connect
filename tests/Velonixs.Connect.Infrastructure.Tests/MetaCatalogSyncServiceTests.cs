using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Security;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class MetaCatalogSyncServiceTests
{
    [Fact]
    public async Task QueueProductSyncAsync_PersistsPendingItem_AndProcessesIt()
    {
        await using var dbContext = CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "Demo Bistro",
            WhatsAppPhoneNumberId = "phone-id"
        };

        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync();

        var menuItem = new MenuItem
        {
            Restaurant = restaurant,
            Category = new MenuCategory
            {
                Restaurant = restaurant,
                Name = "Pizza"
            },
            ItemCode = 1,
            Name = "Margherita Pizza",
            Price = 199
        };

        dbContext.MenuItems.Add(menuItem);
        dbContext.MetaCatalogSettings.Add(new MetaCatalogSetting
        {
            BusinessId = restaurant.Id,
            CatalogId = "catalog-id",
            AccessTokenEncrypted = "token"
        });
        await dbContext.SaveChangesAsync();

        var service = new MetaCatalogSyncService(
            dbContext,
            new HttpClient(new StubHandler()),
            Options.Create(new MetaCatalogOptions { DisableSending = false }),
            NullLogger<MetaCatalogSyncService>.Instance);

        await service.QueueProductSyncAsync(restaurant.Id, menuItem.Id, "create");

        var queued = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        Assert.Equal("Pending", queued.Status);
        Assert.Equal("create", queued.EventType);

        await service.ProcessPendingAsync();

        var updated = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        Assert.NotEqual("Pending", updated.Status);
    }

    private static RestaurantConnectDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RestaurantConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new RestaurantConnectDbContext(options, new PassThroughEncryptionService());
    }

    private sealed class PassThroughEncryptionService : IFieldEncryptionService
    {
        public string Encrypt(string plaintext) => plaintext;
        public string Decrypt(string protectedValue) => protectedValue;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }
}
