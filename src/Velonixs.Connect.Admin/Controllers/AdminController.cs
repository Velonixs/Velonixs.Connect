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

        var taxSetting = await restaurantService.GetPlatformTaxSettingAsync(cancellationToken);
        var model = new AdminIndexViewModel
        {
            Restaurants = summaries.OrderBy(x => x.Restaurant.Name).ToArray(),
            TaxSetting =
            {
                CgstPercent = taxSetting.CgstPercent,
                SgstPercent = taxSetting.SgstPercent
            },
            NewRestaurant =
            {
                CgstPercent = taxSetting.CgstPercent,
                SgstPercent = taxSetting.SgstPercent
            },
            TotalRestaurants = restaurants.Count,
            ActiveRestaurants = restaurants.Count(x => x.IsActive),
            TotalOrders = summaries.Sum(x => x.OrderCount),
            TotalRevenue = summaries.Sum(x => x.Revenue)
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

        var category = await dbContext.MasterMenuCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        category.Name = form.Name.Trim();
        category.DisplayOrder = form.DisplayOrder;
        category.IsActive = form.IsActive;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
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
        var category = await dbContext.MasterMenuCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        var isInUse =
            await dbContext.MasterMenuItems.AnyAsync(x => x.MasterCategoryId == id, cancellationToken) ||
            await dbContext.MenuCategories.AnyAsync(x => x.MasterCategoryId == id, cancellationToken);

        if (isInUse)
        {
            TempData["Error"] = "This master category is in use. Mark it inactive instead of deleting it.";
            return RedirectToAction(nameof(MasterCatalog));
        }

        dbContext.MasterMenuCategories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);

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

        var item = await dbContext.MasterMenuItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        if (!await dbContext.MasterMenuCategories.AnyAsync(x => x.Id == form.MasterCategoryId, cancellationToken))
        {
            TempData["Error"] = "Select a valid master category.";
            return RedirectToAction(nameof(MasterCatalog));
        }

        item.MasterCategoryId = form.MasterCategoryId;
        item.Name = form.Name.Trim();
        item.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        item.IsActive = form.IsActive;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
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
        var item = await dbContext.MasterMenuItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        if (await dbContext.MenuItems.AnyAsync(x => x.MasterMenuItemId == id, cancellationToken))
        {
            TempData["Error"] = "This master item is mapped by restaurants. Mark it inactive instead of deleting it.";
            return RedirectToAction(nameof(MasterCatalog));
        }

        dbContext.MasterMenuItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

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
                    form.CgstPercent,
                    form.SgstPercent,
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
                form.CgstPercent,
                form.SgstPercent,
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
}
