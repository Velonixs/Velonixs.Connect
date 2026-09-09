using System.Security.Claims;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Api.Mcp;

internal static class McpTenantResolver
{
    public const string AllTenantRoles =
        AppRoles.PlatformAdmin + "," + AppRoles.BusinessOwner + "," + AppRoles.BusinessManager + "," + AppRoles.Cashier + "," + AppRoles.Staff;

    public const string CatalogManagerRoles =
        AppRoles.PlatformAdmin + "," + AppRoles.BusinessOwner + "," + AppRoles.BusinessManager;

    public static Guid Resolve(ClaimsPrincipal? user, params string[] permittedRoles)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("An authenticated MCP caller is required.");
        }

        if (permittedRoles.Length > 0 && !permittedRoles.Any(user.IsInRole))
        {
            throw new UnauthorizedAccessException("The MCP caller is not authorized for this operation.");
        }

        var value = user.FindFirstValue(AppClaimTypes.BusinessId);
        if (!Guid.TryParse(value, out var restaurantId) || restaurantId == Guid.Empty)
        {
            // Platform administrators must use a tenant-bound service identity
            // for MCP. This prevents an unscoped admin token from becoming a
            // cross-tenant data plane.
            throw new UnauthorizedAccessException("The MCP identity is not bound to a restaurant tenant.");
        }

        return restaurantId;
    }
}
