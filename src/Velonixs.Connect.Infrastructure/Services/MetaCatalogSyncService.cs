using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Infrastructure.Services;

/// <summary>
/// Implements the durable outbox used to synchronize one business's local
/// products with its pre-provisioned Meta catalog.
/// </summary>
public sealed class MetaCatalogSyncService(
    RestaurantConnectDbContext dbContext,
    HttpClient httpClient,
    IOptions<MetaCatalogOptions> options,
    ILogger<MetaCatalogSyncService> logger) : IMetaCatalogSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MetaCatalogOptions _options = options.Value;

    public async Task<bool> QueueProductSyncAsync(
        Guid businessId,
        Guid productId,
        string eventType,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (businessId == Guid.Empty || productId == Guid.Empty)
        {
            return false;
        }

        var normalizedEventType = NormalizeEventType(eventType);

        // The filtered unique indexes allow one deliverable row and one waiting
        // successor per product. If two web requests enqueue the same product
        // concurrently, re-read once after the losing insert and coalesce onto
        // the row that won instead of allowing parallel remote delivery. The
        // settings-row confirmation below also makes a concurrent connection
        // change roll this enqueue back rather than allowing stale work to
        // appear after SaveSettings has performed its cancellation sweep.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var queueTransaction = dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null
                ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                var settings = await dbContext.MetaCatalogSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.BusinessId == businessId, cancellationToken);
                if (!CanQueue(settings, force))
                {
                    if (queueTransaction is not null)
                    {
                        await queueTransaction.RollbackAsync(cancellationToken);
                        await queueTransaction.DisposeAsync();
                        queueTransaction = null;
                    }

                    await MarkProductNotQueuedAsync(businessId, productId, cancellationToken);
                    return false;
                }

                var catalogId = settings!.CatalogId!;
                var product = await dbContext.MenuItems
                    .Include(x => x.Category)
                    .FirstOrDefaultAsync(x => x.Id == productId && x.RestaurantId == businessId, cancellationToken);

                if (product is null)
                {
                    if (queueTransaction is not null)
                    {
                        await queueTransaction.RollbackAsync(cancellationToken);
                    }

                    return false;
                }

                product.ProductRetailerId ??= CreateGeneratedRetailerId(businessId, product.Id);
                var snapshot = CatalogProductSnapshot.From(product, normalizedEventType);
                product.SyncStatus = "Pending";
                product.MetaCatalogId = catalogId;
                product.UpdatedAt = DateTimeOffset.UtcNow;

                var queuedItem = await dbContext.CatalogSyncQueue
                    .AsNoTracking()
                    .Where(x => x.BusinessId == businessId &&
                                x.ProductId == productId &&
                                x.CatalogId == catalogId &&
                                (x.Status == "Pending" || x.Status == "Failed" ||
                                  x.Status == "Simulated" || x.Status == "Paused" ||
                                  x.Status == "Waiting"))
                    // If a predecessor has already failed/paused and a successor
                    // exists, preserve the successor as the newest desired state
                    // rather than mutating the predecessor out of delivery order.
                    .OrderByDescending(x => x.Status == "Waiting")
                    .ThenByDescending(x => x.UpdatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (queuedItem is not null)
                {
                    if (!await TryCoalesceQueueItemAsync(
                            queuedItem,
                            snapshot,
                            normalizedEventType,
                            catalogId,
                            cancellationToken))
                    {
                        if (queueTransaction is not null)
                        {
                            await queueTransaction.RollbackAsync(cancellationToken);
                        }

                        dbContext.ChangeTracker.Clear();
                        continue;
                    }
                }
                else
                {
                    var predecessorId = await dbContext.CatalogSyncQueue
                        .AsNoTracking()
                        .Where(x => x.BusinessId == businessId &&
                                    x.ProductId == productId &&
                                    x.Status == "Processing")
                        .OrderByDescending(x => x.UpdatedAt)
                        .Select(x => (Guid?)x.Id)
                        .FirstOrDefaultAsync(cancellationToken);

                    queuedItem = new CatalogSyncQueueItem
                    {
                        BusinessId = businessId,
                        ProductId = productId,
                        CatalogId = catalogId,
                        PredecessorQueueItemId = predecessorId,
                        Status = predecessorId.HasValue ? "Waiting" : "Pending",
                        EventType = normalizedEventType,
                        ProductRetailerId = snapshot.ProductRetailerId,
                        PayloadJson = BuildPayload(snapshot),
                        RetryCount = 0,
                        NextAttemptAt = predecessorId.HasValue ? null : DateTimeOffset.UtcNow,
                        LastError = null,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    dbContext.CatalogSyncQueue.Add(queuedItem);
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                if (dbContext.Database.IsRelational() &&
                    !await ConfirmQueueConfigurationUnchangedAsync(settings, force, cancellationToken))
                {
                    if (queueTransaction is not null)
                    {
                        await queueTransaction.RollbackAsync(cancellationToken);
                        dbContext.ChangeTracker.Clear();
                        continue;
                    }

                    throw new InvalidOperationException(
                        "The catalog connection changed while the product was being queued. Retry the menu change.");
                }

                if (queueTransaction is not null)
                {
                    await queueTransaction.CommitAsync(cancellationToken);
                }

                return true;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                if (queueTransaction is not null)
                {
                    await queueTransaction.RollbackAsync(cancellationToken);
                }

                dbContext.ChangeTracker.Clear();
            }
            finally
            {
                if (queueTransaction is not null)
                {
                    await queueTransaction.DisposeAsync();
                }
            }
        }

        return false;
    }

    private async Task MarkProductNotQueuedAsync(
        Guid businessId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        const string reason = "Superseded by a local product change while catalog sync is disabled or manual.";
        var transaction = dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            if (dbContext.Database.IsRelational())
            {
                await dbContext.CatalogSyncQueue
                    .Where(item => item.BusinessId == businessId && item.ProductId == productId &&
                                   (item.Status == "Pending" || item.Status == "Failed" ||
                                    item.Status == "Processing" || item.Status == "Paused" ||
                                    item.Status == "Simulated" || item.Status == "Waiting"))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, "Cancelled")
                        .SetProperty(item => item.LastError, reason)
                        .SetProperty(item => item.NextAttemptAt, (DateTimeOffset?)null)
                        .SetProperty(item => item.LeaseId, (Guid?)null)
                        .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(item => item.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);
                await dbContext.MenuItems
                    .Where(product => product.Id == productId && product.RestaurantId == businessId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(product => product.SyncStatus, "NotQueued"), cancellationToken);
            }
            else
            {
                var product = await dbContext.MenuItems.FirstOrDefaultAsync(
                    candidate => candidate.Id == productId && candidate.RestaurantId == businessId,
                    cancellationToken);
                if (product is null)
                {
                    return;
                }

                var queueItems = await dbContext.CatalogSyncQueue
                    .Where(item => item.BusinessId == businessId && item.ProductId == productId &&
                                   (item.Status == "Pending" || item.Status == "Failed" ||
                                    item.Status == "Processing" || item.Status == "Paused" ||
                                    item.Status == "Simulated" || item.Status == "Waiting"))
                    .ToArrayAsync(cancellationToken);
                foreach (var queueItem in queueItems)
                {
                    queueItem.Status = "Cancelled";
                    queueItem.LastError = reason;
                    queueItem.NextAttemptAt = null;
                    queueItem.LeaseId = null;
                    queueItem.LeaseExpiresAt = null;
                    queueItem.UpdatedAt = DateTimeOffset.UtcNow;
                }

                product.SyncStatus = "NotQueued";
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<bool> ConfirmQueueConfigurationUnchangedAsync(
        MetaCatalogSetting settings,
        bool force,
        CancellationToken cancellationToken)
    {
        // The no-op UPDATE obtains an update lock until the surrounding queue
        // transaction commits. A settings change either happens first (so this
        // returns zero and the queued row is rolled back) or waits and then
        // sees/cancels the committed row in its own transaction.
        var rows = await dbContext.MetaCatalogSettings
            .Where(candidate => candidate.BusinessId == settings.BusinessId &&
                                candidate.UpdatedAt == settings.UpdatedAt &&
                                candidate.IsEnabled &&
                                candidate.CatalogId == settings.CatalogId &&
                                candidate.AccessTokenEncrypted != null &&
                                (force || candidate.SyncMode != "manual"))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.UpdatedAt, candidate => candidate.UpdatedAt), cancellationToken);
        return rows == 1;
    }

    private async Task<bool> TryCoalesceQueueItemAsync(
        CatalogSyncQueueItem queuedItem,
        CatalogProductSnapshot snapshot,
        string incomingEventType,
        string catalogId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var eventType = CoalesceEventType(queuedItem.EventType, incomingEventType);
        var payload = BuildPayload(snapshot with { EventType = eventType });

        if (dbContext.Database.IsRelational())
        {
            var rows = await dbContext.CatalogSyncQueue
                .Where(item => item.Id == queuedItem.Id && item.CatalogId == catalogId &&
                               (item.Status == "Pending" || item.Status == "Failed" ||
                                item.Status == "Simulated" || item.Status == "Paused" ||
                                item.Status == "Waiting"))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.EventType, eventType)
                    .SetProperty(item => item.CatalogId, catalogId)
                    .SetProperty(item => item.ProductRetailerId, snapshot.ProductRetailerId)
                    .SetProperty(item => item.PayloadJson, payload)
                    .SetProperty(item => item.Status, item => item.Status == "Waiting" ? "Waiting" : "Pending")
                    .SetProperty(item => item.RetryCount, 0)
                    .SetProperty(item => item.NextAttemptAt, item => item.Status == "Waiting" ? null : now)
                    .SetProperty(item => item.LastError, (string?)null)
                    .SetProperty(item => item.LeaseId, (Guid?)null)
                    .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.UpdatedAt, now), cancellationToken);
            return rows == 1;
        }

        var writableItem = await dbContext.CatalogSyncQueue.FirstOrDefaultAsync(
            item => item.Id == queuedItem.Id && item.CatalogId == catalogId &&
                    (item.Status == "Pending" || item.Status == "Failed" ||
                     item.Status == "Simulated" || item.Status == "Paused" ||
                     item.Status == "Waiting"),
            cancellationToken);
        if (writableItem is null)
        {
            return false;
        }

        writableItem.EventType = CoalesceEventType(writableItem.EventType, incomingEventType);
        writableItem.ProductRetailerId = snapshot.ProductRetailerId;
        writableItem.PayloadJson = BuildPayload(snapshot with { EventType = writableItem.EventType });
        if (writableItem.Status != "Waiting")
        {
            writableItem.Status = "Pending";
            writableItem.NextAttemptAt = now;
        }

        writableItem.RetryCount = 0;
        writableItem.LastError = null;
        writableItem.LeaseId = null;
        writableItem.LeaseExpiresAt = null;
        writableItem.UpdatedAt = now;
        return true;
    }

    public async Task<int> QueueAllProductsAsync(Guid businessId, CancellationToken cancellationToken = default)
    {
        var products = await dbContext.MenuItems
            .AsNoTracking()
            .Where(x => x.RestaurantId == businessId)
            .Select(x => new { x.Id, x.IsActive })
            .ToArrayAsync(cancellationToken);

        var queuedCount = 0;
        foreach (var product in products)
        {
            if (await QueueProductSyncAsync(
                    businessId,
                    product.Id,
                    product.IsActive ? "update" : "delete",
                    force: true,
                    cancellationToken: cancellationToken))
            {
                queuedCount++;
            }
        }

        return queuedCount;
    }

    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        // A queue event is fenced to the catalog it was created for. Clear any
        // stale event before it can block a replacement event for the same
        // product under the database's active-work uniqueness constraint.
        await CancelSupersededCatalogItemsAsync(cancellationToken);

        // Reconcile first so a current product snapshot coalesces/replaces any
        // older dry-run event before live replay is considered.
        await RecoverPendingReconciliationsAsync(cancellationToken);

        // A dry run intentionally leaves an auditable Simulated result. Once a
        // host is switched to live sending, make that staged work replayable so
        // operators do not have to recreate every catalog event by hand.
        if (!_options.DisableSending)
        {
            await RequeueSimulatedItemsAsync(cancellationToken);
        }

        var batchLimit = Math.Clamp(_options.BatchSize, 1, 100);
        for (var processed = 0; processed < batchLimit; processed++)
        {
            await ReleaseReadySuccessorsAsync(cancellationToken);
            var claimedItem = (await ClaimPendingItemsAsync(maxItems: 1, cancellationToken)).SingleOrDefault();
            if (claimedItem is null)
            {
                break;
            }

            await ProcessItemAsync(claimedItem.Item, claimedItem.LeaseId, cancellationToken);
        }
    }

    public async Task<MetaCatalogSettingsSummary?> GetSettingsAsync(Guid businessId, CancellationToken cancellationToken = default)
    {
        var setting = await dbContext.MetaCatalogSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == businessId, cancellationToken);

        return setting is null
            ? null
            : new MetaCatalogSettingsSummary(
                setting.BusinessId,
                setting.WabaId,
                setting.CatalogId,
                setting.PhoneNumberId,
                setting.IsEnabled,
                setting.SyncMode,
                setting.MetaBusinessId,
                setting.CredentialReference,
                setting.IsCartEnabled,
                setting.LastSuccessfulSyncAt);
    }

    public async Task SaveSettingsAsync(Guid businessId, MetaCatalogSettingsInput input, CancellationToken cancellationToken = default)
    {
        if (businessId == Guid.Empty)
        {
            throw new ArgumentException("Business is required.", nameof(businessId));
        }

        var restaurant = await dbContext.Restaurants
            .FirstOrDefaultAsync(x => x.Id == businessId, cancellationToken)
            ?? throw new KeyNotFoundException("Business was not found.");

        var setting = await dbContext.MetaCatalogSettings
            .FirstOrDefaultAsync(x => x.BusinessId == businessId, cancellationToken);

        var existingCatalogId = setting?.CatalogId;
        var wasActiveConnection = HasActiveConnection(setting);
        var wasManualSyncMode = string.Equals(setting?.SyncMode, "manual", StringComparison.OrdinalIgnoreCase);
        var catalogId = NormalizeOptional(input.CatalogId);
        var phoneNumberId = NormalizeOptional(input.PhoneNumberId);
        var suppliedAccessToken = NormalizeOptional(input.AccessToken);
        var accessToken = suppliedAccessToken ?? setting?.AccessTokenEncrypted;
        var syncMode = NormalizeSyncMode(input.SyncMode);

        if (input.IsEnabled && (catalogId is null || accessToken is null))
        {
            throw new InvalidOperationException(
                "A catalog ID and access token are required before catalog synchronization can be enabled.");
        }

        if (phoneNumberId is not null &&
            !string.Equals(phoneNumberId, restaurant.WhatsAppPhoneNumberId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Meta phone number ID must match the business WhatsApp Phone Number ID used for webhook routing.");
        }

        if (setting is null)
        {
            setting = new MetaCatalogSetting
            {
                BusinessId = businessId
            };
            dbContext.MetaCatalogSettings.Add(setting);
        }

        setting.WabaId = NormalizeOptional(input.WabaId);
        setting.MetaBusinessId = NormalizeOptional(input.MetaBusinessId);
        setting.CatalogId = catalogId;
        setting.PhoneNumberId = phoneNumberId;
        setting.CredentialReference = NormalizeOptional(input.CredentialReference);
        setting.AccessTokenEncrypted = accessToken;
        setting.WebhookVerifyTokenEncrypted = NormalizeOptional(input.WebhookVerifyToken)
            ?? setting.WebhookVerifyTokenEncrypted;
        setting.IsEnabled = input.IsEnabled;
        setting.IsCartEnabled = input.IsCartEnabled;
        setting.SyncMode = syncMode;
        setting.UpdatedAt = DateTimeOffset.UtcNow;

        // Existing customer ordering uses the restaurant field. Keep it in sync
        // with the single business-level Meta connection until that legacy field
        // can be retired.
        restaurant.WhatsAppCatalogId = catalogId;

        var catalogChanged = !string.Equals(existingCatalogId, catalogId, StringComparison.Ordinal);
        var hasActiveConnection = HasActiveConnection(setting);
        var switchedToManual = !wasManualSyncMode &&
            string.Equals(syncMode, "manual", StringComparison.OrdinalIgnoreCase);
        var requiresAutomaticReconciliation = hasActiveConnection &&
            !string.Equals(syncMode, "manual", StringComparison.OrdinalIgnoreCase) &&
            (catalogChanged || (!wasActiveConnection && hasActiveConnection) || wasManualSyncMode);
        if (requiresAutomaticReconciliation)
        {
            // This marker is persisted with the connection transition. It is
            // only cleared after paused snapshots are resumed and every product
            // specifically marked NotQueued has an outbox entry.
            setting.ReconciliationRequired = true;
        }
        else if (catalogChanged || string.Equals(syncMode, "manual", StringComparison.OrdinalIgnoreCase))
        {
            setting.ReconciliationRequired = false;
        }

        var reconciliationTransaction = dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            if (catalogChanged)
            {
                // Product node IDs are scoped to a Meta catalog. Do not expose a
                // new catalog to customers using stale IDs from the old one.
                var products = await dbContext.MenuItems
                    .Where(product => product.RestaurantId == businessId)
                    .ToArrayAsync(cancellationToken);
                foreach (var product in products)
                {
                    product.MetaProductId = null;
                    product.SyncStatus = "NotQueued";
                }

                // Persist the new connection before sweeping the old queue.
                // This locks the settings row first, so a concurrent enqueue
                // either confirms against the old row and becomes visible to
                // this sweep, or sees the new row and rolls itself back.
                await dbContext.SaveChangesAsync(cancellationToken);
                await CancelOutstandingQueueItemsAsync(businessId, cancellationToken);
            }
            else if (switchedToManual)
            {
                // Manual mode is an operator boundary. Letting a queued
                // automatic snapshot complete after a later manual edit could
                // falsely mark that newer local state as Synced, so fence the
                // old work and require an explicit manual sync instead.
                await dbContext.SaveChangesAsync(cancellationToken);
                await CancelOutstandingQueueItemsAsync(
                    businessId,
                    cancellationToken,
                    "Cancelled because catalog synchronization was switched to manual mode.",
                    markProductsNotQueued: true);
            }
            else if (!hasActiveConnection)
            {
                // A disabled connection is an intentional operator pause, not a
                // delivery failure. Preserve snapshots so a later re-enable can
                // resume deletes for products that no longer exist locally.
                await dbContext.SaveChangesAsync(cancellationToken);
                await PauseOutstandingQueueItemsAsync(businessId, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            if (reconciliationTransaction is not null)
            {
                await reconciliationTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (reconciliationTransaction is not null)
            {
                await reconciliationTransaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (reconciliationTransaction is not null)
            {
                await reconciliationTransaction.DisposeAsync();
            }
        }

        if (setting.ReconciliationRequired && CanQueue(setting, force: false))
        {
            await ReconcileRequiredConnectionAsync(businessId, cancellationToken);
        }
    }

    private async Task CancelOutstandingQueueItemsAsync(
        Guid businessId,
        CancellationToken cancellationToken,
        string reason = "Superseded because the Meta catalog connection changed.",
        bool markProductsNotQueued = false)
    {
        var now = DateTimeOffset.UtcNow;

        if (markProductsNotQueued)
        {
            var productIds = await dbContext.CatalogSyncQueue
                .AsNoTracking()
                .Where(item => item.BusinessId == businessId &&
                               (item.Status == "Pending" || item.Status == "Failed" ||
                                item.Status == "Processing" || item.Status == "Paused" ||
                                item.Status == "Simulated" || item.Status == "Waiting"))
                .Select(item => item.ProductId)
                .Distinct()
                .ToArrayAsync(cancellationToken);

            if (productIds.Length > 0)
            {
                if (dbContext.Database.IsRelational())
                {
                    await dbContext.MenuItems
                        .Where(product => product.RestaurantId == businessId && productIds.Contains(product.Id))
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(product => product.SyncStatus, "NotQueued"), cancellationToken);
                }
                else
                {
                    var products = await dbContext.MenuItems
                        .Where(product => product.RestaurantId == businessId && productIds.Contains(product.Id))
                        .ToArrayAsync(cancellationToken);
                    foreach (var product in products)
                    {
                        product.SyncStatus = "NotQueued";
                    }
                }
            }
        }

        if (dbContext.Database.IsRelational())
        {
            await dbContext.CatalogSyncQueue
                .Where(item => item.BusinessId == businessId &&
                               (item.Status == "Pending" || item.Status == "Failed" ||
                                item.Status == "Processing" || item.Status == "Paused" ||
                                item.Status == "Simulated" || item.Status == "Waiting"))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, "Cancelled")
                    .SetProperty(item => item.LastError, reason)
                    .SetProperty(item => item.NextAttemptAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.LeaseId, (Guid?)null)
                    .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.UpdatedAt, now), cancellationToken);
            return;
        }

        var queueItems = await dbContext.CatalogSyncQueue
            .Where(item => item.BusinessId == businessId &&
                           (item.Status == "Pending" || item.Status == "Failed" ||
                            item.Status == "Processing" || item.Status == "Paused" ||
                            item.Status == "Simulated" || item.Status == "Waiting"))
            .ToArrayAsync(cancellationToken);
        foreach (var queueItem in queueItems)
        {
            queueItem.Status = "Cancelled";
            queueItem.LastError = reason;
            queueItem.NextAttemptAt = null;
            queueItem.LeaseId = null;
            queueItem.LeaseExpiresAt = null;
            queueItem.UpdatedAt = now;
        }
    }

    private async Task PauseOutstandingQueueItemsAsync(Guid businessId, CancellationToken cancellationToken)
    {
        const string reason = "Paused because the Meta catalog connection is disabled or incomplete.";
        var now = DateTimeOffset.UtcNow;

        if (dbContext.Database.IsRelational())
        {
            await dbContext.CatalogSyncQueue
                .Where(item => item.BusinessId == businessId &&
                               (item.Status == "Pending" || item.Status == "Failed"))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, "Paused")
                    .SetProperty(item => item.LastError, reason)
                    .SetProperty(item => item.NextAttemptAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.LeaseId, (Guid?)null)
                    .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.UpdatedAt, now), cancellationToken);
            return;
        }

        var queueItems = await dbContext.CatalogSyncQueue
            .Where(item => item.BusinessId == businessId &&
                           (item.Status == "Pending" || item.Status == "Failed"))
            .ToArrayAsync(cancellationToken);
        foreach (var queueItem in queueItems)
        {
            queueItem.Status = "Paused";
            queueItem.LastError = reason;
            queueItem.NextAttemptAt = null;
            queueItem.LeaseId = null;
            queueItem.LeaseExpiresAt = null;
            queueItem.UpdatedAt = now;
        }
    }

    public async Task<IReadOnlyCollection<CatalogSyncQueueSummary>> GetQueueAsync(Guid businessId, CancellationToken cancellationToken = default)
    {
        return await dbContext.CatalogSyncQueue
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .GroupJoin(
                dbContext.MenuItems.AsNoTracking(),
                queue => queue.ProductId,
                product => product.Id,
                (queue, products) => new { queue, product = products.FirstOrDefault() })
            .Select(x => new CatalogSyncQueueSummary(
                x.queue.Id,
                x.queue.BusinessId,
                x.queue.ProductId,
                x.product == null ? "Deleted product" : x.product.Name,
                x.queue.EventType,
                x.queue.Status,
                x.queue.RetryCount,
                x.queue.NextAttemptAt,
                x.queue.LastError,
                x.queue.CreatedAt,
                x.queue.UpdatedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CatalogSyncLogSummary>> GetLogsAsync(Guid businessId, CancellationToken cancellationToken = default)
    {
        return await dbContext.CatalogSyncLogs
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .GroupJoin(
                dbContext.MenuItems.AsNoTracking(),
                log => log.ProductId,
                product => product.Id,
                (log, products) => new { log, product = products.FirstOrDefault() })
            .Select(x => new CatalogSyncLogSummary(
                x.log.Id,
                x.log.BusinessId,
                x.log.ProductId,
                x.product == null ? "Deleted product" : x.product.Name,
                x.log.EventType,
                x.log.Status,
                x.log.ResponseCode,
                x.log.ErrorMessage,
                x.log.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<bool> RetryQueueItemAsync(Guid businessId, Guid id, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (dbContext.Database.IsRelational())
        {
            var rows = await dbContext.CatalogSyncQueue
                .Where(item => item.Id == id && item.BusinessId == businessId && item.Status == "Failed")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, "Pending")
                    .SetProperty(item => item.LastError, (string?)null)
                    .SetProperty(item => item.RetryCount, 0)
                    .SetProperty(item => item.NextAttemptAt, now)
                    .SetProperty(item => item.LeaseId, (Guid?)null)
                    .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.UpdatedAt, now), cancellationToken);
            if (rows == 1)
            {
                dbContext.ChangeTracker.Clear();
                return true;
            }

            return false;
        }

        var item = await dbContext.CatalogSyncQueue
            .FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId, cancellationToken);

        if (item is null || item.Status != "Failed")
        {
            return false;
        }

        item.Status = "Pending";
        item.LastError = null;
        item.RetryCount = 0;
        item.NextAttemptAt = now;
        item.LeaseId = null;
        item.LeaseExpiresAt = null;
        item.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task RequeueSimulatedItemsAsync(CancellationToken cancellationToken)
    {
        var activeBusinessIds = await dbContext.MetaCatalogSettings
            .AsNoTracking()
            .Where(setting => setting.IsEnabled &&
                              setting.CatalogId != null &&
                              setting.AccessTokenEncrypted != null)
            .Select(setting => setting.BusinessId)
            .ToArrayAsync(cancellationToken);

        if (activeBusinessIds.Length == 0)
        {
            return;
        }

        var simulatedItems = await dbContext.CatalogSyncQueue
            .Where(item => item.Status == "Simulated" && activeBusinessIds.Contains(item.BusinessId))
            .ToArrayAsync(cancellationToken);

        await ResumeQueueItemsAsync(simulatedItems, cancellationToken);
    }

    private async Task CancelSupersededCatalogItemsAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.MetaCatalogSettings
            .AsNoTracking()
            .Where(setting => setting.CatalogId != null)
            .Select(setting => new { setting.BusinessId, setting.CatalogId })
            .ToArrayAsync(cancellationToken);

        foreach (var setting in settings)
        {
            const string reason = "Superseded because the Meta catalog connection changed.";
            var now = DateTimeOffset.UtcNow;

            if (dbContext.Database.IsRelational())
            {
                await dbContext.CatalogSyncQueue
                    .Where(item => item.BusinessId == setting.BusinessId &&
                                   (item.CatalogId == null || item.CatalogId != setting.CatalogId) &&
                                   (item.Status == "Pending" || item.Status == "Failed" ||
                                    item.Status == "Processing" || item.Status == "Paused" ||
                                    item.Status == "Simulated" || item.Status == "Waiting"))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, "Cancelled")
                        .SetProperty(item => item.LastError, reason)
                        .SetProperty(item => item.NextAttemptAt, (DateTimeOffset?)null)
                        .SetProperty(item => item.LeaseId, (Guid?)null)
                        .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(item => item.UpdatedAt, now), cancellationToken);
                continue;
            }

            var staleItems = await dbContext.CatalogSyncQueue
                .Where(item => item.BusinessId == setting.BusinessId &&
                               (item.CatalogId == null || item.CatalogId != setting.CatalogId) &&
                               (item.Status == "Pending" || item.Status == "Failed" ||
                                item.Status == "Processing" || item.Status == "Paused" ||
                                item.Status == "Simulated" || item.Status == "Waiting"))
                .ToArrayAsync(cancellationToken);
            foreach (var staleItem in staleItems)
            {
                CancelItem(staleItem, reason);
            }

            if (staleItems.Length > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        if (dbContext.Database.IsRelational())
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    private async Task RecoverPendingReconciliationsAsync(CancellationToken cancellationToken)
    {
        var businessIds = await dbContext.MetaCatalogSettings
            .AsNoTracking()
            .Where(setting => setting.ReconciliationRequired)
            .Select(setting => setting.BusinessId)
            .ToArrayAsync(cancellationToken);

        foreach (var businessId in businessIds)
        {
            await ReconcileRequiredConnectionAsync(businessId, cancellationToken);
        }
    }

    private async Task ReconcileRequiredConnectionAsync(Guid businessId, CancellationToken cancellationToken)
    {
        var setting = await dbContext.MetaCatalogSettings
            .FirstOrDefaultAsync(candidate => candidate.BusinessId == businessId, cancellationToken);
        if (setting is null || !setting.ReconciliationRequired || !CanQueue(setting, force: false))
        {
            return;
        }

        var catalogId = setting.CatalogId!;
        await ResumePausedItemsAsync(businessId, catalogId, cancellationToken);
        await QueueUnqueuedProductsAsync(businessId, cancellationToken);

        var hasUnqueuedProducts = await dbContext.MenuItems
            .AsNoTracking()
            .AnyAsync(product => product.RestaurantId == businessId &&
                                 product.SyncStatus == "NotQueued", cancellationToken);
        var hasPausedItems = await dbContext.CatalogSyncQueue
            .AsNoTracking()
            .AnyAsync(item => item.BusinessId == businessId &&
                              item.CatalogId == catalogId &&
                              item.Status == "Paused", cancellationToken);
        if (!hasUnqueuedProducts && !hasPausedItems)
        {
            var completedAt = DateTimeOffset.UtcNow;
            if (dbContext.Database.IsRelational())
            {
                // Do not clear a marker that a concurrent SaveSettings call
                // created for a different catalog transition while this worker
                // was finishing the older one.
                await dbContext.MetaCatalogSettings
                    .Where(candidate => candidate.BusinessId == businessId &&
                                        candidate.ReconciliationRequired &&
                                        candidate.IsEnabled &&
                                        candidate.CatalogId == catalogId &&
                                        candidate.AccessTokenEncrypted != null &&
                                        candidate.SyncMode != "manual")
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(candidate => candidate.ReconciliationRequired, false)
                        .SetProperty(candidate => candidate.UpdatedAt, completedAt), cancellationToken);
                dbContext.ChangeTracker.Clear();
                return;
            }

            await dbContext.Entry(setting).ReloadAsync(cancellationToken);
            if (setting.ReconciliationRequired && CanQueue(setting, force: false) &&
                string.Equals(setting.CatalogId, catalogId, StringComparison.Ordinal))
            {
                setting.ReconciliationRequired = false;
                setting.UpdatedAt = completedAt;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task QueueUnqueuedProductsAsync(Guid businessId, CancellationToken cancellationToken)
    {
        var products = await dbContext.MenuItems
            .AsNoTracking()
            .Where(product => product.RestaurantId == businessId && product.SyncStatus == "NotQueued")
            .Select(product => new { product.Id, product.IsActive })
            .ToArrayAsync(cancellationToken);

        foreach (var product in products)
        {
            await QueueProductSyncAsync(
                businessId,
                product.Id,
                product.IsActive ? "update" : "delete",
                cancellationToken: cancellationToken);
        }
    }

    private async Task ResumePausedItemsAsync(Guid businessId, string catalogId, CancellationToken cancellationToken)
    {
        var pausedItems = await dbContext.CatalogSyncQueue
            .Where(item => item.BusinessId == businessId &&
                           item.CatalogId == catalogId &&
                           item.Status == "Paused")
            .ToArrayAsync(cancellationToken);

        await ResumeQueueItemsAsync(pausedItems, cancellationToken);
    }

    private async Task ResumeQueueItemsAsync(
        IReadOnlyCollection<CatalogSyncQueueItem> queueItems,
        CancellationToken cancellationToken)
    {
        if (queueItems.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var queueItem in queueItems)
        {
            queueItem.Status = "Pending";
            queueItem.RetryCount = 0;
            queueItem.NextAttemptAt = now;
            queueItem.LastError = null;
            queueItem.LeaseId = null;
            queueItem.LeaseExpiresAt = null;
            queueItem.UpdatedAt = now;
        }

        var productIds = queueItems.Select(item => item.ProductId).Distinct().ToArray();
        var products = await dbContext.MenuItems
            .Where(product => productIds.Contains(product.Id))
            .ToArrayAsync(cancellationToken);
        foreach (var product in products)
        {
            product.SyncStatus = "Pending";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ReleaseReadySuccessorsAsync(CancellationToken cancellationToken)
    {
        var readySuccessors = await dbContext.CatalogSyncQueue
            .Where(item => item.Status == "Waiting" &&
                           !dbContext.CatalogSyncQueue.Any(predecessor =>
                               predecessor.Id == item.PredecessorQueueItemId &&
                               (predecessor.Status == "Pending" || predecessor.Status == "Failed" ||
                                predecessor.Status == "Processing" || predecessor.Status == "Paused" ||
                                predecessor.Status == "Simulated" || predecessor.Status == "Waiting")))
            .ToArrayAsync(cancellationToken);

        if (readySuccessors.Length == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var successor in readySuccessors)
        {
            successor.Status = "Pending";
            successor.NextAttemptAt = now;
            successor.LastError = null;
            successor.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<ClaimedQueueItem>> ClaimPendingItemsAsync(
        int maxItems,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var maxRetryCount = Math.Max(1, _options.MaxRetryCount);
        var candidateIds = await dbContext.CatalogSyncQueue
            .AsNoTracking()
            .Where(x =>
                (((x.Status == "Pending" || x.Status == "Failed") &&
                  x.RetryCount < maxRetryCount &&
                  (x.NextAttemptAt == null || x.NextAttemptAt <= now)) ||
                 (x.Status == "Processing" &&
                  x.RetryCount < maxRetryCount &&
                  x.LeaseExpiresAt != null &&
                  x.LeaseExpiresAt <= now)) &&
                !dbContext.CatalogSyncQueue.Any(predecessor =>
                    predecessor.Id == x.PredecessorQueueItemId &&
                    (predecessor.Status == "Pending" || predecessor.Status == "Failed" ||
                     predecessor.Status == "Processing" || predecessor.Status == "Paused" ||
                     predecessor.Status == "Simulated" || predecessor.Status == "Waiting")))
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .Take(Math.Clamp(maxItems, 1, 100))
            .ToArrayAsync(cancellationToken);

        var claims = new List<(Guid Id, Guid LeaseId)>();
        var configuredLeaseSeconds = Math.Clamp(_options.ProcessingLeaseSeconds, 30, 3600);
        var deliveryLeaseSeconds = (int)Math.Ceiling(httpClient.Timeout.TotalSeconds * 2) + 30;
        var leaseExpiresAt = now.AddSeconds(Math.Clamp(Math.Max(configuredLeaseSeconds, deliveryLeaseSeconds), 30, 3600));

        foreach (var id in candidateIds)
        {
            var leaseId = Guid.NewGuid();
            var claimed = false;

            if (dbContext.Database.IsRelational())
            {
                var rows = await dbContext.CatalogSyncQueue
                    .Where(x => x.Id == id &&
                        (((x.Status == "Pending" || x.Status == "Failed") &&
                          x.RetryCount < maxRetryCount &&
                          (x.NextAttemptAt == null || x.NextAttemptAt <= now)) ||
                         (x.Status == "Processing" &&
                          x.RetryCount < maxRetryCount &&
                          x.LeaseExpiresAt != null &&
                          x.LeaseExpiresAt <= now)) &&
                        !dbContext.CatalogSyncQueue.Any(predecessor =>
                            predecessor.Id == x.PredecessorQueueItemId &&
                            (predecessor.Status == "Pending" || predecessor.Status == "Failed" ||
                             predecessor.Status == "Processing" || predecessor.Status == "Paused" ||
                             predecessor.Status == "Simulated" || predecessor.Status == "Waiting")))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Status, "Processing")
                        .SetProperty(x => x.LeaseId, leaseId)
                        .SetProperty(x => x.LeaseExpiresAt, leaseExpiresAt)
                        .SetProperty(x => x.UpdatedAt, now), cancellationToken);
                claimed = rows == 1;
            }
            else
            {
                var item = await dbContext.CatalogSyncQueue.FirstOrDefaultAsync(
                    x => x.Id == id &&
                         (((x.Status == "Pending" || x.Status == "Failed") &&
                           x.RetryCount < maxRetryCount &&
                           (x.NextAttemptAt == null || x.NextAttemptAt <= now)) ||
                          (x.Status == "Processing" &&
                           x.RetryCount < maxRetryCount &&
                           x.LeaseExpiresAt != null &&
                           x.LeaseExpiresAt <= now)) &&
                         !dbContext.CatalogSyncQueue.Any(predecessor =>
                             predecessor.Id == x.PredecessorQueueItemId &&
                             (predecessor.Status == "Pending" || predecessor.Status == "Failed" ||
                              predecessor.Status == "Processing" || predecessor.Status == "Paused" ||
                              predecessor.Status == "Simulated" || predecessor.Status == "Waiting")),
                    cancellationToken);

                if (item is not null)
                {
                    item.Status = "Processing";
                    item.LeaseId = leaseId;
                    item.LeaseExpiresAt = leaseExpiresAt;
                    item.UpdatedAt = now;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    claimed = true;
                }
            }

            if (claimed)
            {
                claims.Add((id, leaseId));
            }
        }

        if (dbContext.Database.IsRelational())
        {
            dbContext.ChangeTracker.Clear();
        }

        var claimedItems = new List<ClaimedQueueItem>();
        foreach (var claim in claims)
        {
            var item = await dbContext.CatalogSyncQueue.FirstOrDefaultAsync(
                x => x.Id == claim.Id && x.LeaseId == claim.LeaseId,
                cancellationToken);
            if (item is not null)
            {
                claimedItems.Add(new ClaimedQueueItem(item, claim.LeaseId));
            }
        }

        return claimedItems;
    }

    private async Task ProcessItemAsync(CatalogSyncQueueItem item, Guid leaseId, CancellationToken cancellationToken)
    {
        if (item.LeaseId != leaseId ||
            !await OwnsLeaseAsync(item.Id, leaseId, cancellationToken))
        {
            return;
        }

        var product = await dbContext.MenuItems
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == item.ProductId && x.RestaurantId == item.BusinessId, cancellationToken);

        MetaCatalogSetting? deliverySetting = null;
        try
        {
            deliverySetting = await dbContext.MetaCatalogSettings
                .FirstOrDefaultAsync(x => x.BusinessId == item.BusinessId, cancellationToken);

            if (!string.Equals(item.CatalogId, deliverySetting?.CatalogId, StringComparison.Ordinal))
            {
                if (await OwnsLeaseAsync(item.Id, leaseId, cancellationToken))
                {
                    CancelItem(item, "Superseded because the Meta catalog connection changed.");
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                return;
            }

            if (!HasActiveConnection(deliverySetting))
            {
                if (await OwnsLeaseAsync(item.Id, leaseId, cancellationToken))
                {
                    PauseItem(item, product, "Paused because the Meta catalog connection is disabled or incomplete.");
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                return;
            }

            var activeSetting = deliverySetting!;

            if (_options.DisableSending)
            {
                if (!await PrepareTerminalWriteAsync(item, product, leaseId, activeSetting.CatalogId!, cancellationToken))
                {
                    return;
                }

                await CompleteAndPersistAsync(item, product, "Simulated", null, 0, cancellationToken);
                return;
            }

            // A local item can be deactivated before it has ever reached Meta.
            // In that case there is no remote node to delete, so the delete is an
            // idempotent no-op rather than a retryable failure.
            var queuedSnapshot = ReadPayload(item.PayloadJson);
            if (item.EventType == "delete" &&
                string.IsNullOrWhiteSpace(product?.MetaProductId) &&
                string.IsNullOrWhiteSpace(queuedSnapshot?.MetaProductId))
            {
                if (!await PrepareTerminalWriteAsync(item, product, leaseId, activeSetting.CatalogId!, cancellationToken))
                {
                    return;
                }

                activeSetting.LastSuccessfulSyncAt = DateTimeOffset.UtcNow;
                await CompleteAndPersistAsync(item, product, "Synced", null, 204, cancellationToken);
                return;
            }

            using var response = await SendToMetaAsync(item, product, activeSetting, cancellationToken);
            var responseBody = response.Content is null
                ? null
                : await response.Content.ReadAsStringAsync(cancellationToken);

            // DELETE is idempotent. A lost success response can cause the retry
            // to receive 404 because Meta has already removed the node.
            if (!response.IsSuccessStatusCode &&
                !(item.EventType == "delete" && (int)response.StatusCode == 404))
            {
                throw new MetaCatalogHttpException((int)response.StatusCode, responseBody);
            }

            if (!await PrepareTerminalWriteAsync(item, product, leaseId, activeSetting.CatalogId!, cancellationToken))
            {
                return;
            }

            activeSetting.LastSuccessfulSyncAt = DateTimeOffset.UtcNow;
            await CompleteAndPersistAsync(item, product, "Synced", responseBody, (int)response.StatusCode, cancellationToken);
            return;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another worker reclaimed or superseded this row after its lease
            // was checked. Its terminal state is authoritative; never write a
            // stale result over it.
            dbContext.ChangeTracker.Clear();
            logger.LogInformation("Catalog sync lease was superseded for product {ProductId}.", item.ProductId);
            return;
        }
        catch (MetaCatalogHttpException ex)
        {
            if (await PrepareTerminalWriteAsync(item, product, leaseId, deliverySetting?.CatalogId, cancellationToken))
            {
                FailItem(
                    item,
                    product,
                    ex.Message,
                    ex.ResponseCode,
                    ex.ResponseBody,
                    IsTransientStatusCode(ex.ResponseCode));
                logger.LogWarning("Meta catalog sync failed for product {ProductId}. StatusCode={StatusCode}", item.ProductId, ex.ResponseCode);
            }
        }
        catch (Exception ex)
        {
            if (await PrepareTerminalWriteAsync(item, product, leaseId, deliverySetting?.CatalogId, cancellationToken))
            {
                FailItem(item, product, ex.Message, 0, null, isTransient: true);
                logger.LogWarning(ex, "Meta catalog sync failed for product {ProductId}", item.ProductId);
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            logger.LogInformation("Catalog sync lease was superseded for product {ProductId}.", item.ProductId);
        }
    }

    private Task<bool> OwnsLeaseAsync(Guid queueItemId, Guid leaseId, CancellationToken cancellationToken) =>
        dbContext.CatalogSyncQueue
            .AsNoTracking()
            .AnyAsync(item => item.Id == queueItemId &&
                              item.Status == "Processing" &&
                              item.LeaseId == leaseId,
                cancellationToken);

    private async Task<bool> PrepareTerminalWriteAsync(
        CatalogSyncQueueItem item,
        MenuItem? product,
        Guid leaseId,
        string? expectedCatalogId,
        CancellationToken cancellationToken)
    {
        if (!await OwnsLeaseAsync(item.Id, leaseId, cancellationToken))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(expectedCatalogId))
        {
            return true;
        }

        var currentSetting = await dbContext.MetaCatalogSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == item.BusinessId, cancellationToken);
        if (!string.Equals(currentSetting?.CatalogId, expectedCatalogId, StringComparison.Ordinal))
        {
            CancelItem(item, "Superseded because the Meta catalog connection changed.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        if (!HasActiveConnection(currentSetting))
        {
            PauseItem(item, product, "Paused because the Meta catalog connection was disabled while delivery was in progress.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        return true;
    }

    private void CompleteItem(
        CatalogSyncQueueItem item,
        MenuItem? product,
        string status,
        string? responseBody,
        int responseCode)
    {
        item.Status = status;
        item.LastError = null;
        item.RetryCount = 0;
        item.NextAttemptAt = null;
        item.LeaseId = null;
        item.LeaseExpiresAt = null;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        if (product is not null)
        {
            if (status == "Synced")
            {
                product.MetaProductId = item.EventType == "delete"
                    ? null
                    : ExtractMetaProductId(responseBody) ?? product.MetaProductId;
            }
        }

        dbContext.CatalogSyncLogs.Add(new CatalogSyncLog
        {
            BusinessId = item.BusinessId,
            ProductId = item.ProductId,
            EventType = item.EventType,
            Status = status,
            ResponseCode = responseCode,
            ResponseBody = responseBody,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task CompleteAndPersistAsync(
        CatalogSyncQueueItem item,
        MenuItem? product,
        string status,
        string? responseBody,
        int responseCode,
        CancellationToken cancellationToken)
    {
        var completionTransaction = dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            CompleteItem(item, product, status, responseBody, responseCode);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (product is not null)
            {
                await SetTerminalProductSyncStatusIfCurrentAsync(item, product.Id, status, cancellationToken);
            }

            if (completionTransaction is not null)
            {
                await completionTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (completionTransaction is not null)
            {
                await completionTransaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (completionTransaction is not null)
            {
                await completionTransaction.DisposeAsync();
            }
        }
    }

    private async Task SetTerminalProductSyncStatusIfCurrentAsync(
        CatalogSyncQueueItem completedItem,
        Guid productId,
        string status,
        CancellationToken cancellationToken)
    {
        var outstandingStatuses = new[] { "Pending", "Failed", "Processing", "Paused", "Simulated", "Waiting" };
        if (dbContext.Database.IsRelational())
        {
            await dbContext.MenuItems
                .Where(product => product.Id == productId &&
                                  !dbContext.CatalogSyncQueue.Any(queueItem =>
                                      queueItem.ProductId == completedItem.ProductId &&
                                      queueItem.Id != completedItem.Id &&
                                      outstandingStatuses.Contains(queueItem.Status)))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(product => product.SyncStatus, status)
                    .SetProperty(
                        product => product.LastSyncedAt,
                        product => status == "Synced" ? DateTimeOffset.UtcNow : product.LastSyncedAt)
                    .SetProperty(product => product.LastSyncError, (string?)null)
                    .SetProperty(product => product.RetryCount, 0), cancellationToken);
            return;
        }

        var hasOutstandingSuccessor = await dbContext.CatalogSyncQueue
            .AsNoTracking()
            .AnyAsync(queueItem => queueItem.ProductId == completedItem.ProductId &&
                                  queueItem.Id != completedItem.Id &&
                                  outstandingStatuses.Contains(queueItem.Status),
                cancellationToken);
        if (hasOutstandingSuccessor)
        {
            return;
        }

        var product = await dbContext.MenuItems.FirstOrDefaultAsync(item => item.Id == productId, cancellationToken);
        if (product is not null)
        {
            product.SyncStatus = status;
            product.LastSyncedAt = status == "Synced" ? DateTimeOffset.UtcNow : product.LastSyncedAt;
            product.LastSyncError = null;
            product.RetryCount = 0;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private void FailItem(
        CatalogSyncQueueItem item,
        MenuItem? product,
        string errorMessage,
        int responseCode,
        string? responseBody,
        bool isTransient)
    {
        item.Status = "Failed";
        item.LastError = errorMessage;
        item.RetryCount = isTransient
            ? item.RetryCount + 1
            : Math.Max(Math.Max(1, _options.MaxRetryCount), item.RetryCount + 1);
        item.NextAttemptAt = isTransient
            ? DateTimeOffset.UtcNow.AddMinutes(Math.Pow(2, Math.Min(item.RetryCount, 5)))
            : null;
        item.LeaseId = null;
        item.LeaseExpiresAt = null;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        if (product is not null)
        {
            product.SyncStatus = "Failed";
            product.LastSyncError = errorMessage;
            product.RetryCount = item.RetryCount;
        }

        dbContext.CatalogSyncLogs.Add(new CatalogSyncLog
        {
            BusinessId = item.BusinessId,
            ProductId = item.ProductId,
            EventType = item.EventType,
            Status = "Failed",
            ErrorMessage = errorMessage,
            ResponseCode = responseCode,
            ResponseBody = responseBody,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static bool IsTransientStatusCode(int statusCode) =>
        statusCode is 408 or 429 || statusCode >= 500;

    private void CancelItem(CatalogSyncQueueItem item, string reason)
    {
        item.Status = "Cancelled";
        item.LastError = reason;
        item.NextAttemptAt = null;
        item.LeaseId = null;
        item.LeaseExpiresAt = null;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        dbContext.CatalogSyncLogs.Add(new CatalogSyncLog
        {
            BusinessId = item.BusinessId,
            ProductId = item.ProductId,
            EventType = item.EventType,
            Status = "Cancelled",
            ErrorMessage = reason,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private void PauseItem(CatalogSyncQueueItem item, MenuItem? product, string reason)
    {
        item.Status = "Paused";
        item.LastError = reason;
        item.NextAttemptAt = null;
        item.LeaseId = null;
        item.LeaseExpiresAt = null;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        if (product is not null)
        {
            product.SyncStatus = "Paused";
        }

        dbContext.CatalogSyncLogs.Add(new CatalogSyncLog
        {
            BusinessId = item.BusinessId,
            ProductId = item.ProductId,
            EventType = item.EventType,
            Status = "Paused",
            ErrorMessage = reason,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task<HttpResponseMessage> SendToMetaAsync(
        CatalogSyncQueueItem item,
        MenuItem? product,
        MetaCatalogSetting setting,
        CancellationToken cancellationToken)
    {
        var snapshot = ReadPayload(item.PayloadJson) ??
            (product is null ? null : CatalogProductSnapshot.From(product, item.EventType));
        if (item.EventType != "delete" && snapshot is null)
        {
            throw new InvalidOperationException("The catalog product snapshot is unavailable.");
        }

        var metaProductId = NormalizeOptional(product?.MetaProductId)
            ?? NormalizeOptional(snapshot?.MetaProductId);
        using var request = new HttpRequestMessage(
            item.EventType == "delete" ? HttpMethod.Delete : HttpMethod.Post,
            BuildMetaUri(setting.CatalogId!, metaProductId, item.EventType));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", setting.AccessTokenEncrypted);

        if (item.EventType != "delete")
        {
            request.Content = new FormUrlEncodedContent(BuildMetaForm(snapshot!));
        }

        return await httpClient.SendAsync(request, cancellationToken);
    }

    private Uri BuildMetaUri(string catalogId, string? metaProductId, string eventType)
    {
        var baseUrl = _options.GetVersionedGraphApiBaseUrl();
        var path = eventType == "delete"
            ? $"{baseUrl}/{Uri.EscapeDataString(metaProductId ?? throw new InvalidOperationException("The Meta product ID is required to delete a remote catalog product."))}"
            : $"{baseUrl}/{Uri.EscapeDataString(catalogId)}/products";

        return new Uri(path);
    }

    private Dictionary<string, string> BuildMetaForm(CatalogProductSnapshot product)
    {
        var imageUrl = RequirePublicHttpsImageUrl(product.ImageUrl);
        var form = new Dictionary<string, string>
        {
            ["retailer_id"] = product.ProductRetailerId,
            ["name"] = product.Name,
            ["description"] = NormalizeOptional(product.Description) ?? product.Name,
            ["availability"] = product.IsActive && product.IsAvailable ? "in stock" : "out of stock",
            ["condition"] = "new",
            ["price"] = ToMetaMinorUnits(product.Price),
            // The restaurant menu is authoritative. Older queued snapshots did
            // not have a currency field, so retain the configured fallback only
            // while those durable rows are drained after deployment.
            ["currency"] = NormalizeCurrency(product.Currency ?? _options.Currency),
            // Meta's catalog products edge supports retailer-ID upsert, letting
            // a durable update retry succeed even when a prior response was lost.
            ["allow_upsert"] = "true",
            ["image_url"] = imageUrl
        };

        if (!string.IsNullOrWhiteSpace(product.CategoryName))
        {
            form["product_type"] = product.CategoryName.Trim();
        }

        return form;
    }

    private static string RequirePublicHttpsImageUrl(string? imageUrl)
    {
        if (!Uri.TryCreate(NormalizeOptional(imageUrl), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            uri.IsLoopback)
        {
            throw new InvalidOperationException(
                "A publicly accessible HTTPS image URL is required before a product can synchronize with Meta.");
        }

        return uri.AbsoluteUri;
    }

    private static string ToMetaMinorUnits(decimal price)
    {
        if (price < 0)
        {
            throw new InvalidOperationException("A Meta catalog product price cannot be negative.");
        }

        return decimal.Round(price * 100m, 0, MidpointRounding.AwayFromZero)
            .ToString("0", CultureInfo.InvariantCulture);
    }

    private static string NormalizeCurrency(string? currency)
    {
        var normalized = NormalizeOptional(currency)?.ToUpperInvariant();
        if (normalized is null || normalized.Length != 3 || !normalized.All(char.IsAsciiLetter))
        {
            throw new InvalidOperationException("Meta catalog currency must be a three-letter ISO 4217 code.");
        }

        return normalized;
    }

    private static bool CanQueue(MetaCatalogSetting? settings, bool force) =>
        HasActiveConnection(settings) &&
        (force || !string.Equals(settings!.SyncMode, "manual", StringComparison.OrdinalIgnoreCase));

    private static bool HasActiveConnection(MetaCatalogSetting? settings) =>
        settings is { IsEnabled: true } &&
        !string.IsNullOrWhiteSpace(settings.CatalogId) &&
        !string.IsNullOrWhiteSpace(settings.AccessTokenEncrypted);

    private static string CoalesceEventType(string existingEventType, string incomingEventType)
    {
        if (incomingEventType == "delete")
        {
            return "delete";
        }

        return string.Equals(existingEventType, "create", StringComparison.OrdinalIgnoreCase)
            ? "create"
            : incomingEventType;
    }

    private static string BuildPayload(CatalogProductSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonOptions);

    private static CatalogProductSnapshot? ReadPayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CatalogProductSnapshot>(payloadJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractMetaProductId(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return TryReadString(document.RootElement, "id")
                ?? TryReadString(document.RootElement, "product_id")
                ?? TryReadString(document.RootElement, "retailer_id");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    private static string NormalizeEventType(string? eventType) =>
        eventType?.Trim().ToLowerInvariant() switch
        {
            "create" => "create",
            "update" => "update",
            "delete" => "delete",
            _ => "update"
        };

    private static string NormalizeSyncMode(string? syncMode) =>
        syncMode?.Trim().ToLowerInvariant() switch
        {
            null or "" or "default" => "default",
            "manual" => "manual",
            "automatic" => "automatic",
            _ => throw new ArgumentException("Sync mode must be Default, Manual, or Automatic.", nameof(syncMode))
        };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CreateGeneratedRetailerId(Guid businessId, Guid productId) =>
        $"vxc-{businessId:N}-{productId:N}";

    private sealed record ClaimedQueueItem(CatalogSyncQueueItem Item, Guid LeaseId);

    private sealed record CatalogProductSnapshot(
        Guid BusinessId,
        Guid ProductId,
        string ProductRetailerId,
        string? MetaProductId,
        string Name,
        string? Description,
        decimal Price,
        string? Currency,
        string? ImageUrl,
        string? CategoryName,
        bool IsActive,
        bool IsAvailable,
        string EventType,
        DateTimeOffset CapturedAt)
    {
        public static CatalogProductSnapshot From(MenuItem product, string eventType) =>
            new(
                product.RestaurantId,
                product.Id,
                product.ProductRetailerId ?? CreateGeneratedRetailerId(product.RestaurantId, product.Id),
                product.MetaProductId,
                product.Name,
                product.Description,
                product.DiscountPrice ?? product.Price,
                product.Currency,
                product.ImageUrl,
                product.Category?.Name,
                product.IsActive && (product.Category?.IsActive ?? true),
                product.IsAvailable,
                eventType,
                DateTimeOffset.UtcNow);
    }

    private sealed class MetaCatalogHttpException(int responseCode, string? responseBody)
        : Exception($"Meta catalog request failed with HTTP {responseCode}.")
    {
        public int ResponseCode { get; } = responseCode;
        public string? ResponseBody { get; } = responseBody;
    }
}
