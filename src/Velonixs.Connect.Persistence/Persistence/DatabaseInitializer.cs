using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Configuration;
using Velonixs.Connect.Persistence.Identity;

namespace Velonixs.Connect.Persistence.Persistence;

public sealed class DatabaseInitializer(
    RestaurantConnectDbContext dbContext,
    IOptions<RestaurantConnectOptions> options,
    IOptions<IdentitySeedOptions> identitySeedOptions,
    UserManager<ApplicationUser> userManager,
    ILogger<DatabaseInitializer> logger)
{
    private readonly RestaurantConnectOptions _options = options.Value;
    private readonly IdentitySeedOptions _identitySeedOptions = identitySeedOptions.Value;

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

        await SeedDefaultAdminAsync(cancellationToken);
    }

    private async Task SeedDefaultAdminAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_identitySeedOptions.DefaultAdminEmail) ||
            string.IsNullOrWhiteSpace(_identitySeedOptions.DefaultAdminPassword))
        {
            return;
        }

        var email = _identitySeedOptions.DefaultAdminEmail.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(_identitySeedOptions.DefaultAdminDisplayName)
                ? email
                : _identitySeedOptions.DefaultAdminDisplayName.Trim()
        };

        var result = await userManager.CreateAsync(user, _identitySeedOptions.DefaultAdminPassword);

        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Default admin user was not seeded. Errors={Errors}",
                string.Join("; ", result.Errors.Select(x => x.Description)));
            return;
        }

        logger.LogInformation("Seeded default admin user '{Email}'.", email);
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
            BusinessType = BusinessTypes.Restaurant,
            WhatsAppPhoneNumberId = demoPhoneNumberId,
            BusinessPhone = "+918055572840",
            NotificationEmail = "info@velonixs.com",
            StaffWhatsAppNumber = "8080225080",
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
