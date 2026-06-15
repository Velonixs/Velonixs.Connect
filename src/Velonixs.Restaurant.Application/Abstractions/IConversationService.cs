using Velonixs.Restaurant.Application.Models;

namespace Velonixs.Restaurant.Application.Abstractions;

public interface IConversationService
{
    Task<WhatsAppWebhookProcessResult> ProcessIncomingMessageAsync(
        IncomingWhatsAppMessage message,
        CancellationToken cancellationToken = default);
}
