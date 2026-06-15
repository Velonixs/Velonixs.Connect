using Velonixs.Restaurant.Application.Models;

namespace Velonixs.Restaurant.Application.Abstractions;

public interface IWhatsAppMessageSender
{
    Task<WhatsAppSendResult> SendTextMessageAsync(
        string phoneNumberId,
        string recipientPhoneNumber,
        string message,
        CancellationToken cancellationToken = default);

    Task<WhatsAppSendResult> SendInteractiveListMessageAsync(
        string phoneNumberId,
        string recipientPhoneNumber,
        string bodyText,
        string buttonText,
        IReadOnlyCollection<WhatsAppInteractiveListSection> sections,
        string? footerText = null,
        CancellationToken cancellationToken = default);
}
