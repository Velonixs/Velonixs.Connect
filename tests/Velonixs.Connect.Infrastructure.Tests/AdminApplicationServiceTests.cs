using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Security;
using Velonixs.Connect.Shared.Security;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class AdminApplicationServiceTests
{
    [Fact]
    public async Task AdminService_CreatesRestaurantWithOwnerAccount()
    {
        await using var services = await CreateServicesAsync();
        var dbContext = services.GetRequiredService<RestaurantConnectDbContext>();
        var adminService = CreateAdminService(services);

        var result = await adminService.CreateRestaurantWithOwnerAsync(
            new CreateRestaurantWithOwnerRequest(
                new CreateRestaurantRequest(
                    "99 Restaurant",
                    BusinessTypes.Restaurant,
                    "phone-id",
                    BusinessPhone: null,
                    NotificationEmail: null,
                    StaffWhatsAppNumber: null,
                    Address: null,
                    CgstPercent: 2.5m,
                    SgstPercent: 2.5m),
                new CreateOwnerUserRequest(
                    "Owner",
                    "owner@example.com",
                    "Password123")));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.NotNull(result.Restaurant);
        Assert.NotNull(result.Owner);
        Assert.Equal(AppRoles.BusinessOwner, result.Owner.Role);
        Assert.Equal(result.Restaurant.Id, result.Owner.BusinessId);
        Assert.Equal(1, await dbContext.Restaurants.CountAsync());
    }

    [Fact]
    public async Task AdminService_DashboardAggregatesRestaurantMetrics()
    {
        await using var services = await CreateServicesAsync();
        var dbContext = services.GetRequiredService<RestaurantConnectDbContext>();
        var adminService = CreateAdminService(services);
        var restaurant = new Restaurant
        {
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = "phone-id"
        };
        var customer = new Customer
        {
            Restaurant = restaurant,
            PhoneNumber = "919999999999"
        };
        dbContext.AddRange(
            restaurant,
            customer,
            new MenuCategory
            {
                Restaurant = restaurant,
                Name = "Meals"
            },
            new Order
            {
                Restaurant = restaurant,
                Customer = customer,
                OrderNumber = "VRC-3001",
                CustomerName = "Customer",
                CustomerPhone = customer.PhoneNumber,
                Address = "Pickup",
                OrderStatus = OrderStatuses.Delivered,
                TotalAmount = 300
            });
        await dbContext.SaveChangesAsync();

        var dashboard = await adminService.GetPlatformDashboardAsync();

        Assert.Equal(1, dashboard.TotalRestaurants);
        Assert.Equal(1, dashboard.ActiveRestaurants);
        Assert.Equal(1, dashboard.TotalOrders);
        Assert.Equal(300, dashboard.TotalRevenue);
        Assert.Single(dashboard.Restaurants);
    }

    [Fact]
    public async Task MenuService_MasterCatalogMutationsProtectInUseRecords()
    {
        await using var services = await CreateServicesAsync();
        var dbContext = services.GetRequiredService<RestaurantConnectDbContext>();
        var menuService = new MenuService(dbContext);
        var category = await menuService.CreateMasterCategoryAsync(
            new CreateMasterMenuCategoryRequest("Meals", DisplayOrder: 1));
        var item = await menuService.CreateMasterItemAsync(
            new CreateMasterMenuItemRequest(category.Id, "Paneer Meal", "Lunch", true));

        var updatedItem = await menuService.UpdateMasterItemAsync(
            item.Id,
            new CreateMasterMenuItemRequest(category.Id, "Paneer Thali", "Dinner", true));

        Assert.NotNull(updatedItem);
        Assert.Equal("Paneer Thali", updatedItem.Name);

        var restaurant = new Restaurant
        {
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = "restaurant-phone-id"
        };
        var restaurantCategory = new MenuCategory
        {
            Restaurant = restaurant,
            MasterCategoryId = category.Id,
            Name = category.Name
        };
        dbContext.AddRange(restaurant, restaurantCategory);
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            menuService.DeleteMasterCategoryAsync(category.Id));
    }

    private static AdminService CreateAdminService(ServiceProvider services)
    {
        var dbContext = services.GetRequiredService<RestaurantConnectDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var restaurantService = new RestaurantService(dbContext);
        var menuService = new MenuService(dbContext);
        var orderService = new OrderService(dbContext, new FakeNotificationService(), new NoOpOrderRealtimeNotifier());
        var customerService = new CustomerService(dbContext);
        var staffService = new StaffService(userManager);

        return new AdminService(
            dbContext,
            userManager,
            restaurantService,
            menuService,
            orderService,
            customerService,
            staffService);
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

    private sealed class FakeNotificationService : INotificationService
    {
        public Task NotifyOrderConfirmedAsync(
            Restaurant restaurant,
            Customer customer,
            Order order,
            IReadOnlyCollection<OrderItem> items,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NotifyStaffHandoverAsync(
            Restaurant restaurant,
            Customer customer,
            string customerMessage,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<WhatsAppSendResult> NotifyCustomerOrderStatusAsync(
            Restaurant restaurant,
            Customer customer,
            Order order,
            string message,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WhatsAppSendResult(true, false, Guid.NewGuid().ToString("N")));
    }
}
