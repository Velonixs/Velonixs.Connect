using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Persistence.Configuration;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/webhooks/whatsapp")]
public sealed class WhatsAppWebhookController(
    IConversationService conversationService,
    IOptions<WhatsAppOptions> whatsAppOptions,
    IOptions<RestaurantConnectOptions> restaurantConnectOptions,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    private readonly WhatsAppOptions _whatsAppOptions = whatsAppOptions.Value;
    private readonly RestaurantConnectOptions _restaurantConnectOptions = restaurantConnectOptions.Value;

    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode == "subscribe" &&
            !string.IsNullOrWhiteSpace(challenge) &&
            verifyToken == _whatsAppOptions.VerifyToken)
        {
            return Content(challenge, "text/plain");
        }

        return Unauthorized();
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (!IsSignatureValid(rawBody))
        {
            return Unauthorized();
        }

        using var jsonDocument = JsonDocument.Parse(rawBody);
        var payload = jsonDocument.RootElement;
        var message = TryReadIncomingMessage(payload);

        if (message is null)
        {
            logger.LogInformation("WhatsApp webhook received no supported text message.");
            return Ok(new { processed = false, reason = "No supported text message found." });
        }

        var result = await conversationService.ProcessIncomingMessageAsync(message, cancellationToken);

        return Ok(result);
    }

    [HttpPost("local-test")]
    public async Task<IActionResult> LocalTest(
        [FromBody] LocalWhatsAppMessageRequest request,
        CancellationToken cancellationToken)
    {
        var message = new IncomingWhatsAppMessage(
            string.IsNullOrWhiteSpace(request.PhoneNumberId) ? _restaurantConnectOptions.DemoWhatsAppPhoneNumberId : request.PhoneNumberId,
            string.IsNullOrWhiteSpace(request.FromPhoneNumber) ? "+919876543210" : request.FromPhoneNumber,
            request.MessageText,
            request.WhatsAppMessageId ?? Guid.NewGuid().ToString("N"),
            request.ProfileName,
            DateTimeOffset.UtcNow);

        var result = await conversationService.ProcessIncomingMessageAsync(message, cancellationToken);

        return Ok(result);
    }

    private static IncomingWhatsAppMessage? TryReadIncomingMessage(JsonElement payload)
    {
        if (!payload.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value))
                {
                    continue;
                }

                var phoneNumberId = ReadString(value, "metadata", "phone_number_id");
                var profileName = ReadFirstContactName(value);

                if (!value.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var whatsAppMessage in messages.EnumerateArray())
                {
                    var from = ReadString(whatsAppMessage, "from");
                    var id = ReadString(whatsAppMessage, "id");
                    var text = ReadString(whatsAppMessage, "text", "body")
                        ?? ReadInteractiveReplyAsText(whatsAppMessage);

                    if (string.IsNullOrWhiteSpace(phoneNumberId) ||
                        string.IsNullOrWhiteSpace(from) ||
                        string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    return new IncomingWhatsAppMessage(phoneNumberId, from, text, id, profileName, DateTimeOffset.UtcNow);
                }
            }
        }

        return null;
    }

    private static string? ReadFirstContactName(JsonElement value)
    {
        if (!value.TryGetProperty("contacts", out var contacts) ||
            contacts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var contact = contacts.EnumerateArray().FirstOrDefault();

        if (contact.ValueKind is JsonValueKind.Undefined)
        {
            return null;
        }

        return ReadString(contact, "profile", "name");
    }

    private static string? ReadString(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static string? ReadInteractiveReplyAsText(JsonElement whatsAppMessage)
    {
        var listReplyId = ReadString(whatsAppMessage, "interactive", "list_reply", "id");
        var buttonReplyId = ReadString(whatsAppMessage, "interactive", "button_reply", "id");
        var replyId = listReplyId ?? buttonReplyId;

        if (string.IsNullOrWhiteSpace(replyId))
        {
            return null;
        }

        return replyId switch
        {
            "main.view_menu" => "1",
            "main.place_order" => "2",
            "main.location" => "3",
            "main.staff" => "4",
            "cart.add_more" => "add more",
            "cart.checkout" => "checkout",
            "cart.cancel" => "cancel",
            var id when id.StartsWith("menu.item.", StringComparison.OrdinalIgnoreCase) =>
                $"select_item:{id["menu.item.".Length..]}",
            var id when id.StartsWith("menu.qty.", StringComparison.OrdinalIgnoreCase) =>
                ReadQuantityReplyAsText(id),
            _ => ReadString(whatsAppMessage, "interactive", "list_reply", "title")
                ?? ReadString(whatsAppMessage, "interactive", "button_reply", "title")
        };
    }

    private static string ReadQuantityReplyAsText(string replyId)
    {
        var parts = replyId.Split('.', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 4 &&
               int.TryParse(parts[2], out var itemCode) &&
               int.TryParse(parts[3], out var quantity)
            ? $"Order: {itemCode} x {quantity}"
            : replyId;
    }

    private bool IsSignatureValid(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(_whatsAppOptions.AppSecret))
        {
            return true;
        }

        if (!Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader))
        {
            return false;
        }

        var expectedBytes = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_whatsAppOptions.AppSecret),
            Encoding.UTF8.GetBytes(rawBody));
        var expected = $"sha256={Convert.ToHexString(expectedBytes).ToLowerInvariant()}";
        var provided = signatureHeader.ToString();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided));
    }
}

public sealed record LocalWhatsAppMessageRequest(
    string MessageText,
    string? PhoneNumberId,
    string? FromPhoneNumber,
    string? WhatsAppMessageId,
    string? ProfileName);
