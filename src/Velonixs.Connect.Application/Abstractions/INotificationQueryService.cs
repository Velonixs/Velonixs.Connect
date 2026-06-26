using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface INotificationQueryService
{
    Task<IReadOnlyCollection<NotificationLogResponse>> GetRecentNotificationsAsync(
        Guid restaurantId,
        int take = 50,
        CancellationToken cancellationToken = default);
}
