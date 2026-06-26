using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Contracts.Menu;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize]
public sealed class MenuController(
    IMenuService menuService,
    ApiBusinessAccessService access) : ControllerBase
{
    [HttpGet("api/restaurants/{restaurantId:guid}/menu")]
    [HttpGet("api/v1/restaurants/{restaurantId:guid}/menu")]
    public async Task<IActionResult> GetMenu(Guid restaurantId, CancellationToken cancellationToken)
    {
        if (!access.CanReadBusiness(restaurantId))
        {
            return Forbid();
        }

        var menu = await menuService.GetMenuAsync(restaurantId, cancellationToken);
        return menu is null ? NotFound() : Ok(menu);
    }

    [HttpPost("api/restaurants/{restaurantId:guid}/menu-categories")]
    [HttpPost("api/v1/restaurants/{restaurantId:guid}/menu-categories")]
    [HttpPost("api/v1/categories")]
    public async Task<IActionResult> CreateCategory(
        Guid restaurantId,
        [FromBody] CreateMenuCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageCatalog(restaurantId))
        {
            return Forbid();
        }

        var category = await menuService.CreateCategoryAsync(restaurantId, request, cancellationToken);
        return Ok(category);
    }

    [HttpPost("api/restaurants/{restaurantId:guid}/menu-items")]
    [HttpPost("api/v1/restaurants/{restaurantId:guid}/menu-items")]
    public async Task<IActionResult> CreateItem(
        Guid restaurantId,
        [FromBody] CreateMenuItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageCatalog(restaurantId))
        {
            return Forbid();
        }

        var item = await menuService.CreateItemAsync(restaurantId, request, cancellationToken);
        return Ok(item);
    }

    [HttpPut("api/menu-items/{id:guid}")]
    [HttpPut("api/v1/menu-items/{id:guid}")]
    public async Task<IActionResult> UpdateItem(
        Guid id,
        [FromBody] UpdateMenuItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!await access.CanManageProductAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var item = await menuService.UpdateItemAsync(id, request, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPatch("api/v1/menu-items/{id:guid}/availability")]
    public async Task<IActionResult> UpdateAvailability(
        Guid id,
        [FromBody] UpdateMenuItemAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        if (!await access.CanManageProductAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var item = await menuService.UpdateItemAvailabilityAsync(id, request.IsAvailable, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("api/menu-items/{id:guid}")]
    [HttpDelete("api/v1/menu-items/{id:guid}")]
    public async Task<IActionResult> DeactivateItem(Guid id, CancellationToken cancellationToken)
    {
        if (!await access.CanManageProductAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var wasUpdated = await menuService.DeactivateItemAsync(id, cancellationToken);
        return wasUpdated ? NoContent() : NotFound();
    }
}
