using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Configuration;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class NotificationService(
    IOptions<SmtpOptions> smtpOptions,
    IWhatsAppMessageSender whatsAppMessageSender,
    ILogger<NotificationService> logger) : INotificationService
{
    private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

    public async Task NotifyOrderConfirmedAsync(
        Domain.Entities.Restaurant restaurant,
        Customer customer,
        Order order,
        IReadOnlyCollection<OrderItem> items,
        CancellationToken cancellationToken = default)
    {
        var notificationText = MenuTextFormatter.BuildRestaurantNotification(order, items);

        await SendEmailIfConfiguredAsync(
            restaurant.NotificationEmail ?? _smtpOptions.AdminEmail,
            $"New WhatsApp Order {order.OrderNumber}",
            notificationText,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(restaurant.StaffWhatsAppNumber))
        {
            var result = await whatsAppMessageSender.SendTextMessageAsync(
                restaurant.WhatsAppPhoneNumberId,
                restaurant.StaffWhatsAppNumber,
                notificationText,
                cancellationToken);

            if (result.IsSkipped)
            {
                throw new InvalidOperationException(
                    "WhatsApp staff notification was skipped because sending is disabled or the access token is missing.");
            }

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"WhatsApp staff notification failed. {result.Error}");
            }
        }
    }

    public Task NotifyStaffHandoverAsync(
        Domain.Entities.Restaurant restaurant,
        Customer customer,
        string customerMessage,
        CancellationToken cancellationToken = default)
    {
        var body = $"""
            Customer requested staff handover.

            Restaurant: {restaurant.Name}
            Customer phone: {customer.PhoneNumber}
            Customer name: {customer.Name}
            Message: {customerMessage}
            """;

        return SendEmailIfConfiguredAsync(
            restaurant.NotificationEmail ?? _smtpOptions.AdminEmail,
            "WhatsApp customer wants to talk to staff",
            body,
            cancellationToken);
    }

    public Task<WhatsAppSendResult> NotifyCustomerOrderStatusAsync(
        Domain.Entities.Restaurant restaurant,
        Customer customer,
        Order order,
        string message,
        CancellationToken cancellationToken = default) =>
        whatsAppMessageSender.SendTextMessageAsync(
            restaurant.WhatsAppPhoneNumberId,
            customer.PhoneNumber,
            message,
            cancellationToken);

    private async Task SendEmailIfConfiguredAsync(
        string? to,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        if (!_smtpOptions.EnableEmail ||
            string.IsNullOrWhiteSpace(_smtpOptions.Host) ||
            string.IsNullOrWhiteSpace(_smtpOptions.DefaultFromEmail) ||
            string.IsNullOrWhiteSpace(to))
        {
            logger.LogInformation(
                "Email notification skipped because SMTP configuration or recipient is missing.");

            return;
        }

        using var message = new MailMessage(_smtpOptions.DefaultFromEmail, to, subject, body);
        using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_smtpOptions.Username))
        {
            client.Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
