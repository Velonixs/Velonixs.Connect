using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/restaurants")]
public sealed class RestaurantsController(
    IRestaurantService restaurantService,
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
}
