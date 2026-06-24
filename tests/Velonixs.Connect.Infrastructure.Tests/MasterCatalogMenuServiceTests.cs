using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Security;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class MasterCatalogMenuServiceTests
{
    [Fact]
    public async Task RestaurantMenuItem_UsesMasterItemNameWithRestaurantSpecificPriceAndAvailability()
    {
        await using var dbContext = CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = "phone-id"
        };

        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync();

        var service = new MenuService(dbContext);
        var masterCategory = await service.CreateMasterCategoryAsync(
            new CreateMasterMenuCategoryRequest("Pizza", DisplayOrder: 1));
        var masterItem = await service.CreateMasterItemAsync(
            new CreateMasterMenuItemRequest(
                masterCategory.Id,
                "Paneer Pizza",
                "Paneer, cheese and capsicum"));

        var restaurantCategory = await service.CreateCategoryAsync(
            restaurant.Id,
            new CreateMenuCategoryRequest("Ignored", DisplayOrder: 1, MasterCategoryId: masterCategory.Id));
        var restaurantItem = await service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(
                restaurantCategory.Id,
                masterItem.Id,
                ItemCode: 1,
                Name: "Ignored",
                Description: null,
                Price: 249,
                IsAvailable: false,
                IsActive: true));

        Assert.Equal("Pizza", restaurantCategory.Name);
        Assert.Equal(masterCategory.Id, restaurantCategory.MasterCategoryId);
        Assert.Equal("Paneer Pizza", restaurantItem.Name);
        Assert.Equal("Paneer, cheese and capsicum", restaurantItem.Description);
        Assert.Equal(249, restaurantItem.Price);
        Assert.False(restaurantItem.IsAvailable);
        Assert.Equal(masterItem.Id, restaurantItem.MasterMenuItemId);
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
}
