using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Security;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class NotificationQueryServiceTests
{
    [Fact]
    public async Task GetRecentNotificationsAsync_ReturnsRestaurantScopedRecentLogs()
    {
        await using var dbContext = CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = "phone-id"
        };
        var otherRestaurant = new Restaurant
        {
            Name = "Other Restaurant",
            WhatsAppPhoneNumberId = "other-phone-id"
        };
        dbContext.AddRange(
            restaurant,
            otherRestaurant,
            new MessageLog
            {
                Restaurant = restaurant,
                Direction = MessageDirections.Outgoing,
                MessageText = "First",
                Status = MessageStatuses.Sent,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
            },
            new MessageLog
            {
                Restaurant = restaurant,
                Direction = MessageDirections.Outgoing,
                MessageText = "Second",
                Status = MessageStatuses.Sent,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new MessageLog
            {
                Restaurant = otherRestaurant,
                Direction = MessageDirections.Outgoing,
                MessageText = "Other",
                Status = MessageStatuses.Sent,
                CreatedAt = DateTimeOffset.UtcNow
            });
        await dbContext.SaveChangesAsync();
        var service = new NotificationQueryService(dbContext);

        var notifications = await service.GetRecentNotificationsAsync(restaurant.Id);

        Assert.Equal(2, notifications.Count);
        Assert.Equal("Second", notifications.First().MessageText);
        Assert.All(notifications, x => Assert.Equal(restaurant.Id, x.RestaurantId));
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
