using Velonixs.Connect.Application.Abstractions;

namespace Velonixs.Connect.Api.Hubs;

public interface IOrdersHubClient
{
    Task OrderChanged(OrderRealtimeEvent orderEvent);
}
