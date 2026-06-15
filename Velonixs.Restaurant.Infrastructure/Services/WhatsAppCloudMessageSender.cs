using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velonixs.Restaurant.Application.Abstractions;
using Velonixs.Restaurant.Application.Models;
using Velonixs.Restaurant.Infrastructure.Configuration;

namespace Velonixs.Restaurant.Infrastructure.Services;

public sealed class WhatsAppCloudMessageSender(
    HttpClient httpClient,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppCloudMessageSender> logger) : IWhatsAppMessageSender
{
    private readonly WhatsAppOptions _options = options.Value;

    public async Task<WhatsAppSendResult> SendTextMessageAsync(
        string phoneNumberId,
        string recipientPhoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = recipientPhoneNumber,
            type = "text",
            text = new
            {
                preview_url = false,
                body = message
            }
        };

        return await SendPayloadAsync(phoneNumberId, recipientPhoneNumber, payload, message, cancellationToken);
    }

    public async Task<WhatsAppSendResult> SendInteractiveListMessageAsync(
        string phoneNumberId,
        string recipientPhoneNumber,
        string bodyText,
        string buttonText,
        IReadOnlyCollection<WhatsAppInteractiveListSection> sections,
        string? footerText = null,
        CancellationToken cancellationToken = default)
    {
        var interactive = new Dictionary<string, object?>
        {
            ["type"] = "list",
            ["body"] = new
            {
                text = bodyText
            },
            ["action"] = new
            {
                button = buttonText,
                sections = sections.Select(section => new
                {
                    title = section.Title,
                    rows = section.Rows.Select(row => new
                    {
                        id = row.Id,
                        title = row.Title,
                        description = row.Description
                    }).ToArray()
                }).ToArray()
            }
        };

        if (!string.IsNullOrWhiteSpace(footerText))
        {
            interactive["footer"] = new
            {
                text = footerText
            };
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = recipientPhoneNumber,
            type = "interactive",
            interactive
        };

        return await SendPayloadAsync(phoneNumberId, recipientPhoneNumber, payload, bodyText, cancellationToken);
    }

    private async Task<WhatsAppSendResult> SendPayloadAsync(
        string phoneNumberId,
        string recipientPhoneNumber,
        object payload,
        string logMessage,
        CancellationToken cancellationToken)
    {
        if (_options.DisableSending || string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            logger.LogInformation(
                "WhatsApp sending skipped. PhoneNumberId={PhoneNumberId}, To={To}, Message={Message}",
                phoneNumberId,
                recipientPhoneNumber,
                logMessage);

            return new WhatsAppSendResult(true, true);
        }

        var requestUri = $"{_options.BaseUrl.TrimEnd('/')}/{_options.ApiVersion}/{phoneNumberId}/messages";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        request.Content = JsonContent.Create(payload);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "WhatsApp send failed. StatusCode={StatusCode}, Response={Response}",
                response.StatusCode,
                responseText);

            return new WhatsAppSendResult(false, false, Error: responseText);
        }

        return new WhatsAppSendResult(true, false, ProviderMessageId: ExtractProviderMessageId(responseText));
    }

    private static string? ExtractProviderMessageId(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);

            if (document.RootElement.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.String)
            {
                return id.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
