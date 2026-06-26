using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Contracts.Common;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.PlatformAdmin)]
[Route("api/v1/master-catalog")]
public sealed class MasterCatalogController(IMenuService menuService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMasterCatalog(CancellationToken cancellationToken)
    {
        return Ok(await menuService.GetMasterCatalogAsync(cancellationToken));
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateMasterMenuCategoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await menuService.CreateMasterCategoryAsync(request, cancellationToken));
        }
        catch (DbUpdateException)
        {
            return BadRequest(new ApiErrorResponse(
                "duplicate_master_category",
                "A master category with this name already exists.",
                HttpContext.TraceIdentifier));
        }
    }

    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(
        Guid id,
        [FromBody] CreateMasterMenuCategoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var category = await menuService.UpdateMasterCategoryAsync(id, request, cancellationToken);
            return category is null ? NotFound() : Ok(category);
        }
        catch (DbUpdateException)
        {
            return BadRequest(new ApiErrorResponse(
                "duplicate_master_category",
                "A master category with this name already exists.",
                HttpContext.TraceIdentifier));
        }
    }

    [HttpDelete("categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await menuService.DeleteMasterCategoryAsync(id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(
                "master_category_in_use",
                ex.Message,
                HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem(
        [FromBody] CreateMasterMenuItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await menuService.CreateMasterItemAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse("invalid_master_item", ex.Message, HttpContext.TraceIdentifier));
        }
        catch (DbUpdateException)
        {
            return BadRequest(new ApiErrorResponse(
                "duplicate_master_item",
                "A master item with this name already exists in the selected category.",
                HttpContext.TraceIdentifier));
        }
    }

    [HttpPut("items/{id:guid}")]
    public async Task<IActionResult> UpdateItem(
        Guid id,
        [FromBody] CreateMasterMenuItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await menuService.UpdateMasterItemAsync(id, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse("invalid_master_item", ex.Message, HttpContext.TraceIdentifier));
        }
        catch (DbUpdateException)
        {
            return BadRequest(new ApiErrorResponse(
                "duplicate_master_item",
                "A master item with this name already exists in the selected category.",
                HttpContext.TraceIdentifier));
        }
    }

    [HttpDelete("items/{id:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await menuService.DeleteMasterItemAsync(id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse("master_item_in_use", ex.Message, HttpContext.TraceIdentifier));
        }
    }
}
