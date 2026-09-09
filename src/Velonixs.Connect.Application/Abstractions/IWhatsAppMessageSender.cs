using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

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

    Task<WhatsAppSendResult> SendReplyButtonMessageAsync(
        string phoneNumberId,
        string recipientPhoneNumber,
        string bodyText,
        IReadOnlyCollection<WhatsAppReplyButton> buttons,
        string? footerText = null,
        CancellationToken cancellationToken = default);

    Task<WhatsAppSendResult> SendCatalogMessageAsync(
        string phoneNumberId,
        string recipientPhoneNumber,
        string bodyText,
        string? thumbnailProductRetailerId = null,
        string? footerText = null,
        CancellationToken cancellationToken = default) =>
        SendTextMessageAsync(phoneNumberId, recipientPhoneNumber, bodyText, cancellationToken);

    Task<WhatsAppSendResult> SendMultiProductMessageAsync(
        string phoneNumberId,
        string recipientPhoneNumber,
        string catalogId,
        string headerText,
        string bodyText,
        IReadOnlyCollection<WhatsAppProductListSection> sections,
        string? footerText = null,
        CancellationToken cancellationToken = default);
}
