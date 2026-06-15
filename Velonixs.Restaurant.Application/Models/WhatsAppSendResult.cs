namespace Velonixs.Restaurant.Application.Models;

public sealed record WhatsAppSendResult(
    bool IsSuccess,
    bool IsSkipped,
    string? ProviderMessageId = null,
    string? Error = null);
