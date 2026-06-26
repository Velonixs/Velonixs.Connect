namespace Velonixs.Connect.Application.Models;

public sealed record NotificationLogResponse(
    Guid Id,
    Guid RestaurantId,
    Guid? CustomerId,
    string Direction,
    string MessageText,
    string? WhatsAppMessageId,
    string Status,
    DateTimeOffset CreatedAt);
