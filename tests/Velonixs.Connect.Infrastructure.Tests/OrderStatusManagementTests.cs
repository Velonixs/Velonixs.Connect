using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Security;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class OrderStatusManagementTests
{
    [Fact]
    public async Task ConfirmOrder_StoresHistoryAndNotifiesCustomerWithEstimatedTime()
    {
        await using var dbContext = CreateDbContext();
        var (_, _, order) = await SeedPendingOrderAsync(dbContext);
        var notificationService = new FakeNotificationService();
        var service = new OrderService(dbContext, notificationService);

        var result = await service.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest(OrderStatuses.Confirmed, "We are preparing it fresh.", "Staff", 30));

        Assert.NotNull(result);
        Assert.Equal(OrderStatuses.Confirmed, result.OrderStatus);
        Assert.Equal(30, result.EstimatedMinutes);
        Assert.Equal("We are preparing it fresh.", result.RestaurantComment);
        Assert.Single(result.StatusHistory);
        Assert.Equal(OrderStatuses.PendingConfirmation, result.StatusHistory.Single().PreviousStatus);
        Assert.Equal(OrderStatuses.Confirmed, result.StatusHistory.Single().NewStatus);
        Assert.Single(notificationService.CustomerMessages);
        Assert.Contains("Status: *Confirmed*", notificationService.CustomerMessages[0]);
        Assert.Contains("Estimated time: *30 minutes*", notificationService.CustomerMessages[0]);
        Assert.Equal(1, await dbContext.MessageLogs.CountAsync(x => x.Direction == MessageDirections.Outgoing));
    }

    [Fact]
    public async Task RejectOrder_RequiresReason()
    {
        await using var dbContext = CreateDbContext();
        var (_, _, order) = await SeedPendingOrderAsync(dbContext);
        var service = new OrderService(dbContext, new FakeNotificationService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateStatusAsync(
                order.Id,
                new UpdateOrderStatusRequest(OrderStatuses.Rejected, UpdatedBy: "Staff")));
    }

    [Fact]
    public async Task InvalidTransition_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var (_, _, order) = await SeedPendingOrderAsync(dbContext);
        var service = new OrderService(dbContext, new FakeNotificationService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateStatusAsync(
                order.Id,
                new UpdateOrderStatusRequest(OrderStatuses.Delivered, UpdatedBy: "Staff")));
    }

    [Fact]
    public async Task SameStatusUpdate_DoesNotDuplicateCustomerNotification()
    {
        await using var dbContext = CreateDbContext();
        var (_, _, order) = await SeedPendingOrderAsync(dbContext);
        var notificationService = new FakeNotificationService();
        var service = new OrderService(dbContext, notificationService);

        await service.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest(OrderStatuses.Confirmed, "Accepted.", "Staff", 20));
        await service.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest(OrderStatuses.Confirmed, "Accepted.", "Staff", 20));

        Assert.Single(notificationService.CustomerMessages);
        Assert.Equal(1, await dbContext.OrderStatusHistory.CountAsync());
    }

    private static async Task<(Restaurant Restaurant, Customer Customer, Order Order)> SeedPendingOrderAsync(
        RestaurantConnectDbContext dbContext)
    {
        var restaurant = new Restaurant
        {
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = "phone-id"
        };
        var customer = new Customer
        {
            Restaurant = restaurant,
            PhoneNumber = "919999999999",
            Name = "Customer"
        };
        var order = new Order
        {
            Restaurant = restaurant,
            Customer = customer,
            OrderNumber = "VRC-1001",
            CustomerName = "Customer",
            CustomerPhone = customer.PhoneNumber,
            Address = "Pickup",
            OrderStatus = OrderStatuses.PendingConfirmation,
            TotalAmount = 249
        };

        dbContext.AddRange(restaurant, customer, order);
        await dbContext.SaveChangesAsync();

        return (restaurant, customer, order);
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
        public List<string> CustomerMessages { get; } = new();

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
            CancellationToken cancellationToken = default)
        {
            CustomerMessages.Add(message);
            return Task.FromResult(new WhatsAppSendResult(true, false, Guid.NewGuid().ToString("N")));
        }
    }
}
