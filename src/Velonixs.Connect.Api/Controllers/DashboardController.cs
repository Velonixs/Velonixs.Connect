using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Contracts.Common;
using Velonixs.Connect.Contracts.Restaurants;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardController(
    IDashboardService dashboardService,
    IAdminService adminService,
    ApiBusinessAccessService access) : ControllerBase
{
    [HttpGet("platform")]
    [Authorize(Roles = AppRoles.PlatformAdmin)]
    public async Task<IActionResult> GetPlatformSummary(CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetPlatformDashboardAsync(cancellationToken));
    }

    [HttpPatch("platform/tax-settings")]
    [Authorize(Roles = AppRoles.PlatformAdmin)]
    public async Task<IActionResult> UpdatePlatformTaxSettings(
        [FromBody] UpdateRestaurantTaxSettingRequest request,
        [FromServices] IRestaurantService restaurantService,
        CancellationToken cancellationToken)
    {
        return Ok(await restaurantService.UpdatePlatformTaxSettingAsync(
            request.CgstPercent,
            request.SgstPercent,
            cancellationToken));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Guid? restaurantId,
        CancellationToken cancellationToken)
    {
        var effectiveRestaurantId = restaurantId ?? access.AssignedBusinessId;

        if (effectiveRestaurantId is not Guid businessId)
        {
            return BadRequest(new ApiErrorResponse(
                "restaurant_required",
                "A restaurantId query value is required for platform administrators.",
                HttpContext.TraceIdentifier));
        }

        if (!access.CanManageOrders(businessId))
        {
            return Forbid();
        }

        var summary = await dashboardService.GetRestaurantDashboardAsync(businessId, cancellationToken: cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }
}
