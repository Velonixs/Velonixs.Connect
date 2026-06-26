using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Portal.Models;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Portal.Controllers;

[Authorize(Roles = AppRoles.PortalRoles)]
public sealed class PortalController(
    IRestaurantService restaurantService,
    IMenuService menuService,
    IOrderService orderService,
    IDashboardService dashboardService,
    ICustomerService customerService,
    IStaffService staffService) : Controller
{
    private static readonly string[] StatusOptions =
    [
        OrderStatuses.Preparing,
        OrderStatuses.ReadyForPickup,
        OrderStatuses.OutForDelivery,
        OrderStatuses.Delivered,
        OrderStatuses.Cancelled
    ];

    [HttpGet("")]
    [HttpGet("portal")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboard = await dashboardService.GetRestaurantDashboardAsync(GetBusinessId(), cancellationToken: cancellationToken);

        if (dashboard is null)
        {
            return View("NoRestaurant");
        }

        var model = new PortalDashboardViewModel
        {
            Restaurant = dashboard.Restaurant,
            Orders = dashboard.RecentOrders,
            Menu = dashboard.Menu,
            PendingOrderCount = dashboard.PendingOrderCount,
            TodayOrderCount = dashboard.TodayOrderCount,
            TodayRevenue = dashboard.TodayRevenue,
            AvailableItemCount = dashboard.AvailableItemCount,
            RecentCustomers = dashboard.RecentCustomers.Select(ToPortalCustomerSummary).ToArray(),
            CurrentRole = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role)?.Value ?? string.Empty
        };

        return View(model);
    }

    [HttpGet("portal/menu")]
    public async Task<IActionResult> Menu(CancellationToken cancellationToken)
    {
        var restaurant = await ResolveRestaurantAsync(cancellationToken);

        if (restaurant is null)
        {
            return View("NoRestaurant");
        }

        var menu = await menuService.GetMenuAsync(restaurant.Id, cancellationToken);
        var masterCatalog = await menuService.GetMasterCatalogAsync(cancellationToken);

        return View(new PortalMenuViewModel
        {
            Restaurant = restaurant,
            Menu = menu,
            MasterCatalog = masterCatalog,
            NewItem =
            {
                ItemCode = (menu?.Items.Select(x => x.ItemCode).DefaultIfEmpty().Max() ?? 0) + 1,
                CategoryId = menu?.Categories.FirstOrDefault(x => x.IsActive)?.Id ?? Guid.Empty,
                MasterMenuItemId = masterCatalog.Items.FirstOrDefault(x => x.IsActive)?.Id ?? Guid.Empty
            },
            NewCategory =
            {
                MasterCategoryId = masterCatalog.Categories.FirstOrDefault(x => x.IsActive)?.Id ?? Guid.Empty
            },
            TaxSetting =
            {
                CgstPercent = restaurant.CgstPercent,
                SgstPercent = restaurant.SgstPercent
            },
            CanManageMenu = User.IsInRole(AppRoles.BusinessOwner) || User.IsInRole(AppRoles.BusinessManager)
        });
    }

    [HttpGet("portal/customers")]
    public async Task<IActionResult> Customers(CancellationToken cancellationToken)
    {
        var restaurant = await ResolveRestaurantAsync(cancellationToken);

        if (restaurant is null)
        {
            return View("NoRestaurant");
        }

        var customers = await customerService.GetRestaurantCustomersAsync(
            restaurant.Id,
            100,
            cancellationToken: cancellationToken);

        return View(new PortalCustomersViewModel
        {
            Restaurant = restaurant,
            Customers = customers.Select(ToPortalCustomerSummary).ToArray()
        });
    }

    [HttpGet("portal/orders/{id:guid}")]
    public async Task<IActionResult> Order(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderService.GetOrderAsync(id, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        var businessId = GetBusinessId();

        if (order.RestaurantId != businessId)
        {
            return Forbid();
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
            StatusOptions = ResolveStatusOptions(order.OrderStatus)
        });
    }

    [HttpPost("portal/orders/{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, PortalOrderStatusFormModel form, CancellationToken cancellationToken)
    {
        var existingOrder = await orderService.GetOrderAsync(id, cancellationToken);

        if (existingOrder is null)
        {
            return NotFound();
        }

        if (existingOrder.RestaurantId != GetBusinessId())
        {
            return Forbid();
        }

        OrderDetailResponse? order;
        try
        {
            order = await orderService.UpdateStatusAsync(
                id,
                new UpdateOrderStatusRequest(
                    form.Status,
                    form.Comment,
                    ResolveUpdatedBy(),
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

    [HttpPost("portal/categories")]
    [Authorize(Roles = AppRoles.BusinessOwner + "," + AppRoles.BusinessManager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(MenuCategoryPortalFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter a valid category name.";
            return RedirectToAction(nameof(Menu));
        }

        try
        {
            await menuService.CreateCategoryAsync(
                GetBusinessId(),
                new CreateMenuCategoryRequest(form.Name, form.DisplayOrder, form.IsActive, form.MasterCategoryId),
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "A category with this name already exists for this restaurant.";
            return RedirectToAction(nameof(Menu));
        }

        TempData["Success"] = "Menu category added.";
        return RedirectToAction(nameof(Menu));
    }

    [HttpPost("portal/tax-settings")]
    [Authorize(Roles = AppRoles.BusinessOwner + "," + AppRoles.BusinessManager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTaxSetting(RestaurantTaxFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "CGST and SGST must be between 0 and 100.";
            return RedirectToAction(nameof(Menu));
        }

        var restaurant = await restaurantService.UpdateRestaurantTaxSettingAsync(
            GetBusinessId(),
            form.CgstPercent,
            form.SgstPercent,
            cancellationToken);

        if (restaurant is null)
        {
            return NotFound();
        }

        TempData["Success"] = "GST settings saved. New carts and orders will use these values.";
        return RedirectToAction(nameof(Menu));
    }

    [HttpPost("portal/menu-items")]
    [Authorize(Roles = AppRoles.BusinessOwner + "," + AppRoles.BusinessManager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMenuItem(MenuItemPortalFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter valid product details.";
            return RedirectToAction(nameof(Menu));
        }

        try
        {
            await menuService.CreateItemAsync(
                GetBusinessId(),
                new CreateMenuItemRequest(
                    form.CategoryId,
                    form.MasterMenuItemId,
                    form.ItemCode,
                    form.Name,
                    form.Description,
                    form.Price,
                    form.IsAvailable,
                    form.IsActive),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Menu));
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "A product with this code or category/name already exists for this restaurant.";
            return RedirectToAction(nameof(Menu));
        }

        TempData["Success"] = "Menu product added.";
        return RedirectToAction(nameof(Menu));
    }

    [HttpPost("portal/menu-items/{id:guid}")]
    [Authorize(Roles = AppRoles.BusinessOwner + "," + AppRoles.BusinessManager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMenuItem(Guid id, MenuItemPortalFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter valid product details.";
            return RedirectToAction(nameof(Menu));
        }

        var item = await menuService.GetItemAsync(id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        if (item.RestaurantId != GetBusinessId())
        {
            return Forbid();
        }

        try
        {
            await menuService.UpdateItemAsync(
                id,
                new UpdateMenuItemRequest(
                    form.CategoryId,
                    form.MasterMenuItemId,
                    form.ItemCode,
                    form.Name,
                    form.Description,
                    form.Price,
                    form.IsAvailable,
                    form.IsActive),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Menu));
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "A product with this code or category/name already exists for this restaurant.";
            return RedirectToAction(nameof(Menu));
        }

        TempData["Success"] = "Menu product saved.";
        return RedirectToAction(nameof(Menu));
    }

    [HttpPost("portal/menu-items/{id:guid}/deactivate")]
    [Authorize(Roles = AppRoles.BusinessOwner + "," + AppRoles.BusinessManager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateMenuItem(Guid id, CancellationToken cancellationToken)
    {
        var item = await menuService.GetItemAsync(id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        if (item.RestaurantId != GetBusinessId())
        {
            return Forbid();
        }

        await menuService.DeactivateItemAsync(id, cancellationToken);

        TempData["Success"] = "Menu product deactivated.";
        return RedirectToAction(nameof(Menu));
    }

    [HttpPost("portal/menu-items/{id:guid}/delete")]
    [Authorize(Roles = AppRoles.BusinessOwner + "," + AppRoles.BusinessManager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMenuItem(Guid id, CancellationToken cancellationToken)
    {
        var item = await menuService.GetItemAsync(id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        if (item.RestaurantId != GetBusinessId())
        {
            return Forbid();
        }

        try
        {
            await menuService.DeleteItemAsync(id, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Menu));
        }

        TempData["Success"] = "Menu product deleted.";
        return RedirectToAction(nameof(Menu));
    }

    [HttpPost("portal/menu-items/{id:guid}/availability")]
    [Authorize(Roles = AppRoles.BusinessOwner + "," + AppRoles.BusinessManager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAvailability(Guid id, MenuAvailabilityFormModel form, CancellationToken cancellationToken)
    {
        var item = await menuService.GetItemAsync(id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        if (item.RestaurantId != GetBusinessId())
        {
            return Forbid();
        }

        var updated = await menuService.UpdateItemAvailabilityAsync(id, form.IsAvailable, cancellationToken);

        TempData["Success"] = $"{updated?.Name ?? item.Name} is now {(form.IsAvailable ? "available" : "unavailable")}.";
        return RedirectToAction(nameof(Menu));
    }

    [HttpPost("portal/customers/{id:guid}")]
    [Authorize(Roles = AppRoles.BusinessOwner + "," + AppRoles.BusinessManager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCustomer(Guid id, CustomerEditFormModel form, CancellationToken cancellationToken)
    {
        var customer = await customerService.GetCustomerAsync(id, cancellationToken: cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        if (customer.RestaurantId != GetBusinessId())
        {
            return Forbid();
        }

        await customerService.UpdateCustomerAsync(
            id,
            new UpdateCustomerRequest(form.Name, form.LastAddress),
            cancellationToken);

        TempData["Success"] = "Customer saved.";
        return RedirectToAction(nameof(Customers));
    }

    [AllowAnonymous]
    [HttpGet("portal/error")]
    public IActionResult Error(int? statusCode = null)
    {
        ViewData["StatusCode"] = statusCode;
        ViewData["RequestId"] = HttpContext.TraceIdentifier;
        return View();
    }

    private async Task<RestaurantResponse?> ResolveRestaurantAsync(CancellationToken cancellationToken)
    {
        return await restaurantService.GetRestaurantAsync(GetBusinessId(), cancellationToken);
    }

    [Authorize(Roles = AppRoles.BusinessOwner)]
    [HttpGet("portal/staff")]
    public async Task<IActionResult> Staff(CancellationToken cancellationToken)
    {
        var businessId = GetBusinessId();
        var business = await restaurantService.GetRestaurantAsync(businessId, cancellationToken);

        if (business is null)
        {
            return NotFound();
        }

        var users = await staffService.GetBusinessStaffAsync(businessId, cancellationToken);

        return View(new StaffIndexViewModel
        {
            Business = business,
            Users = users.Select(ToStaffUserSummary).ToArray()
        });
    }

    [Authorize(Roles = AppRoles.BusinessOwner)]
    [HttpPost("portal/staff")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStaff(CreateStaffUserModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !AppRoles.StaffAssignable.Contains(form.Role))
        {
            TempData["Error"] = "Enter valid staff details and select an allowed role.";
            return RedirectToAction(nameof(Staff));
        }

        var result = await staffService.CreateStaffUserAsync(
            GetBusinessId(),
            new CreateStaffUserRequest(form.DisplayName, form.Email, form.Password, form.Role),
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors);
            return RedirectToAction(nameof(Staff));
        }

        TempData["Success"] = $"Created {form.Role} account for {result.User?.Email ?? form.Email.Trim()}.";
        return RedirectToAction(nameof(Staff));
    }

    [Authorize(Roles = AppRoles.BusinessOwner)]
    [HttpPost("portal/staff/{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStaffStatus(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var currentUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
            ? parsed
            : (Guid?)null;
        var result = await staffService.UpdateStaffStatusAsync(
            GetBusinessId(),
            id,
            isActive,
            currentUserId,
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors);
            return RedirectToAction(nameof(Staff));
        }

        TempData["Success"] = $"{result.User?.DisplayName ?? "Staff account"} is now {(isActive ? "active" : "inactive")}.";
        return RedirectToAction(nameof(Staff));
    }

    private static CustomerPortalSummary ToPortalCustomerSummary(CustomerSummaryResponse customer) =>
        new()
        {
            Id = customer.Id,
            PhoneNumber = customer.PhoneNumber,
            Name = customer.Name,
            LastAddress = customer.LastAddress,
            LastInteractionAt = customer.LastInteractionAt
        };

    private static StaffUserSummary ToStaffUserSummary(StaffUserResponse user) =>
        new()
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive
        };

    private Guid GetBusinessId()
    {
        var value = User.FindFirstValue(AppClaimTypes.BusinessId);
        return Guid.TryParse(value, out var businessId)
            ? businessId
            : throw new InvalidOperationException("The signed-in user is not assigned to a business.");
    }

    private string ResolveUpdatedBy() =>
        User.FindFirstValue(ClaimTypes.Email) ??
        User.Identity?.Name ??
        "Portal staff";

    private static IReadOnlyCollection<string> ResolveStatusOptions(string currentStatus) =>
        currentStatus switch
        {
            OrderStatuses.Confirmed =>
            [
                OrderStatuses.Preparing,
                OrderStatuses.ReadyForPickup,
                OrderStatuses.OutForDelivery,
                OrderStatuses.Delivered,
                OrderStatuses.Cancelled
            ],
            OrderStatuses.Preparing =>
            [
                OrderStatuses.ReadyForPickup,
                OrderStatuses.OutForDelivery,
                OrderStatuses.Delivered,
                OrderStatuses.Cancelled
            ],
            OrderStatuses.ReadyForPickup =>
            [
                OrderStatuses.Delivered,
                OrderStatuses.Cancelled
            ],
            OrderStatuses.OutForDelivery =>
            [
                OrderStatuses.Delivered,
                OrderStatuses.Cancelled
            ],
            _ => StatusOptions
        };
}
