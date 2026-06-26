using Velonixs.Connect.Application.Abstractions;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class NoOpOrderRealtimeNotifier : IOrderRealtimeNotifier
{
    public Task NotifyAsync(OrderRealtimeEvent orderEvent, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
