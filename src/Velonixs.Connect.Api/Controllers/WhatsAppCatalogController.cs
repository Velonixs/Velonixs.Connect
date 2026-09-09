using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Application.Abstractions;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize]
public sealed class WhatsAppCatalogController(
    IWhatsAppCatalogService catalogService,
    IWhatsAppCommerceService commerceService,
    IMetaCatalogService metaCatalogService,
    ApiBusinessAccessService access) : ControllerBase
{
    [HttpPost("api/restaurants/{restaurantId:guid}/whatsapp/catalog/send")]
    public Task<IActionResult> Send(
        Guid restaurantId,
        [FromBody] SendCatalogRequest request,
        CancellationToken cancellationToken) =>
        SendCoreAsync(restaurantId, request, false, cancellationToken);

    [HttpPost("api/restaurants/{restaurantId:guid}/whatsapp/catalog/test")]
    public Task<IActionResult> Test(
        Guid restaurantId,
        [FromBody] SendCatalogRequest request,
        CancellationToken cancellationToken) =>
        SendCoreAsync(restaurantId, request, true, cancellationToken);

    [HttpGet("api/restaurants/{restaurantId:guid}/whatsapp/commerce/diagnostics")]
    public async Task<IActionResult> GetDiagnostics(
        Guid restaurantId,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageCatalog(restaurantId))
        {
            return Forbid();
        }

        return Ok(await commerceService.ValidateCatalogConnectionAsync(restaurantId, cancellationToken));
    }

    [HttpPut("api/restaurants/{restaurantId:guid}/whatsapp/commerce/settings")]
    public async Task<IActionResult> UpdateCommerceSettings(
        Guid restaurantId,
        [FromBody] UpdateCommerceSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageCatalog(restaurantId))
        {
            return Forbid();
        }

        return Ok(await commerceService.UpdateCommerceSettingsAsync(
            restaurantId,
            request.IsCatalogVisible,
            request.IsCartEnabled,
            cancellationToken));
    }

    [HttpGet("api/restaurants/{restaurantId:guid}/catalog/status")]
    public async Task<IActionResult> GetCatalogStatus(
        Guid restaurantId,
        CancellationToken cancellationToken)
    {
        if (!access.CanReadBusiness(restaurantId))
        {
            return Forbid();
        }

        return Ok(await metaCatalogService.GetCatalogSyncStatusAsync(restaurantId, cancellationToken));
    }

    private async Task<IActionResult> SendCoreAsync(
        Guid restaurantId,
        SendCatalogRequest request,
        bool isTest,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageCatalog(restaurantId))
        {
            return Forbid();
        }

        var result = await catalogService.SendCatalogMessageAsync(
            restaurantId,
            request.CustomerPhoneNumber,
            isTest,
            cancellationToken);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}

public sealed record SendCatalogRequest(string CustomerPhoneNumber);

public sealed record UpdateCommerceSettingsRequest(bool IsCatalogVisible, bool IsCartEnabled);
