using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IDashboardService
{
    Task<RestaurantDashboardResponse?> GetRestaurantDashboardAsync(
        Guid restaurantId,
        int recentOrderCount = 20,
        int recentCustomerCount = 10,
        CancellationToken cancellationToken = default);
}
