using Microsoft.AspNetCore.SignalR;
using Velonixs.Connect.Application.Abstractions;

namespace Velonixs.Connect.Api.Hubs;

public sealed class SignalROrderRealtimeNotifier(
    IHubContext<OrdersHub, IOrdersHubClient> hubContext) : IOrderRealtimeNotifier
{
    public Task NotifyAsync(OrderRealtimeEvent orderEvent, CancellationToken cancellationToken = default)
    {
        return hubContext
            .Clients
            .Group(OrdersHub.RestaurantGroup(orderEvent.RestaurantId))
            .OrderChanged(orderEvent);
    }
}
