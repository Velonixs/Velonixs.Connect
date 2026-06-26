using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Security;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class PortalApplicationServiceTests
{
    [Fact]
    public async Task RestaurantDashboard_ReturnsOrderMenuAndCustomerSummary()
    {
        await using var dbContext = CreateDbContext();
        var (restaurant, customer, menuItem) = await SeedRestaurantMenuAndCustomerAsync(dbContext);
        dbContext.Orders.Add(new Order
        {
            Restaurant = restaurant,
            Customer = customer,
            OrderNumber = "VRC-2001",
            CustomerName = "Customer",
            CustomerPhone = customer.PhoneNumber,
            Address = "Pickup",
            OrderStatus = OrderStatuses.PendingConfirmation,
            TotalAmount = 450,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var dashboardService = new DashboardService(
            new RestaurantService(dbContext),
            new MenuService(dbContext),
            new OrderService(dbContext, new FakeNotificationService(), new NoOpOrderRealtimeNotifier()),
            new CustomerService(dbContext));

        var dashboard = await dashboardService.GetRestaurantDashboardAsync(restaurant.Id);

        Assert.NotNull(dashboard);
        Assert.Equal(restaurant.Id, dashboard.Restaurant.Id);
        Assert.Single(dashboard.RecentOrders);
        Assert.Equal(1, dashboard.PendingOrderCount);
        Assert.Equal(1, dashboard.TodayOrderCount);
        Assert.Equal(450, dashboard.TodayRevenue);
        Assert.Equal(1, dashboard.AvailableItemCount);
        Assert.Single(dashboard.RecentCustomers);
        Assert.Equal(menuItem.Id, dashboard.Menu?.Items.Single().Id);
    }

    [Fact]
    public async Task CustomerService_UpdatesCustomerAndCanIncludeOrderCount()
    {
        await using var dbContext = CreateDbContext();
        var (restaurant, customer, _) = await SeedRestaurantMenuAndCustomerAsync(dbContext);
        dbContext.Orders.Add(new Order
        {
            Restaurant = restaurant,
            Customer = customer,
            OrderNumber = "VRC-2002",
            CustomerName = "Customer",
            CustomerPhone = customer.PhoneNumber,
            Address = "Pickup",
            OrderStatus = OrderStatuses.Delivered,
            TotalAmount = 120
        });
        await dbContext.SaveChangesAsync();
        var service = new CustomerService(dbContext);

        var updated = await service.UpdateCustomerAsync(
            customer.Id,
            new UpdateCustomerRequest("Updated Customer", "Updated Address"));
        var customers = await service.GetRestaurantCustomersAsync(restaurant.Id, includeOrderCount: true);

        Assert.NotNull(updated);
        Assert.Equal("Updated Customer", updated.Name);
        Assert.Equal("Updated Address", updated.LastAddress);
        Assert.Equal(1, customers.Single().OrderCount);
    }

    [Fact]
    public async Task MenuService_UpdatesAvailabilityAndProtectsOrderHistoryOnDelete()
    {
        await using var dbContext = CreateDbContext();
        var (restaurant, customer, menuItem) = await SeedRestaurantMenuAndCustomerAsync(dbContext);
        var order = new Order
        {
            Restaurant = restaurant,
            Customer = customer,
            OrderNumber = "VRC-2003",
            CustomerName = "Customer",
            CustomerPhone = customer.PhoneNumber,
            Address = "Pickup",
            OrderStatus = OrderStatuses.Delivered
        };
        dbContext.Orders.Add(order);
        dbContext.OrderItems.Add(new OrderItem
        {
            Order = order,
            MenuItem = menuItem,
            ItemName = menuItem.Name,
            UnitPrice = menuItem.Price,
            Quantity = 1,
            LineTotal = menuItem.Price
        });
        await dbContext.SaveChangesAsync();
        var service = new MenuService(dbContext);

        var unavailable = await service.UpdateItemAvailabilityAsync(menuItem.Id, false);

        Assert.NotNull(unavailable);
        Assert.False(unavailable.IsAvailable);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteItemAsync(menuItem.Id));
    }

    [Fact]
    public async Task RestaurantService_UpdatesTaxAndAvailability()
    {
        await using var dbContext = CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = "phone-id"
        };
        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync();
        var service = new RestaurantService(dbContext);

        var taxUpdated = await service.UpdateRestaurantTaxSettingAsync(restaurant.Id, 2.5m, 2.5m);
        var availabilityUpdated = await service.UpdateRestaurantAvailabilityAsync(restaurant.Id, false);

        Assert.NotNull(taxUpdated);
        Assert.Equal(2.5m, taxUpdated.CgstPercent);
        Assert.Equal(2.5m, taxUpdated.SgstPercent);
        Assert.NotNull(availabilityUpdated);
        Assert.False(availabilityUpdated.IsActive);
    }

    private static async Task<(Restaurant Restaurant, Customer Customer, MenuItem MenuItem)> SeedRestaurantMenuAndCustomerAsync(
        RestaurantConnectDbContext dbContext)
    {
        var restaurant = new Restaurant
        {
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = Guid.NewGuid().ToString("N")
        };
        var category = new MenuCategory
        {
            Restaurant = restaurant,
            Name = "Meals",
            DisplayOrder = 1,
            IsActive = true
        };
        var menuItem = new MenuItem
        {
            Restaurant = restaurant,
            Category = category,
            ItemCode = 1,
            Name = "Paneer Meal",
            Price = 250,
            IsActive = true,
            IsAvailable = true
        };
        var customer = new Customer
        {
            Restaurant = restaurant,
            PhoneNumber = "919999999999",
            Name = "Customer",
            LastAddress = "Pickup"
        };

        dbContext.AddRange(restaurant, category, menuItem, customer);
        await dbContext.SaveChangesAsync();

        return (restaurant, customer, menuItem);
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
