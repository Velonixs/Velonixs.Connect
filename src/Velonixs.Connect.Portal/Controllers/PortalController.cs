using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Portal.Models;

namespace Velonixs.Connect.Portal.Controllers;

public sealed class PortalController(
    IConfiguration configuration,
    IRestaurantService restaurantService,
    IMenuService menuService,
    IOrderService orderService,
    RestaurantConnectDbContext dbContext) : Controller
{
    private static readonly string[] StatusOptions =
    [
        OrderStatuses.Notified,
        OrderStatuses.Handled,
        OrderStatuses.Cancelled
    ];

    [HttpGet("")]
    [HttpGet("portal")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var restaurant = await ResolveRestaurantAsync(cancellationToken);

        if (restaurant is null)
        {
            return View("NoRestaurant");
        }

        var orders = await orderService.GetRestaurantOrdersAsync(restaurant.Id, cancellationToken);
        var menu = await menuService.GetMenuAsync(restaurant.Id, cancellationToken);
        var today = DateTimeOffset.UtcNow.Date;
        var recentCustomers = await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.RestaurantId == restaurant.Id)
            .OrderByDescending(x => x.LastInteractionAt)
            .Take(10)
            .Select(x => new CustomerPortalSummary
            {
                PhoneNumber = x.PhoneNumber,
                Name = x.Name,
                LastAddress = x.LastAddress,
                LastInteractionAt = x.LastInteractionAt
            })
            .ToArrayAsync(cancellationToken);

        var model = new PortalDashboardViewModel
        {
            Restaurant = restaurant,
            Orders = orders.Take(20).ToArray(),
            Menu = menu,
            PendingOrderCount = orders.Count(x => x.OrderStatus is OrderStatuses.Confirmed or OrderStatuses.Notified),
            TodayOrderCount = orders.Count(x => x.CreatedAt.UtcDateTime.Date == today),
            TodayRevenue = orders.Where(x => x.CreatedAt.UtcDateTime.Date == today).Sum(x => x.TotalAmount),
            AvailableItemCount = menu?.Items.Count(x => x.IsActive && x.IsAvailable) ?? 0,
            RecentCustomers = recentCustomers
        };

        return View(model);
    }

    [HttpGet("portal/orders/{id:guid}")]
    public async Task<IActionResult> Order(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderService.GetOrderAsync(id, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        var restaurant = await restaurantService.GetRestaurantAsync(order.RestaurantId, cancellationToken);

        if (restaurant is null)
        {
            return NotFound();
        }

        return View(new PortalOrderViewModel
        {
            Restaurant = restaurant,
            Order = order,
            StatusOptions = StatusOptions
        });
    }

    [HttpPost("portal/orders/{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, PortalOrderStatusFormModel form, CancellationToken cancellationToken)
    {
        var order = await orderService.UpdateStatusAsync(id, new UpdateOrderStatusRequest(form.Status), cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        TempData["Success"] = $"Order {order.OrderNumber} marked {order.OrderStatus}.";
        return RedirectToAction(nameof(Order), new { id });
    }

    [HttpPost("portal/menu-items/{id:guid}/availability")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAvailability(Guid id, MenuAvailabilityFormModel form, CancellationToken cancellationToken)
    {
        var item = await dbContext.MenuItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        item.IsAvailable = form.IsAvailable;
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["Success"] = $"{item.Name} is now {(item.IsAvailable ? "available" : "unavailable")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("portal/error")]
    public IActionResult Error()
    {
        return View();
    }

    private async Task<RestaurantResponse?> ResolveRestaurantAsync(CancellationToken cancellationToken)
    {
        var configuredRestaurantId = configuration["Portal:RestaurantId"];

        if (Guid.TryParse(configuredRestaurantId, out var restaurantId))
        {
            return await restaurantService.GetRestaurantAsync(restaurantId, cancellationToken);
        }

        return (await restaurantService.GetRestaurantsAsync(cancellationToken))
            .FirstOrDefault(x => x.IsActive);
    }
}
