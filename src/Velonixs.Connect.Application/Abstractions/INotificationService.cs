using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface INotificationService
{
    Task NotifyOrderConfirmedAsync(
        Domain.Entities.Restaurant restaurant,
        Customer customer,
        Order order,
        IReadOnlyCollection<OrderItem> items,
        CancellationToken cancellationToken = default);

    Task NotifyStaffHandoverAsync(
        Domain.Entities.Restaurant restaurant,
        Customer customer,
        string customerMessage,
        CancellationToken cancellationToken = default);

    Task<WhatsAppSendResult> NotifyCustomerOrderStatusAsync(
        Domain.Entities.Restaurant restaurant,
        Customer customer,
        Order order,
        string message,
        CancellationToken cancellationToken = default);
}
