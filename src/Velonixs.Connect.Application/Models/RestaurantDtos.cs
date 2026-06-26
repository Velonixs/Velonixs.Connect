using System.ComponentModel.DataAnnotations;

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
    [Required]
    string Name,
    string? BusinessType,

    [Required]
    string WhatsAppPhoneNumberId,
    string? BusinessPhone,

    [EmailAddress]
    string? NotificationEmail,
    string? StaffWhatsAppNumber,
    string? Address,

    [Range(typeof(decimal), "0", "100")]
    decimal CgstPercent = 0,

    [Range(typeof(decimal), "0", "100")]
    decimal SgstPercent = 0,
    bool IsActive = true);

public sealed record UpdateRestaurantRequest(
    [Required]
    string Name,
    string? BusinessType,

    [Required]
    string WhatsAppPhoneNumberId,
    string? BusinessPhone,

    [EmailAddress]
    string? NotificationEmail,
    string? StaffWhatsAppNumber,
    string? Address,

    [Range(typeof(decimal), "0", "100")]
    decimal CgstPercent,

    [Range(typeof(decimal), "0", "100")]
    decimal SgstPercent,
    bool IsActive);

public sealed record PlatformTaxSettingResponse(
    decimal CgstPercent,
    decimal SgstPercent,
    DateTimeOffset UpdatedAtUtc);
