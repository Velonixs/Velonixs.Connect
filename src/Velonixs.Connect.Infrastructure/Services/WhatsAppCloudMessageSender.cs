using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Infrastructure.Configuration;

namespace Velonixs.Connect.Infrastructure.Services;

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
        var normalizedRecipientPhoneNumber = NormalizeRecipientPhoneNumber(recipientPhoneNumber);
        if (string.IsNullOrWhiteSpace(normalizedRecipientPhoneNumber))
        {
            logger.LogWarning("WhatsApp send failed. Recipient phone number is empty.");
            return new WhatsAppSendResult(false, false, Error: "Recipient phone number is empty.");
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = normalizedRecipientPhoneNumber,
            type = "text",
            text = new
            {
                preview_url = false,
                body = message
            }
        };

        return await SendPayloadAsync(phoneNumberId, normalizedRecipientPhoneNumber, payload, message, cancellationToken);
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
        var normalizedRecipientPhoneNumber = NormalizeRecipientPhoneNumber(recipientPhoneNumber);
        if (string.IsNullOrWhiteSpace(normalizedRecipientPhoneNumber))
        {
            logger.LogWarning("WhatsApp send failed. Recipient phone number is empty.");
            return new WhatsAppSendResult(false, false, Error: "Recipient phone number is empty.");
        }

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
            to = normalizedRecipientPhoneNumber,
            type = "interactive",
            interactive
        };

        return await SendPayloadAsync(phoneNumberId, normalizedRecipientPhoneNumber, payload, bodyText, cancellationToken);
    }

    public async Task<WhatsAppSendResult> SendReplyButtonMessageAsync(
        string phoneNumberId,
        string recipientPhoneNumber,
        string bodyText,
        IReadOnlyCollection<WhatsAppReplyButton> buttons,
        string? footerText = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedRecipientPhoneNumber = NormalizeRecipientPhoneNumber(recipientPhoneNumber);
        if (string.IsNullOrWhiteSpace(normalizedRecipientPhoneNumber))
        {
            logger.LogWarning("WhatsApp send failed. Recipient phone number is empty.");
            return new WhatsAppSendResult(false, false, Error: "Recipient phone number is empty.");
        }

        var interactive = new Dictionary<string, object?>
        {
            ["type"] = "button",
            ["body"] = new { text = bodyText },
            ["action"] = new
            {
                buttons = buttons.Take(3).Select(button => new
                {
                    type = "reply",
                    reply = new
                    {
                        id = button.Id,
                        title = button.Title
                    }
                }).ToArray()
            }
        };

        if (!string.IsNullOrWhiteSpace(footerText))
        {
            interactive["footer"] = new { text = footerText };
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = normalizedRecipientPhoneNumber,
            type = "interactive",
            interactive
        };

        return await SendPayloadAsync(phoneNumberId, normalizedRecipientPhoneNumber, payload, bodyText, cancellationToken);
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
                "WhatsApp sending skipped because sending is disabled or credentials are missing.");

            return new WhatsAppSendResult(true, true);
        }

        var requestUri = $"{_options.BaseUrl.TrimEnd('/')}/{_options.ApiVersion}/{phoneNumberId}/messages";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        request.Content = JsonContent.Create(payload);

        var stopwatch = Stopwatch.StartNew();
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "WhatsApp send failed in {ElapsedMilliseconds} ms. PhoneNumberId={PhoneNumberId}, Recipient={Recipient}, StatusCode={StatusCode}, Response={Response}",
                stopwatch.ElapsedMilliseconds,
                phoneNumberId,
                recipientPhoneNumber,
                response.StatusCode,
                responseText);

            return new WhatsAppSendResult(false, false, Error: responseText);
        }

        logger.LogInformation(
            "WhatsApp send completed in {ElapsedMilliseconds} ms. PhoneNumberId={PhoneNumberId}, Recipient={Recipient}",
            stopwatch.ElapsedMilliseconds,
            phoneNumberId,
            recipientPhoneNumber);

        return new WhatsAppSendResult(true, false, ProviderMessageId: ExtractProviderMessageId(responseText));
    }

    private static string NormalizeRecipientPhoneNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        if (digits.Length == 11 && digits.StartsWith('0'))
        {
            digits = digits[1..];
        }

        return digits.Length == 10 ? $"91{digits}" : digits;
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
