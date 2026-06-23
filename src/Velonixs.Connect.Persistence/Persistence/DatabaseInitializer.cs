using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Configuration;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Persistence.Persistence;

public sealed class DatabaseInitializer(
    RestaurantConnectDbContext dbContext,
    IOptions<RestaurantConnectOptions> options,
    IOptions<IdentitySeedOptions> identitySeedOptions,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ILogger<DatabaseInitializer> logger)
{
    private const string FieldEncryptionMigrationId = "field-encryption-v1";
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

        await EncryptExistingSensitiveDataAsync(cancellationToken);
        await SeedRolesAsync();
        await SeedDefaultAdminAsync(cancellationToken);
    }

    private async Task EncryptExistingSensitiveDataAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.DataProtectionStates.AnyAsync(
                x => x.Id == FieldEncryptionMigrationId,
                cancellationToken))
        {
            return;
        }

        var restaurants = await dbContext.Restaurants.ToArrayAsync(cancellationToken);
        var customers = await dbContext.Customers.ToArrayAsync(cancellationToken);
        var conversations = await dbContext.Conversations.ToArrayAsync(cancellationToken);
        var orders = await dbContext.Orders.ToArrayAsync(cancellationToken);
        var messages = await dbContext.MessageLogs.ToArrayAsync(cancellationToken);

        foreach (var restaurant in restaurants)
        {
            MarkModified(restaurant, nameof(Restaurant.BusinessPhone));
            MarkModified(restaurant, nameof(Restaurant.NotificationEmail));
            MarkModified(restaurant, nameof(Restaurant.StaffWhatsAppNumber));
            MarkModified(restaurant, nameof(Restaurant.Address));
        }

        foreach (var customer in customers)
        {
            MarkModified(customer, nameof(Customer.Name));
            MarkModified(customer, nameof(Customer.LastAddress));
        }

        foreach (var conversation in conversations)
        {
            MarkModified(conversation, nameof(Conversation.WhatsAppNumber));
            MarkModified(conversation, nameof(Conversation.TempOrderJson));
        }

        foreach (var order in orders)
        {
            MarkModified(order, nameof(Order.CustomerName));
            MarkModified(order, nameof(Order.CustomerPhone));
            MarkModified(order, nameof(Order.Address));
        }

        foreach (var message in messages)
        {
            MarkModified(message, nameof(MessageLog.MessageText));
        }

        dbContext.DataProtectionStates.Add(new DataProtectionState
        {
            Id = FieldEncryptionMigrationId,
            CompletedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Existing sensitive database fields were encrypted.");
    }

    private void MarkModified<TEntity>(TEntity entity, string propertyName)
        where TEntity : class
    {
        dbContext.Entry(entity).Property(propertyName).IsModified = true;
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }
    }

    private async Task SeedDefaultAdminAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_identitySeedOptions.DefaultAdminEmail) ||
            string.IsNullOrWhiteSpace(_identitySeedOptions.DefaultAdminPassword))
        {
            return;
        }

        var email = _identitySeedOptions.DefaultAdminEmail.Trim();

        var existingUser = await userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            if (!existingUser.IsActive)
            {
                existingUser.IsActive = true;
                await userManager.UpdateAsync(existingUser);
            }

            if (!await userManager.IsInRoleAsync(existingUser, AppRoles.PlatformAdmin))
            {
                await userManager.AddToRoleAsync(existingUser, AppRoles.PlatformAdmin);
            }

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

        await userManager.AddToRoleAsync(user, AppRoles.PlatformAdmin);

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
            StaffWhatsAppNumber = "9905544835",
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
            "Seeded demo business '{BusinessName}'.",
            restaurant.Name);
    }
}
