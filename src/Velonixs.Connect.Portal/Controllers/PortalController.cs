using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Portal.Models;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Portal.Controllers;

[Authorize(Roles = AppRoles.PortalRoles)]
public sealed class PortalController(
    IRestaurantService restaurantService,
    IMenuService menuService,
    IOrderService orderService,
    RestaurantConnectDbContext dbContext,
    UserManager<ApplicationUser> userManager) : Controller
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
                Id = x.Id,
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
            PendingOrderCount = orders.Count(x => x.OrderStatus == OrderStatuses.PendingConfirmation),
            TodayOrderCount = orders.Count(x => x.CreatedAt.UtcDateTime.Date == today),
            TodayRevenue = orders.Where(x => x.CreatedAt.UtcDateTime.Date == today).Sum(x => x.TotalAmount),
            AvailableItemCount = menu?.Items.Count(x => x.IsActive && x.IsAvailable) ?? 0,
            RecentCustomers = recentCustomers,
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

        var customers = await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.RestaurantId == restaurant.Id)
            .OrderByDescending(x => x.LastInteractionAt)
            .Take(100)
            .Select(x => new CustomerPortalSummary
            {
                Id = x.Id,
                PhoneNumber = x.PhoneNumber,
                Name = x.Name,
                LastAddress = x.LastAddress,
                LastInteractionAt = x.LastInteractionAt
            })
            .ToArrayAsync(cancellationToken);

        return View(new PortalCustomersViewModel
        {
            Restaurant = restaurant,
            Customers = customers
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
        var notificationWarning = BuildCustomerNotificationWarning(order);
        if (notificationWarning is not null)
        {
            TempData["Warning"] = notificationWarning;
        }

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

        var restaurant = await dbContext.Restaurants.FirstOrDefaultAsync(x => x.Id == GetBusinessId(), cancellationToken);

        if (restaurant is null)
        {
            return NotFound();
        }

        restaurant.CgstPercent = form.CgstPercent;
        restaurant.SgstPercent = form.SgstPercent;
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "GST settings saved. New carts and orders will use these values.";
        return RedirectToAction(nameof(Menu));
    }

    [HttpPost("portal/whatsapp-catalog")]
    [Authorize(Roles = AppRoles.BusinessOwner + "," + AppRoles.BusinessManager)]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateWhatsAppCatalog(RestaurantWhatsAppCatalogFormModel form, CancellationToken cancellationToken)
    {
        // Keep this legacy POST route non-mutating for existing bookmarks and
        // forms. Catalog changes must use the platform Catalog Sync page,
        // which persists MetaCatalogSetting and reconciles product/outbox data.
        _ = form;
        _ = cancellationToken;
        TempData["Info"] = "WhatsApp catalog settings are managed by a platform administrator from Catalog Sync.";
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
                    form.IsActive,
                    form.ProductRetailerId),
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

        var item = await dbContext.MenuItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

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
                    form.IsActive,
                    form.ProductRetailerId),
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
        var item = await dbContext.MenuItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

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
        var item = await dbContext.MenuItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

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
            if (!await menuService.DeleteItemAsync(id, cancellationToken))
            {
                return NotFound();
            }
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
        var item = await dbContext.MenuItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        if (item.RestaurantId != GetBusinessId())
        {
            return Forbid();
        }

        var updatedItem = await menuService.SetItemAvailabilityAsync(id, form.IsAvailable, cancellationToken);

        if (updatedItem is null)
        {
            return NotFound();
        }

        TempData["Success"] = $"{updatedItem.Name} is now {(updatedItem.IsAvailable ? "available" : "unavailable")}.";
        return RedirectToAction(nameof(Menu));
    }

    [HttpPost("portal/customers/{id:guid}")]
    [Authorize(Roles = AppRoles.BusinessOwner + "," + AppRoles.BusinessManager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCustomer(Guid id, CustomerEditFormModel form, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        if (customer.RestaurantId != GetBusinessId())
        {
            return Forbid();
        }

        customer.Name = string.IsNullOrWhiteSpace(form.Name) ? null : form.Name.Trim();
        customer.LastAddress = string.IsNullOrWhiteSpace(form.LastAddress) ? null : form.LastAddress.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);

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

        var users = await userManager.Users
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DisplayName)
            .ToArrayAsync(cancellationToken);
        var summaries = new List<StaffUserSummary>();

        foreach (var user in users)
        {
            summaries.Add(new StaffUserSummary
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email ?? string.Empty,
                Role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Unassigned",
                IsActive = user.IsActive
            });
        }

        return View(new StaffIndexViewModel
        {
            Business = business,
            Users = summaries
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

        var email = form.Email.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            TempData["Error"] = "An account already exists for this email.";
            return RedirectToAction(nameof(Staff));
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = form.DisplayName.Trim(),
            BusinessId = GetBusinessId(),
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, form.Password);

        if (result.Succeeded)
        {
            result = await userManager.AddToRoleAsync(user, form.Role);
        }

        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Staff));
        }

        TempData["Success"] = $"Created {form.Role} account for {email}.";
        return RedirectToAction(nameof(Staff));
    }

    [Authorize(Roles = AppRoles.BusinessOwner)]
    [HttpPost("portal/staff/{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStaffStatus(Guid id, bool isActive)
    {
        var user = await userManager.FindByIdAsync(id.ToString());

        if (user is null || user.BusinessId != GetBusinessId())
        {
            return NotFound();
        }

        if (user.Id.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            TempData["Error"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Staff));
        }

        user.IsActive = isActive;
        await userManager.UpdateAsync(user);
        TempData["Success"] = $"{user.DisplayName} is now {(isActive ? "active" : "inactive")}.";
        return RedirectToAction(nameof(Staff));
    }

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

    private static string? BuildCustomerNotificationWarning(OrderDetailResponse order)
    {
        var notification = order.Messages
            .Where(x => x.Direction == MessageDirections.Outgoing)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        return notification?.Status switch
        {
            MessageStatuses.Skipped =>
                "Order status was saved, but the customer WhatsApp update was not sent because WhatsApp sending is disabled or credentials are missing.",
            MessageStatuses.Failed =>
                "Order status was saved, but the customer WhatsApp update failed. Check the conversation log for the provider response.",
            _ => null
        };
    }
}
