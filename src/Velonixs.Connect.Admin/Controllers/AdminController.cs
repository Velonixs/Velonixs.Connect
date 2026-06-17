using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Admin.Models;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Admin.Controllers;

public sealed class AdminController(
    IRestaurantService restaurantService,
    IMenuService menuService,
    IOrderService orderService,
    RestaurantConnectDbContext dbContext) : Controller
{
    private static readonly string[] OrderStatusOptions =
    [
        OrderStatuses.Confirmed,
        OrderStatuses.Notified,
        OrderStatuses.Handled,
        OrderStatuses.Cancelled,
        OrderStatuses.Failed
    ];

    [HttpGet("")]
    [HttpGet("admin")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var restaurants = await restaurantService.GetRestaurantsAsync(cancellationToken);
        var summaries = new List<RestaurantAdminSummary>();

        foreach (var restaurant in restaurants)
        {
            var orders = dbContext.Orders.AsNoTracking().Where(x => x.RestaurantId == restaurant.Id);

            summaries.Add(new RestaurantAdminSummary
            {
                Restaurant = restaurant,
                MenuItemCount = await dbContext.MenuItems.CountAsync(x => x.RestaurantId == restaurant.Id && x.IsActive, cancellationToken),
                CustomerCount = await dbContext.Customers.CountAsync(x => x.RestaurantId == restaurant.Id, cancellationToken),
                OrderCount = await orders.CountAsync(cancellationToken),
                Revenue = await orders.SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0,
                LastOrderAt = await orders.MaxAsync(x => (DateTimeOffset?)x.CreatedAt, cancellationToken)
            });
        }

        var model = new AdminIndexViewModel
        {
            Restaurants = summaries.OrderBy(x => x.Restaurant.Name).ToArray(),
            TotalRestaurants = restaurants.Count,
            ActiveRestaurants = restaurants.Count(x => x.IsActive),
            TotalOrders = summaries.Sum(x => x.OrderCount),
            TotalRevenue = summaries.Sum(x => x.Revenue)
        };

        return View(model);
    }

    [HttpGet("admin/error")]
    public IActionResult Error()
    {
        return View();
    }

    [HttpPost("admin/restaurants")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRestaurant(RestaurantFormModel form, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(form.Name) || string.IsNullOrWhiteSpace(form.WhatsAppPhoneNumberId))
        {
            TempData["Error"] = "Restaurant name and WhatsApp phone number id are required.";
            return RedirectToAction(nameof(Index));
        }

        var restaurant = await restaurantService.CreateRestaurantAsync(
            new CreateRestaurantRequest(
                form.Name,
                form.BusinessType,
                form.WhatsAppPhoneNumberId,
                form.BusinessPhone,
                form.NotificationEmail,
                form.StaffWhatsAppNumber,
                form.Address,
                form.IsActive),
            cancellationToken);

        TempData["Success"] = $"Created {restaurant.Name}.";
        return RedirectToAction(nameof(Restaurant), new { id = restaurant.Id });
    }

    [HttpGet("admin/restaurants/{id:guid}")]
    public async Task<IActionResult> Restaurant(Guid id, CancellationToken cancellationToken)
    {
        var restaurant = await restaurantService.GetRestaurantAsync(id, cancellationToken);

        if (restaurant is null)
        {
            return NotFound();
        }

        var menu = await menuService.GetMenuAsync(id, cancellationToken);
        var orders = await orderService.GetRestaurantOrdersAsync(id, cancellationToken);
        var customers = await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.RestaurantId == id)
            .OrderByDescending(x => x.LastInteractionAt)
            .Select(x => new CustomerAdminSummary
            {
                PhoneNumber = x.PhoneNumber,
                Name = x.Name,
                LastAddress = x.LastAddress,
                LastInteractionAt = x.LastInteractionAt,
                OrderCount = dbContext.Orders.Count(order => order.CustomerId == x.Id)
            })
            .Take(20)
            .ToArrayAsync(cancellationToken);

        var model = new RestaurantManageViewModel
        {
            Restaurant = restaurant,
            RestaurantForm = ToRestaurantForm(restaurant),
            Menu = menu,
            Orders = orders.Take(25).ToArray(),
            Customers = customers,
            ActiveMenuItemCount = menu?.Items.Count(x => x.IsActive && x.IsAvailable) ?? 0,
            PendingOrderCount = orders.Count(x => x.OrderStatus is OrderStatuses.Confirmed or OrderStatuses.Notified),
            Revenue = orders.Sum(x => x.TotalAmount)
        };

        model.NewItem.ItemCode = (menu?.Items.Select(x => x.ItemCode).DefaultIfEmpty().Max() ?? 0) + 1;
        model.NewItem.CategoryId = menu?.Categories.FirstOrDefault(x => x.IsActive)?.Id ?? Guid.Empty;

        return View(model);
    }

    [HttpPost("admin/restaurants/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRestaurant(Guid id, RestaurantFormModel form, CancellationToken cancellationToken)
    {
        var restaurant = await restaurantService.UpdateRestaurantAsync(
            id,
            new UpdateRestaurantRequest(
                form.Name,
                form.BusinessType,
                form.WhatsAppPhoneNumberId,
                form.BusinessPhone,
                form.NotificationEmail,
                form.StaffWhatsAppNumber,
                form.Address,
                form.IsActive),
            cancellationToken);

        if (restaurant is null)
        {
            return NotFound();
        }

        TempData["Success"] = "Restaurant settings saved.";
        return RedirectToAction(nameof(Restaurant), new { id });
    }

    [HttpPost("admin/restaurants/{restaurantId:guid}/categories")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(Guid restaurantId, MenuCategoryFormModel form, CancellationToken cancellationToken)
    {
        await menuService.CreateCategoryAsync(
            restaurantId,
            new CreateMenuCategoryRequest(form.Name, form.DisplayOrder, form.IsActive),
            cancellationToken);

        TempData["Success"] = "Menu category added.";
        return RedirectToAction(nameof(Restaurant), new { id = restaurantId });
    }

    [HttpPost("admin/restaurants/{restaurantId:guid}/items")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem(Guid restaurantId, MenuItemFormModel form, CancellationToken cancellationToken)
    {
        await menuService.CreateItemAsync(
            restaurantId,
            new CreateMenuItemRequest(form.CategoryId, form.ItemCode, form.Name, form.Description, form.Price, form.IsAvailable, form.IsActive),
            cancellationToken);

        TempData["Success"] = "Menu item added.";
        return RedirectToAction(nameof(Restaurant), new { id = restaurantId });
    }

    [HttpPost("admin/restaurants/{restaurantId:guid}/items/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(Guid restaurantId, Guid id, MenuItemFormModel form, CancellationToken cancellationToken)
    {
        await menuService.UpdateItemAsync(
            id,
            new UpdateMenuItemRequest(form.CategoryId, form.ItemCode, form.Name, form.Description, form.Price, form.IsAvailable, form.IsActive),
            cancellationToken);

        TempData["Success"] = "Menu item saved.";
        return RedirectToAction(nameof(Restaurant), new { id = restaurantId });
    }

    [HttpPost("admin/restaurants/{restaurantId:guid}/items/{id:guid}/deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateItem(Guid restaurantId, Guid id, CancellationToken cancellationToken)
    {
        await menuService.DeactivateItemAsync(id, cancellationToken);
        TempData["Success"] = "Menu item deactivated.";
        return RedirectToAction(nameof(Restaurant), new { id = restaurantId });
    }

    [HttpGet("admin/orders/{id:guid}")]
    public async Task<IActionResult> Order(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderService.GetOrderAsync(id, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        var model = new OrderAdminViewModel
        {
            Order = order,
            Restaurant = await restaurantService.GetRestaurantAsync(order.RestaurantId, cancellationToken),
            StatusOptions = OrderStatusOptions
        };

        return View(model);
    }

    [HttpPost("admin/orders/{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, OrderStatusFormModel form, CancellationToken cancellationToken)
    {
        var order = await orderService.UpdateStatusAsync(id, new UpdateOrderStatusRequest(form.Status), cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        TempData["Success"] = $"Order {order.OrderNumber} marked {order.OrderStatus}.";
        return RedirectToAction(nameof(Order), new { id });
    }

    private static RestaurantFormModel ToRestaurantForm(RestaurantResponse restaurant)
    {
        return new RestaurantFormModel
        {
            Name = restaurant.Name,
            BusinessType = restaurant.BusinessType,
            WhatsAppPhoneNumberId = restaurant.WhatsAppPhoneNumberId,
            BusinessPhone = restaurant.BusinessPhone,
            NotificationEmail = restaurant.NotificationEmail,
            StaffWhatsAppNumber = restaurant.StaffWhatsAppNumber,
            Address = restaurant.Address,
            IsActive = restaurant.IsActive
        };
    }
}
