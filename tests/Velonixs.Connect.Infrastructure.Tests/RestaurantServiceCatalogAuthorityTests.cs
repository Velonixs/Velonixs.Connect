using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Services;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class RestaurantServiceCatalogAuthorityTests
{
    [Fact]
    public async Task CreateRestaurantAsync_IgnoresLegacyCatalogId()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var service = new RestaurantService(dbContext);

        var restaurant = await service.CreateRestaurantAsync(
            new CreateRestaurantRequest(
                "Catalog Authority Cafe",
                "Restaurant",
                "phone-create-catalog-authority",
                null,
                null,
                null,
                null,
                WhatsAppCatalogId: "unreconciled-catalog"));

        Assert.Null(restaurant.WhatsAppCatalogId);
        Assert.Null(await dbContext.Restaurants
            .Where(x => x.Id == restaurant.Id)
            .Select(x => x.WhatsAppCatalogId)
            .SingleAsync());
        Assert.Empty(await dbContext.MetaCatalogSettings.ToArrayAsync());
    }

    [Fact]
    public async Task UpdateRestaurantAsync_PreservesCatalogMirrorWhenLegacyCatalogIdIsSupplied()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "Catalog Authority Cafe",
            WhatsAppPhoneNumberId = "phone-update-catalog-authority",
            WhatsAppCatalogId = "authoritative-catalog"
        };
        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync();

        var service = new RestaurantService(dbContext);
        var updated = await service.UpdateRestaurantAsync(
            restaurant.Id,
            new UpdateRestaurantRequest(
                "Updated Catalog Authority Cafe",
                "Restaurant",
                restaurant.WhatsAppPhoneNumberId,
                null,
                null,
                null,
                null,
                0,
                0,
                true,
                "unreconciled-catalog"));

        Assert.NotNull(updated);
        Assert.Equal("authoritative-catalog", updated!.WhatsAppCatalogId);
        Assert.Equal("authoritative-catalog", await dbContext.Restaurants
            .Where(x => x.Id == restaurant.Id)
            .Select(x => x.WhatsAppCatalogId)
            .SingleAsync());
    }
}
