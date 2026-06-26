using System.ComponentModel.DataAnnotations;

namespace Velonixs.Connect.Application.Models;

public sealed record BusinessResponse(
    Guid Id,
    string Name,
    string BusinessType,
    string WhatsAppPhoneNumberId,
    string? BusinessPhone,
    string? NotificationEmail,
    string? StaffWhatsAppNumber,
    string? Address,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateBusinessRequest(
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
    bool IsActive = true);

public sealed record UpdateBusinessRequest(
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
    bool IsActive);
