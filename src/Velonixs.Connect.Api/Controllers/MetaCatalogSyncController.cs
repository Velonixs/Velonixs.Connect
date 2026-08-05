using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.PlatformAdmin)]
public sealed class MetaCatalogSyncController(
    IMetaCatalogSyncService syncService,
    ApiBusinessAccessService access) : ControllerBase
{
    [HttpGet("api/businesses/{businessId:guid}/meta-sync/settings")]
    public async Task<IActionResult> GetSettings(Guid businessId, CancellationToken cancellationToken)
    {
        if (!access.CanManageMetaCatalog(businessId))
        {
            return Forbid();
        }

        return Ok(await syncService.GetSettingsAsync(businessId, cancellationToken));
    }

    [HttpPut("api/businesses/{businessId:guid}/meta-sync/settings")]
    public async Task<IActionResult> SaveSettings(
        Guid businessId,
        [FromBody] MetaCatalogSettingsInput input,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageMetaCatalog(businessId))
        {
            return Forbid();
        }

        await syncService.SaveSettingsAsync(businessId, input, cancellationToken);
        return NoContent();
    }

    [HttpGet("api/businesses/{businessId:guid}/meta-sync/queue")]
    public async Task<IActionResult> GetQueue(Guid businessId, CancellationToken cancellationToken)
    {
        if (!access.CanManageMetaCatalog(businessId))
        {
            return Forbid();
        }

        return Ok(await syncService.GetQueueAsync(businessId, cancellationToken));
    }

    [HttpGet("api/businesses/{businessId:guid}/meta-sync/logs")]
    public async Task<IActionResult> GetLogs(Guid businessId, CancellationToken cancellationToken)
    {
        if (!access.CanManageMetaCatalog(businessId))
        {
            return Forbid();
        }

        return Ok(await syncService.GetLogsAsync(businessId, cancellationToken));
    }

    [HttpPost("api/businesses/{businessId:guid}/meta-sync/sync-all")]
    public async Task<IActionResult> SyncAll(Guid businessId, CancellationToken cancellationToken)
    {
        if (!access.CanManageMetaCatalog(businessId))
        {
            return Forbid();
        }

        var count = await syncService.QueueAllProductsAsync(businessId, cancellationToken);
        return Ok(new { queued = count });
    }

    [HttpPost("api/businesses/{businessId:guid}/meta-sync/queue/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid businessId, Guid id, CancellationToken cancellationToken)
    {
        if (!access.CanManageMetaCatalog(businessId))
        {
            return Forbid();
        }

        var wasUpdated = await syncService.RetryQueueItemAsync(businessId, id, cancellationToken);
        return wasUpdated ? NoContent() : NotFound();
    }

    [HttpPost("api/meta-sync/process")]
    [Authorize(Roles = AppRoles.PlatformAdmin)]
    public async Task<IActionResult> ProcessPending(CancellationToken cancellationToken)
    {
        await syncService.ProcessPendingAsync(cancellationToken);
        return NoContent();
    }
}
