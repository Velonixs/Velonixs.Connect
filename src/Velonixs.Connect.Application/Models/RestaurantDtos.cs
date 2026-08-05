namespace Velonixs.Connect.Application.Models;

public sealed record RestaurantResponse(
    Guid Id,
    string Name,
    string BusinessType,
    string WhatsAppPhoneNumberId,
    string? BusinessPhone,
    string? NotificationEmail,
    string? StaffWhatsAppNumber,
    string? Address,
    decimal CgstPercent,
    decimal SgstPercent,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? WhatsAppCatalogId = null);

/// <summary>
/// Creates a business. <paramref name="WhatsAppCatalogId"/> remains in this
/// contract for backwards compatibility, but is intentionally ignored: Meta
/// catalog changes must go through <c>IMetaCatalogSyncService.SaveSettingsAsync</c>
/// so catalog product state is reconciled.
/// </summary>
public sealed record CreateRestaurantRequest(
    string Name,
    string? BusinessType,
    string WhatsAppPhoneNumberId,
    string? BusinessPhone,
    string? NotificationEmail,
    string? StaffWhatsAppNumber,
    string? Address,
    decimal CgstPercent = 0,
    decimal SgstPercent = 0,
    bool IsActive = true,
    string? WhatsAppCatalogId = null);

/// <summary>
/// Updates a business profile. <paramref name="WhatsAppCatalogId"/> is a
/// read-only legacy mirror and is ignored on update; use Meta catalog settings
/// to make a reconciled catalog change.
/// </summary>
public sealed record UpdateRestaurantRequest(
    string Name,
    string? BusinessType,
    string WhatsAppPhoneNumberId,
    string? BusinessPhone,
    string? NotificationEmail,
    string? StaffWhatsAppNumber,
    string? Address,
    decimal CgstPercent,
    decimal SgstPercent,
    bool IsActive,
    string? WhatsAppCatalogId = null);

public sealed record PlatformTaxSettingResponse(
    decimal CgstPercent,
    decimal SgstPercent,
    DateTimeOffset UpdatedAtUtc);
