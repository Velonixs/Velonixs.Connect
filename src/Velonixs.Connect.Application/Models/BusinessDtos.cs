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
    string Name,
    string? BusinessType,
    string WhatsAppPhoneNumberId,
    string? BusinessPhone,
    string? NotificationEmail,
    string? StaffWhatsAppNumber,
    string? Address,
    bool IsActive = true);

public sealed record UpdateBusinessRequest(
    string Name,
    string? BusinessType,
    string WhatsAppPhoneNumberId,
    string? BusinessPhone,
    string? NotificationEmail,
    string? StaffWhatsAppNumber,
    string? Address,
    bool IsActive);
