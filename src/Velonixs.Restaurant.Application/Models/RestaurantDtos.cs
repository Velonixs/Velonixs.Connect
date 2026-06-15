namespace Velonixs.Restaurant.Application.Models;

public sealed record RestaurantResponse(
    Guid Id,
    string Name,
    string WhatsAppPhoneNumberId,
    string? BusinessPhone,
    string? NotificationEmail,
    string? StaffWhatsAppNumber,
    string? Address,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateRestaurantRequest(
    string Name,
    string WhatsAppPhoneNumberId,
    string? BusinessPhone,
    string? NotificationEmail,
    string? StaffWhatsAppNumber,
    string? Address,
    bool IsActive = true);

public sealed record UpdateRestaurantRequest(
    string Name,
    string WhatsAppPhoneNumberId,
    string? BusinessPhone,
    string? NotificationEmail,
    string? StaffWhatsAppNumber,
    string? Address,
    bool IsActive);
