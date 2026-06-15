using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velonixs.Restaurant.Domain.Entities;
using Velonixs.Restaurant.Infrastructure.Configuration;

namespace Velonixs.Restaurant.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    RestaurantConnectDbContext dbContext,
    IOptions<RestaurantConnectOptions> options,
    ILogger<DatabaseInitializer> logger)
{
    private readonly RestaurantConnectOptions _options = options.Value;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_options.AutoMigrateDatabase)
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        if (_options.SeedDemoData)
        {
            await SeedDemoDataAsync(cancellationToken);
        }
    }

    private async Task SeedDemoDataAsync(CancellationToken cancellationToken)
    {
        var demoPhoneNumberId = string.IsNullOrWhiteSpace(_options.DemoWhatsAppPhoneNumberId)
            ? "1196816620181240"
            : _options.DemoWhatsAppPhoneNumberId;

        if (await dbContext.Restaurants.AnyAsync(x => x.WhatsAppPhoneNumberId == demoPhoneNumberId, cancellationToken))
        {
            return;
        }

        var restaurant = new Domain.Entities.Restaurant
        {
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = demoPhoneNumberId,
            BusinessPhone = "+918055572840",
            NotificationEmail = "info@velonixs.com",
            StaffWhatsAppNumber = null,
            Address = "Rajarhat, Kolkata, West Bengal, India",
            IsActive = true
        };

        var pizza = new MenuCategory
        {
            Restaurant = restaurant,
            Name = "Pizza",
            DisplayOrder = 1
        };

        var burger = new MenuCategory
        {
            Restaurant = restaurant,
            Name = "Burger",
            DisplayOrder = 2
        };

        var mainCourse = new MenuCategory
        {
            Restaurant = restaurant,
            Name = "Main Course",
            DisplayOrder = 3
        };

        dbContext.Restaurants.Add(restaurant);
        dbContext.MenuCategories.AddRange(pizza, burger, mainCourse);
        dbContext.MenuItems.AddRange(
            new MenuItem
            {
                Restaurant = restaurant,
                Category = pizza,
                ItemCode = 1,
                Name = "Margherita Pizza",
                Price = 199
            },
            new MenuItem
            {
                Restaurant = restaurant,
                Category = pizza,
                ItemCode = 2,
                Name = "Paneer Pizza",
                Price = 249
            },
            new MenuItem
            {
                Restaurant = restaurant,
                Category = burger,
                ItemCode = 3,
                Name = "Veg Burger",
                Price = 99
            },
            new MenuItem
            {
                Restaurant = restaurant,
                Category = burger,
                ItemCode = 4,
                Name = "Cheese Burger",
                Price = 129
            },
            new MenuItem
            {
                Restaurant = restaurant,
                Category = mainCourse,
                ItemCode = 5,
                Name = "Paneer Butter Masala",
                Price = 220
            },
            new MenuItem
            {
                Restaurant = restaurant,
                Category = mainCourse,
                ItemCode = 6,
                Name = "Veg Biryani",
                Price = 180
            });

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded demo restaurant '{RestaurantName}' with WhatsApp phone number id '{PhoneNumberId}'.",
            restaurant.Name,
            restaurant.WhatsAppPhoneNumberId);
    }
}
