using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IConversationService
{
    Task<WhatsAppWebhookProcessResult> ProcessIncomingMessageAsync(
        IncomingWhatsAppMessage message,
        CancellationToken cancellationToken = default);
}
