namespace Velonixs.Connect.Application.Models;

public sealed record IncomingWhatsAppMessage(
    string PhoneNumberId,
    string FromPhoneNumber,
    string MessageText,
    string? WhatsAppMessageId = null,
    string? ProfileName = null,
    DateTimeOffset? ReceivedAt = null,
    IReadOnlyCollection<IncomingWhatsAppOrderItem>? OrderItems = null);

public sealed record IncomingWhatsAppOrderItem(
    string ProductRetailerId,
    int Quantity);
