namespace Velonixs.Connect.Application.Models;

public sealed record WhatsAppWebhookProcessResult(
    bool IsProcessed,
    string? ReplyText,
    Guid? RestaurantId,
    Guid? CustomerId,
    Guid? ConversationId,
    string? ConversationState,
    string? Error = null)
{
    public static WhatsAppWebhookProcessResult Ignored(string reason) =>
        new(false, null, null, null, null, null, reason);
}
