namespace Velonixs.Connect.Application.Models;

public sealed record CatalogMessageSendResult(
    bool IsSuccess,
    bool IsSkipped,
    Guid RestaurantId,
    string? ProviderMessageId = null,
    string? Error = null);

public sealed record WhatsAppCommerceSettingsResult(
    Guid RestaurantId,
    bool IsCatalogVisible,
    bool IsCartEnabled,
    bool IsRemoteValidated,
    string? Diagnostic = null);

public sealed record WhatsAppCommerceDiagnostics(
    Guid RestaurantId,
    bool IsConfigured,
    bool IsPhoneNumberMatched,
    bool IsCatalogVisible,
    bool IsCartEnabled,
    bool IsReady,
    IReadOnlyCollection<string> Issues);

public sealed record MetaCatalogOverview(
    Guid RestaurantId,
    string RestaurantName,
    string? CatalogId,
    bool IsEnabled,
    bool IsCartEnabled,
    string SyncMode,
    int ProductCount,
    int SyncedProductCount,
    DateTimeOffset? LastSuccessfulSyncAt);

public sealed record MetaCatalogSyncStatusResponse(
    Guid RestaurantId,
    int Pending,
    int Processing,
    int Synced,
    int Failed,
    DateTimeOffset? LastSuccessfulSyncAt);
