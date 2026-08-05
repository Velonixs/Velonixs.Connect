namespace Velonixs.Connect.Domain.Entities;

public sealed class CatalogSyncQueueItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public Guid ProductId { get; set; }
    /// <summary>
    /// Captures the catalog connection that produced this event. Delivery is
    /// cancelled if the business is switched to another catalog before work is
    /// sent, preventing stale outbox entries from crossing catalog boundaries.
    /// </summary>
    public string? CatalogId { get; set; }
    public string ProductRetailerId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public string Status { get; set; } = "Pending";
    public int RetryCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    /// <summary>
    /// Links a newer event to work that was already being delivered for the
    /// same product. A successor cannot be claimed until its predecessor has
    /// reached a terminal state, preserving remote create/update/delete order.
    /// </summary>
    public Guid? PredecessorQueueItemId { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    /// <summary>
    /// SQL Server row version used to prevent a stale worker from completing
    /// work that another worker reclaimed after its lease elapsed.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
