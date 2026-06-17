using Velonixs.Connect.Domain.Entities;

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
}
