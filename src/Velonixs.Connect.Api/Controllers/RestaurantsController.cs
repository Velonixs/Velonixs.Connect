using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Contracts.Common;
using Velonixs.Connect.Contracts.Restaurants;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/restaurants")]
[Route("api/v1/restaurants")]
public sealed class RestaurantsController(
    IRestaurantService restaurantService,
    IAdminService adminService,
    ApiBusinessAccessService access) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = AppRoles.PlatformAdmin)]
    public async Task<IActionResult> GetRestaurants(CancellationToken cancellationToken)
    {
        return Ok(await restaurantService.GetRestaurantsAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRestaurant(Guid id, CancellationToken cancellationToken)
    {
        if (!access.CanReadBusinessDetails(id))
        {
            return Forbid();
        }

        var restaurant = await restaurantService.GetRestaurantAsync(id, cancellationToken);
        return restaurant is null ? NotFound() : Ok(restaurant);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.PlatformAdmin)]
    public async Task<IActionResult> CreateRestaurant(
        [FromBody] CreateRestaurantRequest request,
        CancellationToken cancellationToken)
    {
        var restaurant = await restaurantService.CreateRestaurantAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetRestaurant), new { id = restaurant.Id }, restaurant);
    }

    [HttpPost("onboard")]
    [Authorize(Roles = AppRoles.PlatformAdmin)]
    public async Task<IActionResult> OnboardRestaurant(
        [FromBody] CreateRestaurantWithOwnerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminService.CreateRestaurantWithOwnerAsync(request, cancellationToken);

        if (!result.Succeeded || result.Restaurant is null)
        {
            return BadRequest(new ApiErrorResponse(
                "restaurant_onboarding_failed",
                string.Join(" ", result.Errors),
                HttpContext.TraceIdentifier));
        }

        return CreatedAtAction(nameof(GetRestaurant), new { id = result.Restaurant.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.PlatformAdmin)]
    public async Task<IActionResult> UpdateRestaurant(
        Guid id,
        [FromBody] UpdateRestaurantRequest request,
        CancellationToken cancellationToken)
    {
        var restaurant = await restaurantService.UpdateRestaurantAsync(id, request, cancellationToken);
        return restaurant is null ? NotFound() : Ok(restaurant);
    }

    [HttpPatch("{id:guid}/tax-settings")]
    public async Task<IActionResult> UpdateTaxSettings(
        Guid id,
        [FromBody] UpdateRestaurantTaxSettingRequest request,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageCatalog(id))
        {
            return Forbid();
        }

        var restaurant = await restaurantService.UpdateRestaurantTaxSettingAsync(
            id,
            request.CgstPercent,
            request.SgstPercent,
            cancellationToken);

        return restaurant is null ? NotFound() : Ok(restaurant);
    }

    [HttpPatch("{id:guid}/availability")]
    [HttpPatch("~/api/v1/restaurant-availability/{id:guid}")]
    public async Task<IActionResult> UpdateAvailability(
        Guid id,
        [FromBody] UpdateRestaurantAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageCatalog(id))
        {
            return Forbid();
        }

        var restaurant = await restaurantService.UpdateRestaurantAvailabilityAsync(
            id,
            request.IsActive,
            cancellationToken);

        return restaurant is null ? NotFound() : Ok(restaurant);
    }
}
