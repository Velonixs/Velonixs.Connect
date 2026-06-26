using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class AdminService(
    RestaurantConnectDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IRestaurantService restaurantService,
    IMenuService menuService,
    IOrderService orderService,
    ICustomerService customerService,
    IStaffService staffService) : IAdminService
{
    public async Task<PlatformDashboardResponse> GetPlatformDashboardAsync(CancellationToken cancellationToken = default)
    {
        var restaurants = await restaurantService.GetRestaurantsAsync(cancellationToken);
        var summaries = new List<RestaurantAdminSummaryResponse>(restaurants.Count);

        foreach (var restaurant in restaurants)
        {
            var orders = dbContext.Orders.AsNoTracking().Where(x => x.RestaurantId == restaurant.Id);
            summaries.Add(new RestaurantAdminSummaryResponse(
                restaurant,
                await dbContext.MenuItems.CountAsync(x => x.RestaurantId == restaurant.Id && x.IsActive, cancellationToken),
                await dbContext.Customers.CountAsync(x => x.RestaurantId == restaurant.Id, cancellationToken),
                await orders.CountAsync(cancellationToken),
                await orders.SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0,
                await orders.MaxAsync(x => (DateTimeOffset?)x.CreatedAt, cancellationToken)));
        }

        var taxSetting = await restaurantService.GetPlatformTaxSettingAsync(cancellationToken);
        var orderedSummaries = summaries.OrderBy(x => x.Restaurant.Name).ToArray();

        return new PlatformDashboardResponse(
            orderedSummaries,
            taxSetting,
            restaurants.Count,
            restaurants.Count(x => x.IsActive),
            summaries.Sum(x => x.OrderCount),
            summaries.Sum(x => x.Revenue));
    }

    public async Task<RestaurantAdminDetailResponse?> GetRestaurantDetailAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await restaurantService.GetRestaurantAsync(restaurantId, cancellationToken);

        if (restaurant is null)
        {
            return null;
        }

        var menu = await menuService.GetMenuAsync(restaurantId, cancellationToken);
        var orders = await orderService.GetRestaurantOrdersAsync(restaurantId, cancellationToken);
        var customers = await customerService.GetRestaurantCustomersAsync(
            restaurantId,
            20,
            includeOrderCount: true,
            cancellationToken);
        var users = await staffService.GetBusinessStaffAsync(restaurantId, cancellationToken);

        return new RestaurantAdminDetailResponse(
            restaurant,
            menu,
            orders.Take(25).ToArray(),
            customers,
            users,
            menu?.Items.Count(x => x.IsActive && x.IsAvailable) ?? 0,
            orders.Count(x => x.OrderStatus == OrderStatuses.PendingConfirmation),
            orders.Sum(x => x.TotalAmount));
    }

    public async Task<CreateRestaurantWithOwnerResult> CreateRestaurantWithOwnerAsync(
        CreateRestaurantWithOwnerRequest request,
        CancellationToken cancellationToken = default)
    {
        var ownerEmail = request.Owner.Email.Trim();
        var whatsAppPhoneNumberId = request.Restaurant.WhatsAppPhoneNumberId.Trim();

        if (await userManager.FindByEmailAsync(ownerEmail) is not null)
        {
            return CreateRestaurantWithOwnerResult.Failed(["An account already exists for the owner email."]);
        }

        if (await dbContext.Restaurants.AnyAsync(x => x.WhatsAppPhoneNumberId == whatsAppPhoneNumberId, cancellationToken))
        {
            return CreateRestaurantWithOwnerResult.Failed(["Another business already uses this WhatsApp Phone Number ID."]);
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var restaurant = await restaurantService.CreateRestaurantAsync(
            request.Restaurant with { WhatsAppPhoneNumberId = whatsAppPhoneNumberId },
            cancellationToken);
        var ownerResult = await CreateOwnerUserInternalAsync(restaurant.Id, request.Owner);

        if (!ownerResult.Succeeded || ownerResult.User is null)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return CreateRestaurantWithOwnerResult.Failed(ownerResult.Errors);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return CreateRestaurantWithOwnerResult.Success(restaurant, ownerResult.User);
    }

    public async Task<CreateStaffUserResult> CreateBusinessOwnerAsync(
        Guid businessId,
        CreateOwnerUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Restaurants.AnyAsync(x => x.Id == businessId, cancellationToken))
        {
            return CreateStaffUserResult.Failed(["Business was not found."]);
        }

        return await CreateOwnerUserInternalAsync(businessId, request);
    }

    public async Task<ResetUserPasswordResult> ResetBusinessUserPasswordAsync(
        Guid businessId,
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null || user.BusinessId != businessId)
        {
            return ResetUserPasswordResult.Failed(["User account was not found."]);
        }

        var validationErrors = new List<string>();

        foreach (var validator in userManager.PasswordValidators)
        {
            var validationResult = await validator.ValidateAsync(userManager, user, newPassword);

            if (!validationResult.Succeeded)
            {
                validationErrors.AddRange(validationResult.Errors.Select(error => error.Description));
            }
        }

        if (validationErrors.Count > 0)
        {
            return ResetUserPasswordResult.Failed(validationErrors);
        }

        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, newPassword);
        var result = await userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            result = await userManager.UpdateSecurityStampAsync(user);
        }

        return result.Succeeded
            ? ResetUserPasswordResult.Success(await ToStaffUserResponseAsync(user))
            : ResetUserPasswordResult.Failed(result.Errors.Select(error => error.Description));
    }

    private async Task<CreateStaffUserResult> CreateOwnerUserInternalAsync(
        Guid businessId,
        CreateOwnerUserRequest request)
    {
        var email = request.Email.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return CreateStaffUserResult.Failed(["An account already exists for this email."]);
        }

        var owner = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = request.DisplayName.Trim(),
            BusinessId = businessId,
            IsActive = true
        };

        var result = await userManager.CreateAsync(owner, request.Password);

        if (result.Succeeded)
        {
            result = await userManager.AddToRoleAsync(owner, AppRoles.BusinessOwner);
        }

        return result.Succeeded
            ? CreateStaffUserResult.Success(await ToStaffUserResponseAsync(owner))
            : CreateStaffUserResult.Failed(result.Errors.Select(error => error.Description));
    }

    private async Task<StaffUserResponse> ToStaffUserResponseAsync(ApplicationUser user)
    {
        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Unassigned";
        return new StaffUserResponse(
            user.Id,
            user.BusinessId,
            user.DisplayName,
            user.Email ?? string.Empty,
            role,
            user.IsActive);
    }
}
