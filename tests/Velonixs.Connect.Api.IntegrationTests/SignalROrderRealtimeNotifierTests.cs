using Microsoft.AspNetCore.SignalR;
using Velonixs.Connect.Api.Hubs;
using Velonixs.Connect.Application.Abstractions;

namespace Velonixs.Connect.Api.IntegrationTests;

public sealed class SignalROrderRealtimeNotifierTests
{
    [Fact]
    public async Task NotifyAsync_sends_order_event_to_restaurant_group()
    {
        var restaurantId = Guid.NewGuid();
        var orderEvent = new OrderRealtimeEvent(
            "event-1",
            "status_changed",
            restaurantId,
            Guid.NewGuid(),
            "ORD-1001",
            "Confirmed",
            125.50m,
            DateTimeOffset.UtcNow);
        var clients = new RecordingHubClients();
        var notifier = new SignalROrderRealtimeNotifier(new RecordingHubContext(clients));

        await notifier.NotifyAsync(orderEvent);

        Assert.Equal(OrdersHub.RestaurantGroup(restaurantId), clients.LastGroupName);
        Assert.Same(orderEvent, clients.GroupClient.LastOrderEvent);
        Assert.Equal(1, clients.GroupClient.OrderChangedCallCount);
    }

    private sealed class RecordingHubContext(RecordingHubClients clients)
        : IHubContext<OrdersHub, IOrdersHubClient>
    {
        public IHubClients<IOrdersHubClient> Clients => clients;

        public IGroupManager Groups { get; } = new NoOpGroupManager();
    }

    private sealed class RecordingHubClients : IHubClients<IOrdersHubClient>
    {
        public RecordingOrdersHubClient GroupClient { get; } = new();

        public string? LastGroupName { get; private set; }

        public IOrdersHubClient All => throw new NotSupportedException();

        public IOrdersHubClient AllExcept(IReadOnlyList<string> excludedConnectionIds)
            => throw new NotSupportedException();

        public IOrdersHubClient Client(string connectionId)
            => throw new NotSupportedException();

        public IOrdersHubClient Clients(IReadOnlyList<string> connectionIds)
            => throw new NotSupportedException();

        public IOrdersHubClient Group(string groupName)
        {
            LastGroupName = groupName;
            return GroupClient;
        }

        public IOrdersHubClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds)
            => throw new NotSupportedException();

        public IOrdersHubClient Groups(IReadOnlyList<string> groupNames)
            => throw new NotSupportedException();

        public IOrdersHubClient User(string userId)
            => throw new NotSupportedException();

        public IOrdersHubClient Users(IReadOnlyList<string> userIds)
            => throw new NotSupportedException();
    }

    private sealed class RecordingOrdersHubClient : IOrdersHubClient
    {
        public OrderRealtimeEvent? LastOrderEvent { get; private set; }

        public int OrderChangedCallCount { get; private set; }

        public Task OrderChanged(OrderRealtimeEvent orderEvent)
        {
            LastOrderEvent = orderEvent;
            OrderChangedCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
