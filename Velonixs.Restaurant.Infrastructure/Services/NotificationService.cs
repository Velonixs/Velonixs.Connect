using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velonixs.Restaurant.Application.Abstractions;
using Velonixs.Restaurant.Domain.Entities;
using Velonixs.Restaurant.Infrastructure.Configuration;

namespace Velonixs.Restaurant.Infrastructure.Services;

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
            await whatsAppMessageSender.SendTextMessageAsync(
                restaurant.WhatsAppPhoneNumberId,
                restaurant.StaffWhatsAppNumber,
                notificationText,
                cancellationToken);
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
                "Email notification skipped. To={To}, Subject={Subject}, Body={Body}",
                to,
                subject,
                body);

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
