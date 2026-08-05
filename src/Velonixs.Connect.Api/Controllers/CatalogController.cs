using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize]
public sealed class CatalogController(
    ICatalogService catalogService,
    ApiBusinessAccessService access) : ControllerBase
{
    [HttpGet("api/businesses/{businessId:guid}/catalog")]
    public async Task<IActionResult> GetCatalog(Guid businessId, CancellationToken cancellationToken)
    {
        if (!access.CanReadBusiness(businessId))
        {
            return Forbid();
        }

        var catalog = await catalogService.GetCatalogAsync(businessId, cancellationToken);
        return catalog is null ? NotFound() : Ok(catalog);
    }

    [HttpPost("api/businesses/{businessId:guid}/catalog-categories")]
    public async Task<IActionResult> CreateCategory(
        Guid businessId,
        [FromBody] CreateCatalogCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageCatalog(businessId))
        {
            return Forbid();
        }

        var category = await catalogService.CreateCategoryAsync(businessId, request, cancellationToken);
        return Ok(category);
    }

    [HttpPut("api/catalog-categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(
        Guid id,
        [FromBody] UpdateCatalogCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!await access.CanManageCategoryAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var category = await catalogService.UpdateCategoryAsync(id, request, cancellationToken);

        return category is null ? NotFound() : Ok(category);
    }

    [HttpPost("api/catalog-categories/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateCategory(Guid id, CancellationToken cancellationToken)
    {
        if (!await access.CanManageCategoryAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var wasUpdated = await catalogService.DeactivateCategoryAsync(id, cancellationToken);
        return wasUpdated ? NoContent() : NotFound();
    }

    [HttpDelete("api/catalog-categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        if (!await access.CanManageCategoryAsync(id, cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var wasUpdated = await catalogService.DeleteCategoryAsync(id, cancellationToken);
            return wasUpdated ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("api/businesses/{businessId:guid}/catalog-products")]
    public async Task<IActionResult> CreateProduct(
        Guid businessId,
        [FromBody] CreateCatalogProductRequest request,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageCatalog(businessId))
        {
            return Forbid();
        }

        var product = await catalogService.CreateProductAsync(businessId, request, cancellationToken);
        return Ok(product);
    }

    [HttpPut("api/catalog-products/{id:guid}")]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateCatalogProductRequest request,
        CancellationToken cancellationToken)
    {
        if (!await access.CanManageProductAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var product = await catalogService.UpdateProductAsync(id, request, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpDelete("api/catalog-products/{id:guid}")]
    public async Task<IActionResult> DeactivateProduct(Guid id, CancellationToken cancellationToken)
    {
        if (!await access.CanManageProductAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var wasUpdated = await catalogService.DeactivateProductAsync(id, cancellationToken);
        return wasUpdated ? NoContent() : NotFound();
    }
}
