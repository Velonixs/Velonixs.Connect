using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Security;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class PlatformAdministrationServiceConcurrencyTests
{
    [Fact]
    public async Task GetDashboardAsync_AllowsConcurrentCalls_FromSameServiceInstance()
    {
        await using var dbContext = CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "Northwind Kitchen",
            BusinessType = "Restaurant",
            WhatsAppPhoneNumberId = "phone-123",
            IsActive = true
        };
        dbContext.Restaurants.Add(restaurant);
        dbContext.MenuItems.AddRange(
            MenuItem(restaurant, "Paneer Bowl", true, true),
            MenuItem(restaurant, "Masala Dosa", true, true),
            MenuItem(restaurant, "Cold Coffee", false, true));
        dbContext.Orders.Add(new Order { RestaurantId = restaurant.Id, TotalAmount = 650m, CreatedAt = DateTimeOffset.UtcNow });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var results = await Task.WhenAll(
            service.GetDashboardAsync(),
            service.GetDashboardAsync());

        Assert.Equal(2, results.Length);
        Assert.Equal("Northwind Kitchen", results[0].Businesses.Single().Name);
        Assert.Equal(2, results[0].ActiveProductCount);
        Assert.Equal(1, results[0].TodaysOrderCount);
    }

    private static PlatformAdministrationService CreateService(RestaurantConnectDbContext dbContext)
    {
        var userStore = new UserStore<ApplicationUser, IdentityRole<Guid>, RestaurantConnectDbContext, Guid>(dbContext);
        var userManager = new UserManager<ApplicationUser>(
            userStore,
            null!,
            new PasswordHasher<ApplicationUser>(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        return new PlatformAdministrationService(dbContext, userManager);
    }

    private static RestaurantConnectDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RestaurantConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var encryption = new AesGcmFieldEncryptionService(Convert.ToBase64String(new byte[32]));
        return new RestaurantConnectDbContext(options, encryption);
    }

    private static MenuItem MenuItem(Restaurant restaurant, string name, bool isActive, bool isAvailable)
    {
        return new MenuItem
        {
            RestaurantId = restaurant.Id,
            Name = name,
            Price = 120m,
            IsActive = isActive,
            IsAvailable = isAvailable,
            Currency = "INR",
            CategoryId = Guid.NewGuid(),
            Restaurant = restaurant
        };
    }
}
