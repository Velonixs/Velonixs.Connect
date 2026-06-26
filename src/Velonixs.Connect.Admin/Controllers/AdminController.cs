using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Admin.Models;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Admin.Controllers;

[Authorize(Roles = AppRoles.PlatformAdmin)]
public sealed class AdminController(
    IRestaurantService restaurantService,
    IMenuService menuService,
    IOrderService orderService,
    IAdminService adminService) : Controller
{
    private static readonly string[] OrderStatusOptions =
    [
        OrderStatuses.PendingConfirmation,
        OrderStatuses.Confirmed,
        OrderStatuses.Preparing,
        OrderStatuses.ReadyForPickup,
        OrderStatuses.OutForDelivery,
        OrderStatuses.Delivered,
        OrderStatuses.Rejected,
        OrderStatuses.Cancelled,
        OrderStatuses.Failed
    ];

    [HttpGet("")]
    [HttpGet("admin")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboard = await adminService.GetPlatformDashboardAsync(cancellationToken);
        var model = new AdminIndexViewModel
        {
            Restaurants = dashboard.Restaurants.Select(ToRestaurantAdminSummary).ToArray(),
            TaxSetting =
            {
                CgstPercent = dashboard.TaxSetting.CgstPercent,
                SgstPercent = dashboard.TaxSetting.SgstPercent
            },
            NewRestaurant =
            {
                CgstPercent = dashboard.TaxSetting.CgstPercent,
                SgstPercent = dashboard.TaxSetting.SgstPercent
            },
            TotalRestaurants = dashboard.TotalRestaurants,
            ActiveRestaurants = dashboard.ActiveRestaurants,
            TotalOrders = dashboard.TotalOrders,
            TotalRevenue = dashboard.TotalRevenue
        };

        return View(model);
    }

    [HttpPost("admin/tax-settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTaxSetting(TaxSettingFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "CGST and SGST must be between 0 and 100.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await restaurantService.UpdatePlatformTaxSettingAsync(
                form.CgstPercent,
                form.SgstPercent,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Master GST defaults saved. New restaurants will use these defaults.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("admin/master-catalog")]
    public async Task<IActionResult> MasterCatalog(CancellationToken cancellationToken)
    {
        return View(new MasterCatalogManageViewModel
        {
            MasterCatalog = await menuService.GetMasterCatalogAsync(cancellationToken)
        });
    }

    [HttpPost("admin/master-categories")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMasterCategory(
        MasterCatalogCategoryFormModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter a valid master category name.";
            return RedirectToAction(nameof(MasterCatalog));
        }

        try
        {
            await menuService.CreateMasterCategoryAsync(
                new CreateMasterMenuCategoryRequest(form.Name, form.DisplayOrder, form.IsActive),
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "A master category with this name already exists.";
            return RedirectToAction(nameof(MasterCatalog));
        }

        TempData["Success"] = "Master category added.";
        return RedirectToAction(nameof(MasterCatalog));
    }

    [HttpPost("admin/master-categories/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMasterCategory(
        Guid id,
        MasterCatalogCategoryFormModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter a valid master category name.";
            return RedirectToAction(nameof(MasterCatalog));
        }

        try
        {
            var category = await menuService.UpdateMasterCategoryAsync(
                id,
                new CreateMasterMenuCategoryRequest(form.Name, form.DisplayOrder, form.IsActive),
                cancellationToken);

            if (category is null)
            {
                return NotFound();
            }
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "A master category with this name already exists.";
            return RedirectToAction(nameof(MasterCatalog));
        }

        TempData["Success"] = "Master category saved.";
        return RedirectToAction(nameof(MasterCatalog));
    }

    [HttpPost("admin/master-categories/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMasterCategory(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await menuService.DeleteMasterCategoryAsync(id, cancellationToken);

            if (!deleted)
            {
                return NotFound();
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(MasterCatalog));
        }

        TempData["Success"] = "Master category deleted.";
        return RedirectToAction(nameof(MasterCatalog));
    }

    [HttpPost("admin/master-items")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMasterItem(
        MasterCatalogItemFormModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter valid master menu item details.";
            return RedirectToAction(nameof(MasterCatalog));
        }

        try
        {
            await menuService.CreateMasterItemAsync(
                new CreateMasterMenuItemRequest(
                    form.MasterCategoryId,
                    form.Name,
                    form.Description,
                    form.IsActive),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(MasterCatalog));
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "A master item with this name already exists in the selected category.";
            return RedirectToAction(nameof(MasterCatalog));
        }

        TempData["Success"] = "Master menu item added.";
        return RedirectToAction(nameof(MasterCatalog));
    }

    [HttpPost("admin/master-items/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMasterItem(
        Guid id,
        MasterCatalogItemFormModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter valid master menu item details.";
            return RedirectToAction(nameof(MasterCatalog));
        }

        try
        {
            var item = await menuService.UpdateMasterItemAsync(
                id,
                new CreateMasterMenuItemRequest(
                    form.MasterCategoryId,
                    form.Name,
                    form.Description,
                    form.IsActive),
                cancellationToken);

            if (item is null)
            {
                return NotFound();
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(MasterCatalog));
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "A master item with this name already exists in the selected category.";
            return RedirectToAction(nameof(MasterCatalog));
        }

        TempData["Success"] = "Master menu item saved.";
        return RedirectToAction(nameof(MasterCatalog));
    }

    [HttpPost("admin/master-items/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMasterItem(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await menuService.DeleteMasterItemAsync(id, cancellationToken);

            if (!deleted)
            {
                return NotFound();
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(MasterCatalog));
        }

        TempData["Success"] = "Master menu item deleted.";
        return RedirectToAction(nameof(MasterCatalog));
    }

    [AllowAnonymous]
    [HttpGet("admin/error")]
    public IActionResult Error(int? statusCode = null)
    {
        ViewData["StatusCode"] = statusCode;
        ViewData["RequestId"] = HttpContext.TraceIdentifier;
        return View();
    }

    [HttpPost("admin/restaurants")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRestaurant(RestaurantFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Business and owner account details are required. Passwords must be at least 8 characters.";
            return RedirectToAction(nameof(Index));
        }

        var result = await adminService.CreateRestaurantWithOwnerAsync(
            new CreateRestaurantWithOwnerRequest(
                new CreateRestaurantRequest(
                    form.Name,
                    form.BusinessType,
                    form.WhatsAppPhoneNumberId,
                    form.BusinessPhone,
                    form.NotificationEmail,
                    form.StaffWhatsAppNumber,
                    form.Address,
                    form.CgstPercent,
                    form.SgstPercent,
                    form.IsActive),
                new CreateOwnerUserRequest(form.OwnerName, form.OwnerEmail, form.OwnerPassword)),
            cancellationToken);

        if (!result.Succeeded || result.Restaurant is null)
        {
            TempData["Error"] = string.Join(" ", result.Errors);
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = $"Created {result.Restaurant.Name} and owner account {result.Owner?.Email ?? form.OwnerEmail.Trim()}.";
        return RedirectToAction(nameof(Restaurant), new { id = result.Restaurant.Id });
    }

    [HttpGet("admin/restaurants/{id:guid}")]
    public async Task<IActionResult> Restaurant(Guid id, CancellationToken cancellationToken)
    {
        var detail = await adminService.GetRestaurantDetailAsync(id, cancellationToken);

        if (detail is null)
        {
            return NotFound();
        }

        var model = new RestaurantManageViewModel
        {
            Restaurant = detail.Restaurant,
            RestaurantForm = ToRestaurantForm(detail.Restaurant),
            Menu = detail.Menu,
            Orders = detail.RecentOrders,
            Customers = detail.RecentCustomers.Select(ToCustomerAdminSummary).ToArray(),
            ActiveMenuItemCount = detail.ActiveMenuItemCount,
            PendingOrderCount = detail.PendingOrderCount,
            Revenue = detail.Revenue,
            Users = detail.Users.Select(ToBusinessUserSummary).ToArray()
        };

        model.NewItem.ItemCode = (detail.Menu?.Items.Select(x => x.ItemCode).DefaultIfEmpty().Max() ?? 0) + 1;
        model.NewItem.CategoryId = detail.Menu?.Categories.FirstOrDefault(x => x.IsActive)?.Id ?? Guid.Empty;

        return View(model);
    }

    [HttpPost("admin/restaurants/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRestaurant(Guid id, RestaurantFormModel form, CancellationToken cancellationToken)
    {
        RestaurantResponse? restaurant;
        try
        {
            restaurant = await restaurantService.UpdateRestaurantAsync(
                id,
                new UpdateRestaurantRequest(
                    form.Name,
                    form.BusinessType,
                    form.WhatsAppPhoneNumberId,
                    form.BusinessPhone,
                    form.NotificationEmail,
                    form.StaffWhatsAppNumber,
                    form.Address,
                    form.CgstPercent,
                    form.SgstPercent,
                    form.IsActive),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Restaurant), new { id });
        }

        if (restaurant is null)
        {
            return NotFound();
        }

        TempData["Success"] = "Restaurant settings saved.";
        return RedirectToAction(nameof(Restaurant), new { id });
    }

    [HttpPost("admin/restaurants/{id:guid}/owner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOwner(Guid id, OwnerAccountFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || await restaurantService.GetRestaurantAsync(id, cancellationToken) is null)
        {
            TempData["Error"] = "Enter valid owner details. Passwords must be at least 8 characters.";
            return RedirectToAction(nameof(Restaurant), new { id });
        }

        var result = await adminService.CreateBusinessOwnerAsync(
            id,
            new CreateOwnerUserRequest(form.DisplayName, form.Email, form.Password),
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors);
            return RedirectToAction(nameof(Restaurant), new { id });
        }

        TempData["Success"] = $"Created owner account {result.User?.Email ?? form.Email.Trim()}.";
        return RedirectToAction(nameof(Restaurant), new { id });
    }

    [HttpPost("admin/restaurants/{restaurantId:guid}/users/{userId:guid}/password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPortalUserPassword(
        Guid restaurantId,
        Guid userId,
        ResetPortalUserPasswordFormModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter a temporary password with at least 8 characters.";
            return RedirectToAction(nameof(Restaurant), new { id = restaurantId });
        }

        var result = await adminService.ResetBusinessUserPasswordAsync(
            restaurantId,
            userId,
            form.NewPassword,
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors);
            return RedirectToAction(nameof(Restaurant), new { id = restaurantId });
        }

        TempData["Success"] = $"Password reset for {result.User?.Email}. Share the temporary password securely.";
        return RedirectToAction(nameof(Restaurant), new { id = restaurantId });
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
            new CreateMenuItemRequest(form.CategoryId, null, form.ItemCode, form.Name, form.Description, form.Price, form.IsAvailable, form.IsActive),
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
            new UpdateMenuItemRequest(form.CategoryId, null, form.ItemCode, form.Name, form.Description, form.Price, form.IsAvailable, form.IsActive),
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
        OrderDetailResponse? order;
        try
        {
            order = await orderService.UpdateStatusAsync(
                id,
                new UpdateOrderStatusRequest(
                    form.Status,
                    form.Comment,
                    User.Identity?.Name ?? "Platform admin",
                    form.EffectiveEstimatedMinutes),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Order), new { id });
        }

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
            CgstPercent = restaurant.CgstPercent,
            SgstPercent = restaurant.SgstPercent,
            IsActive = restaurant.IsActive
        };
    }

    private static RestaurantAdminSummary ToRestaurantAdminSummary(RestaurantAdminSummaryResponse summary) =>
        new()
        {
            Restaurant = summary.Restaurant,
            MenuItemCount = summary.MenuItemCount,
            CustomerCount = summary.CustomerCount,
            OrderCount = summary.OrderCount,
            Revenue = summary.Revenue,
            LastOrderAt = summary.LastOrderAt
        };

    private static CustomerAdminSummary ToCustomerAdminSummary(CustomerSummaryResponse customer) =>
        new()
        {
            PhoneNumber = customer.PhoneNumber,
            Name = customer.Name,
            LastAddress = customer.LastAddress,
            OrderCount = customer.OrderCount,
            LastInteractionAt = customer.LastInteractionAt
        };

    private static BusinessUserSummary ToBusinessUserSummary(StaffUserResponse user) =>
        new()
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive
        };
}
