using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Admin.Models;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Admin.Controllers;

[Authorize(Roles = AppRoles.PlatformAdmin)]
public sealed class AdminController(
    IRestaurantService restaurantService,
    IMenuService menuService,
    IOrderService orderService,
    RestaurantConnectDbContext dbContext,
    UserManager<ApplicationUser> userManager) : Controller
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

        var ownerEmail = form.OwnerEmail.Trim();
        var whatsAppPhoneNumberId = form.WhatsAppPhoneNumberId.Trim();

        if (await userManager.FindByEmailAsync(ownerEmail) is not null)
        {
            TempData["Error"] = "An account already exists for the owner email.";
            return RedirectToAction(nameof(Index));
        }

        if (await dbContext.Restaurants.AnyAsync(x => x.WhatsAppPhoneNumberId == whatsAppPhoneNumberId, cancellationToken))
        {
            TempData["Error"] = "Another business already uses this WhatsApp Phone Number ID.";
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var restaurant = await restaurantService.CreateRestaurantAsync(
                new CreateRestaurantRequest(
                    form.Name,
                    form.BusinessType,
                    whatsAppPhoneNumberId,
                    form.BusinessPhone,
                    form.NotificationEmail,
                    form.StaffWhatsAppNumber,
                    form.Address,
                    form.IsActive),
                cancellationToken);

        var owner = new ApplicationUser
        {
            UserName = ownerEmail,
            Email = ownerEmail,
            EmailConfirmed = true,
            DisplayName = form.OwnerName.Trim(),
            BusinessId = restaurant.Id,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(owner, form.OwnerPassword);

        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData["Error"] = string.Join(" ", createResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index));
        }

        var roleResult = await userManager.AddToRoleAsync(owner, AppRoles.BusinessOwner);

        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData["Error"] = string.Join(" ", roleResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index));
        }

        await transaction.CommitAsync(cancellationToken);

        TempData["Success"] = $"Created {restaurant.Name} and owner account {ownerEmail}.";
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
        var businessUsers = await userManager.Users
            .AsNoTracking()
            .Where(x => x.BusinessId == id)
            .OrderBy(x => x.DisplayName)
            .ToArrayAsync(cancellationToken);
        var userSummaries = new List<BusinessUserSummary>();

        foreach (var user in businessUsers)
        {
            userSummaries.Add(new BusinessUserSummary
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email ?? string.Empty,
                Role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Unassigned",
                IsActive = user.IsActive
            });
        }

        var model = new RestaurantManageViewModel
        {
            Restaurant = restaurant,
            RestaurantForm = ToRestaurantForm(restaurant),
            Menu = menu,
            Orders = orders.Take(25).ToArray(),
            Customers = customers,
            ActiveMenuItemCount = menu?.Items.Count(x => x.IsActive && x.IsAvailable) ?? 0,
            PendingOrderCount = orders.Count(x => x.OrderStatus == OrderStatuses.PendingConfirmation),
            Revenue = orders.Sum(x => x.TotalAmount),
            Users = userSummaries
        };

        model.NewItem.ItemCode = (menu?.Items.Select(x => x.ItemCode).DefaultIfEmpty().Max() ?? 0) + 1;
        model.NewItem.CategoryId = menu?.Categories.FirstOrDefault(x => x.IsActive)?.Id ?? Guid.Empty;

        return View(model);
    }

    [HttpPost("admin/restaurants/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRestaurant(Guid id, RestaurantFormModel form, CancellationToken cancellationToken)
    {
        var whatsAppPhoneNumberId = form.WhatsAppPhoneNumberId.Trim();

        if (await dbContext.Restaurants.AnyAsync(
                x => x.Id != id && x.WhatsAppPhoneNumberId == whatsAppPhoneNumberId,
                cancellationToken))
        {
            TempData["Error"] = "Another business already uses this WhatsApp Phone Number ID.";
            return RedirectToAction(nameof(Restaurant), new { id });
        }

        var restaurant = await restaurantService.UpdateRestaurantAsync(
            id,
            new UpdateRestaurantRequest(
                form.Name,
                form.BusinessType,
                whatsAppPhoneNumberId,
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

    [HttpPost("admin/restaurants/{id:guid}/owner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOwner(Guid id, OwnerAccountFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || await restaurantService.GetRestaurantAsync(id, cancellationToken) is null)
        {
            TempData["Error"] = "Enter valid owner details. Passwords must be at least 8 characters.";
            return RedirectToAction(nameof(Restaurant), new { id });
        }

        var email = form.Email.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            TempData["Error"] = "An account already exists for this email.";
            return RedirectToAction(nameof(Restaurant), new { id });
        }

        var owner = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = form.DisplayName.Trim(),
            BusinessId = id,
            IsActive = true
        };

        var result = await userManager.CreateAsync(owner, form.Password);

        if (result.Succeeded)
        {
            result = await userManager.AddToRoleAsync(owner, AppRoles.BusinessOwner);
        }

        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Restaurant), new { id });
        }

        TempData["Success"] = $"Created owner account {email}.";
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

        if (await restaurantService.GetRestaurantAsync(restaurantId, cancellationToken) is null)
        {
            return NotFound();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null || user.BusinessId != restaurantId)
        {
            return NotFound();
        }

        var validationErrors = new List<IdentityError>();

        foreach (var validator in userManager.PasswordValidators)
        {
            var validationResult = await validator.ValidateAsync(userManager, user, form.NewPassword);

            if (!validationResult.Succeeded)
            {
                validationErrors.AddRange(validationResult.Errors);
            }
        }

        if (validationErrors.Count > 0)
        {
            TempData["Error"] = string.Join(" ", validationErrors.Select(error => error.Description));
            return RedirectToAction(nameof(Restaurant), new { id = restaurantId });
        }

        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, form.NewPassword);
        var result = await userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            result = await userManager.UpdateSecurityStampAsync(user);
        }

        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Restaurant), new { id = restaurantId });
        }

        TempData["Success"] = $"Password reset for {user.Email}. Share the temporary password securely.";
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
            IsActive = restaurant.IsActive
        };
    }
}
