using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Contracts.Common;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/menu-imports")]
public sealed class MenuImportsController(
    IMenuImportService menuImportService,
    ApiBusinessAccessService access) : ControllerBase
{
    [HttpPost("excel")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> ImportExcel(
        [FromQuery] Guid? restaurantId,
        IFormFile file,
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

        if (!access.CanManageCatalog(businessId))
        {
            return Forbid();
        }

        if (file.Length == 0)
        {
            return BadRequest(new ApiErrorResponse(
                "empty_file",
                "Upload a non-empty Excel workbook.",
                HttpContext.TraceIdentifier));
        }

        await using var stream = file.OpenReadStream();
        var result = await menuImportService.ImportExcelAsync(businessId, stream, cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result);
        }

        var rowErrors = result.Errors
            .GroupBy(error => $"Row {error.RowNumber}")
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray());

        return BadRequest(new ApiErrorResponse(
            "menu_import_failed",
            "Menu import contains invalid rows.",
            HttpContext.TraceIdentifier,
            rowErrors));
    }
}
