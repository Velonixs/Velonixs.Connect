using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Api.Security;

public sealed class ApiBusinessAccessService(
    IHttpContextAccessor httpContextAccessor,
    RestaurantConnectDbContext dbContext)
{
    private ClaimsPrincipal User =>
        httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("The current HTTP user is unavailable.");

    public bool IsPlatformAdmin => User.IsInRole(AppRoles.PlatformAdmin);

    public bool CanReadBusiness(Guid businessId)
    {
        return IsPlatformAdmin || GetBusinessId() == businessId;
    }

    public bool CanReadBusinessDetails(Guid businessId)
    {
        return CanReadBusiness(businessId) &&
               (IsPlatformAdmin ||
                User.IsInRole(AppRoles.BusinessOwner) ||
                User.IsInRole(AppRoles.BusinessManager));
    }

    public bool CanManageBusiness()
    {
        return IsPlatformAdmin;
    }

    public bool CanManageCatalog(Guid businessId)
    {
        return CanReadBusiness(businessId) &&
               (IsPlatformAdmin ||
                User.IsInRole(AppRoles.BusinessOwner) ||
                User.IsInRole(AppRoles.BusinessManager));
    }

    public bool CanManageOrders(Guid businessId)
    {
        return CanReadBusiness(businessId) &&
               (IsPlatformAdmin ||
                User.IsInRole(AppRoles.BusinessOwner) ||
                User.IsInRole(AppRoles.BusinessManager) ||
                User.IsInRole(AppRoles.Cashier) ||
                User.IsInRole(AppRoles.Staff));
    }

    public async Task<bool> CanReadOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var businessId = await dbContext.Orders
            .AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => (Guid?)x.RestaurantId)
            .FirstOrDefaultAsync(cancellationToken);

        return businessId is Guid id && CanManageOrders(id);
    }

    public async Task<bool> CanManageProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        var businessId = await dbContext.MenuItems
            .AsNoTracking()
            .Where(x => x.Id == productId)
            .Select(x => (Guid?)x.RestaurantId)
            .FirstOrDefaultAsync(cancellationToken);

        return businessId is Guid id && CanManageCatalog(id);
    }

    private Guid? GetBusinessId()
    {
        var claim = User.FindFirstValue(AppClaimTypes.BusinessId);
        return Guid.TryParse(claim, out var businessId) ? businessId : null;
    }
}
