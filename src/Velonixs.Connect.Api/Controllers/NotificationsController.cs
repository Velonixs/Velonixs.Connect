using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Contracts.Common;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsController(
    INotificationQueryService notificationQueryService,
    ApiBusinessAccessService access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] Guid? restaurantId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var effectiveRestaurantId = restaurantId ?? access.AssignedBusinessId;

        if (effectiveRestaurantId is not Guid businessId)
        {
            return BadRequest(new ApiErrorResponse(
                "restaurant_required",
                "A restaurantId query value is required for platform administrators.",
                HttpContext.TraceIdentifier));
        }

        if (!access.CanReadBusiness(businessId))
        {
            return Forbid();
        }

        return Ok(await notificationQueryService.GetRecentNotificationsAsync(
            businessId,
            take,
            cancellationToken));
    }
}
