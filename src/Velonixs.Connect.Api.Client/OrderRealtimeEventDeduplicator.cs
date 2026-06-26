using Velonixs.Connect.Application.Abstractions;

namespace Velonixs.Connect.Api.Client;

public sealed class OrderRealtimeEventDeduplicator(int capacity = 256)
{
    private readonly Queue<string> order = new();
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private readonly object syncRoot = new();

    public bool TryAccept(OrderRealtimeEvent orderEvent)
    {
        var eventId = GetEventId(orderEvent);

        lock (syncRoot)
        {
            if (!seen.Add(eventId))
            {
                return false;
            }

            order.Enqueue(eventId);
            while (order.Count > capacity && order.TryDequeue(out var expired))
            {
                seen.Remove(expired);
            }

            return true;
        }
    }

    private static string GetEventId(OrderRealtimeEvent orderEvent)
    {
        return string.IsNullOrWhiteSpace(orderEvent.EventId)
            ? $"{orderEvent.RestaurantId:N}:{orderEvent.OrderId:N}:{orderEvent.EventType}:{orderEvent.OrderStatus}"
            : orderEvent.EventId;
    }
}
