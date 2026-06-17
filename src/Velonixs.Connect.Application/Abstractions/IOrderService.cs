using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IOrderService
{
    Task<IReadOnlyCollection<OrderSummaryResponse>> GetRestaurantOrdersAsync(Guid restaurantId, CancellationToken cancellationToken = default);
    Task<OrderDetailResponse?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OrderDetailResponse?> UpdateStatusAsync(Guid id, UpdateOrderStatusRequest request, CancellationToken cancellationToken = default);
}
