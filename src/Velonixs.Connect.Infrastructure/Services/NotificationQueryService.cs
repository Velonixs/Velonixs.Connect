using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class NotificationQueryService(RestaurantConnectDbContext dbContext) : INotificationQueryService
{
    public async Task<IReadOnlyCollection<NotificationLogResponse>> GetRecentNotificationsAsync(
        Guid restaurantId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);

        return await dbContext.MessageLogs
            .AsNoTracking()
            .Where(x => x.RestaurantId == restaurantId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new NotificationLogResponse(
                x.Id,
                x.RestaurantId,
                x.CustomerId,
                x.Direction,
                x.MessageText,
                x.WhatsAppMessageId,
                x.Status,
                x.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }
}
