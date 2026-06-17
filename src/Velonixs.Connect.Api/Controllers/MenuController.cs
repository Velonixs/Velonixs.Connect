using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
public sealed class MenuController(IMenuService menuService) : ControllerBase
{
    [HttpGet("api/restaurants/{restaurantId:guid}/menu")]
    public async Task<IActionResult> GetMenu(Guid restaurantId, CancellationToken cancellationToken)
    {
        var menu = await menuService.GetMenuAsync(restaurantId, cancellationToken);
        return menu is null ? NotFound() : Ok(menu);
    }

    [HttpPost("api/restaurants/{restaurantId:guid}/menu-categories")]
    public async Task<IActionResult> CreateCategory(
        Guid restaurantId,
        [FromBody] CreateMenuCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await menuService.CreateCategoryAsync(restaurantId, request, cancellationToken);
        return Ok(category);
    }

    [HttpPost("api/restaurants/{restaurantId:guid}/menu-items")]
    public async Task<IActionResult> CreateItem(
        Guid restaurantId,
        [FromBody] CreateMenuItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await menuService.CreateItemAsync(restaurantId, request, cancellationToken);
        return Ok(item);
    }

    [HttpPut("api/menu-items/{id:guid}")]
    public async Task<IActionResult> UpdateItem(
        Guid id,
        [FromBody] UpdateMenuItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await menuService.UpdateItemAsync(id, request, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("api/menu-items/{id:guid}")]
    public async Task<IActionResult> DeactivateItem(Guid id, CancellationToken cancellationToken)
    {
        var wasUpdated = await menuService.DeactivateItemAsync(id, cancellationToken);
        return wasUpdated ? NoContent() : NotFound();
    }
}
