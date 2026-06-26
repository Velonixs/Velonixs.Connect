using Velonixs.Connect.Application.Abstractions;

namespace Velonixs.Connect.Api.Client.Tests;

public sealed class OrderRealtimeEventDeduplicatorTests
{
    [Fact]
    public void TryAccept_ReturnsFalse_ForDuplicateEventId()
    {
        var deduplicator = new OrderRealtimeEventDeduplicator();
        var orderEvent = CreateEvent("event-1");

        Assert.True(deduplicator.TryAccept(orderEvent));
        Assert.False(deduplicator.TryAccept(orderEvent));
    }

    [Fact]
    public void TryAccept_ExpiresOldEvents_WhenCapacityIsExceeded()
    {
        var deduplicator = new OrderRealtimeEventDeduplicator(capacity: 1);
        var firstEvent = CreateEvent("event-1");
        var secondEvent = CreateEvent("event-2");

        Assert.True(deduplicator.TryAccept(firstEvent));
        Assert.True(deduplicator.TryAccept(secondEvent));
        Assert.True(deduplicator.TryAccept(firstEvent));
    }

    private static OrderRealtimeEvent CreateEvent(string eventId) =>
        new(
            eventId,
            "status_changed",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ORD-1001",
            "confirmed",
            250,
            DateTimeOffset.UtcNow);
}
