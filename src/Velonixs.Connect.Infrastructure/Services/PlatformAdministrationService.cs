using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of platform administration use cases. It is
/// the sole place where the presentation layer's former EF/Identity work is
/// performed, keeping UI components focused on DTO-based commands and views.
/// </summary>
public sealed class PlatformAdministrationService : IPlatformAdministrationService
{
    private readonly RestaurantConnectDbContext dbContext;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly IDbContextFactory<RestaurantConnectDbContext>? dbContextFactory;
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    // Retained for focused service tests and non-Blazor hosts that do not
    // register a context factory.
    public PlatformAdministrationService(
        RestaurantConnectDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
    }

    public PlatformAdministrationService(
        RestaurantConnectDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<RestaurantConnectDbContext> dbContextFactory)
        : this(dbContext, userManager)
    {
        this.dbContextFactory = dbContextFactory;
    }

    public async Task<PlatformDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await _dbLock.WaitAsync(cancellationToken);
        RestaurantConnectDbContext? isolatedContext = null;
        try
        {
            // A Blazor layout and its page initialize independently. Use a
            // short-lived context for the layout dashboard read so it cannot
            // overlap the page's scoped context operations.
            isolatedContext = dbContextFactory is null
                ? null
                : await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var context = isolatedContext ?? dbContext;

            var businesses = await context.Restaurants
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Name, x.BusinessType, x.IsActive })
                .ToArrayAsync(cancellationToken);

            var productCounts = await context.MenuItems
                .AsNoTracking()
                .Where(x => x.IsActive)
                .GroupBy(x => x.RestaurantId)
                .Select(x => new { BusinessId = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.BusinessId, x => x.Count, cancellationToken);
            var orderCounts = await context.Orders
                .AsNoTracking()
                .GroupBy(x => x.RestaurantId)
                .Select(x => new { BusinessId = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.BusinessId, x => x.Count, cancellationToken);

            var today = DateTimeOffset.UtcNow.Date;
            var activeProductCount = await context.MenuItems.CountAsync(x => x.IsActive && x.IsAvailable, cancellationToken);
            var pendingCatalogSyncCount = await context.CatalogSyncQueue.CountAsync(
                x => x.Status == "Pending" || x.Status == "Failed",
                cancellationToken);
            var todaysOrderCount = await context.Orders.CountAsync(x => x.CreatedAt >= today, cancellationToken);
            var totalRevenue = await context.Orders.SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;
            return new PlatformDashboardResponse(
                businesses.Select(x => new PlatformBusinessSummary(
                    x.Id,
                    x.Name,
                    x.BusinessType,
                    x.IsActive,
                    productCounts.GetValueOrDefault(x.Id),
                    orderCounts.GetValueOrDefault(x.Id))).ToArray(),
                activeProductCount,
                pendingCatalogSyncCount,
                todaysOrderCount,
                totalRevenue);
        }
        finally
        {
            if (isolatedContext is not null)
            {
                await isolatedContext.DisposeAsync();
            }
            _dbLock.Release();
        }
    }

    public async Task<RestaurantResponse> CreateBusinessWithOwnerAsync(
        CreateBusinessWithOwnerRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateBusinessRequest(request);
        var phoneNumberId = request.WhatsAppPhoneNumberId.Trim();
        var ownerEmail = request.OwnerEmail.Trim();

        if (await dbContext.Restaurants.AnyAsync(x => x.WhatsAppPhoneNumberId == phoneNumberId, cancellationToken))
        {
            throw new InvalidOperationException("Another business already uses this WhatsApp Phone Number ID.");
        }

        if (await userManager.FindByEmailAsync(ownerEmail) is not null)
        {
            throw new InvalidOperationException("An account already exists for the owner email.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var restaurant = new Restaurant
            {
                Name = request.Name.Trim(),
                BusinessType = BusinessTypes.Normalize(request.BusinessType),
                WhatsAppPhoneNumberId = phoneNumberId,
                BusinessPhone = NormalizeOptional(request.BusinessPhone),
                NotificationEmail = NormalizeOptional(request.NotificationEmail),
                StaffWhatsAppNumber = NormalizeOptional(request.StaffWhatsAppNumber),
                // This legacy mirror is written by SaveSettingsAsync only.
                // Retaining the request property keeps existing API clients
                // source-compatible without allowing an unreconciled catalog
                // switch during business creation.
                Address = NormalizeOptional(request.Address),
                CgstPercent = request.CgstPercent,
                SgstPercent = request.SgstPercent,
                IsActive = request.IsActive
            };
            dbContext.Restaurants.Add(restaurant);
            await dbContext.SaveChangesAsync(cancellationToken);

            await CreateBusinessUserAsync(
                restaurant.Id,
                request.OwnerDisplayName,
                ownerEmail,
                request.OwnerPassword,
                AppRoles.BusinessOwner);

            await transaction.CommitAsync(cancellationToken);
            return ToRestaurantResponse(restaurant);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyCollection<BusinessUserResponse>> GetBusinessUsersAsync(
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        var users = await userManager.Users
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DisplayName)
            .ToArrayAsync(cancellationToken);
        var responses = new List<BusinessUserResponse>(users.Length);

        foreach (var user in users)
        {
            var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Unassigned";
            responses.Add(new BusinessUserResponse(
                user.Id,
                businessId,
                user.DisplayName,
                user.Email ?? string.Empty,
                role,
                user.IsActive));
        }

        return responses;
    }

    public async Task<BusinessUserResponse> CreateBusinessOwnerAsync(
        Guid businessId,
        CreateBusinessOwnerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (businessId == Guid.Empty || !await dbContext.Restaurants.AnyAsync(x => x.Id == businessId, cancellationToken))
        {
            throw new KeyNotFoundException("Business was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Owner name, email, and password are required.");
        }

        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new InvalidOperationException("An account already exists for this email.");
        }

        var user = await CreateBusinessUserAsync(
            businessId,
            request.DisplayName,
            email,
            request.Password,
            AppRoles.BusinessOwner);
        return user;
    }

    public async Task ResetBusinessUserPasswordAsync(
        Guid businessId,
        Guid userId,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(temporaryPassword))
        {
            throw new InvalidOperationException("Enter a temporary password.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.BusinessId != businessId)
        {
            throw new KeyNotFoundException("User was not found for this business.");
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, temporaryPassword);
        EnsureSucceeded(result);
    }

    public async Task<IReadOnlyCollection<CustomerSummaryResponse>> GetCustomerSummariesAsync(
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.RestaurantId == businessId)
            .OrderByDescending(x => x.LastInteractionAt)
            .Select(x => new CustomerSummaryResponse(
                x.Id,
                x.Name,
                x.PhoneNumber,
                x.LastAddress,
                dbContext.Orders.Count(order => order.CustomerId == x.Id),
                x.LastInteractionAt))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<BusinessUserResponse> CreateBusinessUserAsync(
        Guid businessId,
        string displayName,
        string email,
        string password,
        string role)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName.Trim(),
            BusinessId = businessId,
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, password);
        EnsureSucceeded(result);
        result = await userManager.AddToRoleAsync(user, role);
        EnsureSucceeded(result);

        return new BusinessUserResponse(user.Id, businessId, user.DisplayName, user.Email ?? string.Empty, role, user.IsActive);
    }

    private static void ValidateBusinessRequest(CreateBusinessWithOwnerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.WhatsAppPhoneNumberId) ||
            string.IsNullOrWhiteSpace(request.OwnerDisplayName) ||
            string.IsNullOrWhiteSpace(request.OwnerEmail) ||
            string.IsNullOrWhiteSpace(request.OwnerPassword))
        {
            throw new InvalidOperationException("Business, WhatsApp Phone ID, and owner account details are required.");
        }

        if (request.CgstPercent is < 0 or > 100 || request.SgstPercent is < 0 or > 100)
        {
            throw new InvalidOperationException("CGST and SGST must be between 0 and 100.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RestaurantResponse ToRestaurantResponse(Restaurant restaurant) =>
        new(
            restaurant.Id,
            restaurant.Name,
            restaurant.BusinessType,
            restaurant.WhatsAppPhoneNumberId,
            restaurant.BusinessPhone,
            restaurant.NotificationEmail,
            restaurant.StaffWhatsAppNumber,
            restaurant.Address,
            restaurant.CgstPercent,
            restaurant.SgstPercent,
            restaurant.IsActive,
            restaurant.CreatedAt,
            restaurant.WhatsAppCatalogId);
}
