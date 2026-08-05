namespace Velonixs.Connect.Application.Abstractions;

public interface IMetaCatalogSyncService
{
    /// <summary>
    /// Adds or coalesces an item in the business catalog outbox. A manual sync can
    /// override a connection configured in manual mode.
    /// </summary>
    Task<bool> QueueProductSyncAsync(
        Guid businessId,
        Guid productId,
        string eventType,
        bool force = false,
        CancellationToken cancellationToken = default);
    Task<int> QueueAllProductsAsync(Guid businessId, CancellationToken cancellationToken = default);
    Task ProcessPendingAsync(CancellationToken cancellationToken = default);
    Task<MetaCatalogSettingsSummary?> GetSettingsAsync(Guid businessId, CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(Guid businessId, MetaCatalogSettingsInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CatalogSyncQueueSummary>> GetQueueAsync(Guid businessId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CatalogSyncLogSummary>> GetLogsAsync(Guid businessId, CancellationToken cancellationToken = default);
    Task<bool> RetryQueueItemAsync(Guid businessId, Guid id, CancellationToken cancellationToken = default);
}

public sealed record MetaCatalogSettingsInput(
    string? WabaId,
    string? CatalogId,
    string? PhoneNumberId,
    string? AccessToken,
    string? WebhookVerifyToken,
    bool IsEnabled = true,
    string SyncMode = "default");

public sealed record MetaCatalogSettingsSummary(
    Guid BusinessId,
    string? WabaId,
    string? CatalogId,
    string? PhoneNumberId,
    bool IsEnabled,
    string SyncMode);

public sealed record CatalogSyncQueueSummary(
    Guid Id,
    Guid BusinessId,
    Guid ProductId,
    string ProductName,
    string EventType,
    string Status,
    int RetryCount,
    DateTimeOffset? NextAttemptAt,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CatalogSyncLogSummary(
    Guid Id,
    Guid BusinessId,
    Guid ProductId,
    string ProductName,
    string EventType,
    string Status,
    int ResponseCode,
    string? ErrorMessage,
    DateTimeOffset CreatedAt);
