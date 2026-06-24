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
    DateTimeOffset CreatedAt);

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
    bool IsActive = true);

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
    bool IsActive);

public sealed record PlatformTaxSettingResponse(
    decimal CgstPercent,
    decimal SgstPercent,
    DateTimeOffset UpdatedAtUtc);
