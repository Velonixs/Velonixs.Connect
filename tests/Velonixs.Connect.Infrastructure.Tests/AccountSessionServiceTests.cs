using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Security;
using Velonixs.Connect.Shared.Security;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class AccountSessionServiceTests
{
    [Fact]
    public async Task AuthenticateAdminAsync_AllowsOnlyActivePlatformAdmins()
    {
        await using var services = await CreateServicesAsync();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var service = new AccountSessionService(userManager);
        await CreateUserAsync(userManager, "admin@example.com", AppRoles.PlatformAdmin);
        await CreateUserAsync(userManager, "owner@example.com", AppRoles.BusinessOwner, Guid.NewGuid());

        var adminSession = await service.AuthenticateAdminAsync("admin@example.com", "Password123");
        var ownerSession = await service.AuthenticateAdminAsync("owner@example.com", "Password123");

        Assert.NotNull(adminSession);
        Assert.Contains(AppRoles.PlatformAdmin, adminSession.Roles);
        Assert.Null(ownerSession);
    }

    [Fact]
    public async Task AuthenticatePortalAsync_AllowsActiveBusinessUsersOnly()
    {
        await using var services = await CreateServicesAsync();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var service = new AccountSessionService(userManager);
        var businessId = Guid.NewGuid();
        await CreateUserAsync(userManager, "owner@example.com", AppRoles.BusinessOwner, businessId);
        await CreateUserAsync(userManager, "admin@example.com", AppRoles.PlatformAdmin);

        var ownerSession = await service.AuthenticatePortalAsync("owner@example.com", "Password123");
        var adminSession = await service.AuthenticatePortalAsync("admin@example.com", "Password123");

        Assert.NotNull(ownerSession);
        Assert.Equal(businessId, ownerSession.BusinessId);
        Assert.Null(adminSession);
    }

    [Fact]
    public async Task ValidatePortalSessionAsync_RejectsBusinessMismatch()
    {
        await using var services = await CreateServicesAsync();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var service = new AccountSessionService(userManager);
        var businessId = Guid.NewGuid();
        var user = await CreateUserAsync(userManager, "staff@example.com", AppRoles.Staff, businessId);

        var matching = await service.ValidatePortalSessionAsync(user.Id, businessId);
        var mismatched = await service.ValidatePortalSessionAsync(user.Id, Guid.NewGuid());

        Assert.NotNull(matching);
        Assert.Null(mismatched);
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string role,
        Guid? businessId = null,
        bool isActive = true)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = email,
            BusinessId = businessId,
            IsActive = isActive
        };

        var result = await userManager.CreateAsync(user, "Password123");
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Description)));

        result = await userManager.AddToRoleAsync(user, role);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Description)));

        return user;
    }

    private static async Task<ServiceProvider> CreateServicesAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFieldEncryptionService, PassThroughEncryptionService>();
        services.AddDbContext<RestaurantConnectDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<RestaurantConnectDbContext>();

        var provider = services.BuildServiceProvider();
        var dbContext = provider.GetRequiredService<RestaurantConnectDbContext>();
        dbContext.Restaurants.Add(new Restaurant
        {
            Id = Guid.NewGuid(),
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = Guid.NewGuid().ToString("N")
        });
        await dbContext.SaveChangesAsync();

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        return provider;
    }

    private sealed class PassThroughEncryptionService : IFieldEncryptionService
    {
        public string Encrypt(string plaintext) => plaintext;
        public string Decrypt(string protectedValue) => protectedValue;
    }
}
