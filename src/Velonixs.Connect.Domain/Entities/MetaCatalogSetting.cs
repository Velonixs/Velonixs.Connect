namespace Velonixs.Connect.Domain.Entities;

public sealed class MetaCatalogSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public string? MetaBusinessId { get; set; }
    public string? WabaId { get; set; }
    public string? CatalogId { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? CredentialReference { get; set; }
    public string? AccessTokenEncrypted { get; set; }
    public string? WebhookVerifyTokenEncrypted { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsCartEnabled { get; set; } = true;
    public string SyncMode { get; set; } = "default";
    // Set in the same transaction as a connection transition so the worker
    // can safely finish reconciliation after a host interruption.
    public bool ReconciliationRequired { get; set; }
    public DateTimeOffset? LastSuccessfulSyncAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
