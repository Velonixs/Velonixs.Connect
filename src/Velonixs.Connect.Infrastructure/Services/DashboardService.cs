using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class DashboardService(
    IRestaurantService restaurantService,
    IMenuService menuService,
    IOrderService orderService,
    ICustomerService customerService) : IDashboardService
{
    public async Task<RestaurantDashboardResponse?> GetRestaurantDashboardAsync(
        Guid restaurantId,
        int recentOrderCount = 20,
        int recentCustomerCount = 10,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await restaurantService.GetRestaurantAsync(restaurantId, cancellationToken);

        if (restaurant is null)
        {
            return null;
        }

        var orders = await orderService.GetRestaurantOrdersAsync(restaurantId, cancellationToken);
        var menu = await menuService.GetMenuAsync(restaurantId, cancellationToken);
        var today = DateTimeOffset.UtcNow.Date;
        var recentCustomers = await customerService.GetRestaurantCustomersAsync(
            restaurantId,
            recentCustomerCount,
            cancellationToken: cancellationToken);

        return new RestaurantDashboardResponse(
            restaurant,
            orders.Take(Math.Clamp(recentOrderCount, 1, 100)).ToArray(),
            menu,
            orders.Count(x => x.OrderStatus == OrderStatuses.PendingConfirmation),
            orders.Count(x => x.CreatedAt.UtcDateTime.Date == today),
            orders.Where(x => x.CreatedAt.UtcDateTime.Date == today).Sum(x => x.TotalAmount),
            menu?.Items.Count(x => x.IsActive && x.IsAvailable) ?? 0,
            recentCustomers);
    }
}
