using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Contracts.Common;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/staff")]
public sealed class StaffController(
    IStaffService staffService,
    ApiBusinessAccessService access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetStaff(
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

        if (!access.CanReadBusinessDetails(businessId))
        {
            return Forbid();
        }

        return Ok(await staffService.GetBusinessStaffAsync(businessId, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.BusinessOwner + "," + AppRoles.PlatformAdmin)]
    public async Task<IActionResult> CreateStaff(
        [FromQuery] Guid? restaurantId,
        [FromBody] CreateStaffUserRequest request,
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

        if (!access.CanReadBusinessDetails(businessId))
        {
            return Forbid();
        }

        var result = await staffService.CreateStaffUserAsync(businessId, request, cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetStaff), new { restaurantId = businessId }, result.User)
            : BadRequest(new ApiErrorResponse("staff_create_failed", string.Join(" ", result.Errors), HttpContext.TraceIdentifier));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = AppRoles.BusinessOwner + "," + AppRoles.PlatformAdmin)]
    public async Task<IActionResult> UpdateStaffStatus(
        Guid id,
        [FromQuery] Guid? restaurantId,
        [FromBody] UpdateStaffStatusRequest request,
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

        if (!access.CanReadBusinessDetails(businessId))
        {
            return Forbid();
        }

        var currentUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
            ? parsed
            : (Guid?)null;
        var result = await staffService.UpdateStaffStatusAsync(
            businessId,
            id,
            request.IsActive,
            currentUserId,
            cancellationToken);

        return result.Succeeded
            ? Ok(result.User)
            : BadRequest(new ApiErrorResponse("staff_status_failed", string.Join(" ", result.Errors), HttpContext.TraceIdentifier));
    }
}
