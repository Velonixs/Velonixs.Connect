namespace Velonixs.Connect.Application.Abstractions;

public sealed record OrderRealtimeEvent(
    string EventId,
    string EventType,
    Guid RestaurantId,
    Guid OrderId,
    string OrderNumber,
    string OrderStatus,
    decimal TotalAmount,
    DateTimeOffset OccurredAtUtc);

public interface IOrderRealtimeNotifier
{
    Task NotifyAsync(OrderRealtimeEvent orderEvent, CancellationToken cancellationToken = default);
}
