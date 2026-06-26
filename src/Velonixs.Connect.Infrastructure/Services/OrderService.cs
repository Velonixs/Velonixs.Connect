using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Domain.Services;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class OrderService(
    RestaurantConnectDbContext dbContext,
    INotificationService notificationService,
    IOrderRealtimeNotifier orderRealtimeNotifier) : IOrderService
{
    public async Task<IReadOnlyCollection<OrderSummaryResponse>> GetRestaurantOrdersAsync(Guid restaurantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(x => x.RestaurantId == restaurantId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => ToSummaryResponse(x))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<OrderDetailResponse?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
        {
            return null;
        }

        var messages = await dbContext.MessageLogs
            .AsNoTracking()
            .Where(x => x.RestaurantId == order.RestaurantId && x.CustomerId == order.CustomerId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new MessageLogResponse(
                x.Id,
                x.Direction,
                x.MessageText,
                x.WhatsAppMessageId,
                x.Status,
                x.CreatedAt))
            .ToArrayAsync(cancellationToken);

        return ToDetailResponse(order, messages);
    }

    public async Task<OrderDetailResponse?> UpdateStatusAsync(
        Guid id,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(x => x.Items)
            .Include(x => x.Restaurant)
            .Include(x => x.Customer)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
        {
            return null;
        }

        var newStatus = OrderStatusTransitionPolicy.Normalize(request.Status);
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        var updatedBy = string.IsNullOrWhiteSpace(request.UpdatedBy) ? "System" : request.UpdatedBy.Trim();

        if (string.Equals(order.OrderStatus, newStatus, StringComparison.OrdinalIgnoreCase))
        {
            return ToDetailResponse(order, Array.Empty<MessageLogResponse>());
        }

        OrderStatusTransitionPolicy.ValidateTransition(
            order.OrderStatus,
            newStatus,
            comment,
            request.EstimatedMinutes);

        var previousStatus = order.OrderStatus;
        var alreadyNotified = order.StatusHistory.Any(
            x => string.Equals(x.PreviousStatus, previousStatus, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(x.NewStatus, newStatus, StringComparison.OrdinalIgnoreCase));
        order.OrderStatus = newStatus;

        if (newStatus == OrderStatuses.Confirmed)
        {
            order.EstimatedMinutes = request.EstimatedMinutes;
            order.RestaurantComment = comment;
        }
        else if (!string.IsNullOrWhiteSpace(comment))
        {
            order.RestaurantComment = comment;
        }

        var history = new OrderStatusHistory
        {
            OrderId = order.Id,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Comment = comment,
            UpdatedBy = updatedBy,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.OrderStatusHistory.Add(history);

        var notificationText = BuildCustomerNotification(order, comment);
        await dbContext.SaveChangesAsync(cancellationToken);
        await orderRealtimeNotifier.NotifyAsync(
            new OrderRealtimeEvent(
                $"{order.Id:N}:{newStatus}",
                "status_changed",
                order.RestaurantId,
                order.Id,
                order.OrderNumber,
                order.OrderStatus,
                order.TotalAmount,
                DateTimeOffset.UtcNow),
            cancellationToken);

        if (!alreadyNotified && notificationText is not null)
        {
            await NotifyCustomerAsync(order, notificationText, cancellationToken);
        }

        return ToDetailResponse(order, Array.Empty<MessageLogResponse>());
    }

    private static OrderSummaryResponse ToSummaryResponse(Domain.Entities.Order order)
    {
        return new OrderSummaryResponse(
            order.Id,
            order.OrderNumber,
            order.RestaurantId,
            order.CustomerName,
            order.CustomerPhone,
            order.Address,
            order.OrderStatus,
            order.EstimatedMinutes,
            order.RestaurantComment,
            order.SubTotalAmount,
            order.CgstPercent,
            order.CgstAmount,
            order.SgstPercent,
            order.SgstAmount,
            order.TotalAmount,
            order.Source,
            order.CreatedAt);
    }

    private static OrderDetailResponse ToDetailResponse(
        Domain.Entities.Order order,
        IReadOnlyCollection<MessageLogResponse> messages)
    {
        return new OrderDetailResponse(
            order.Id,
            order.OrderNumber,
            order.RestaurantId,
            order.CustomerId,
            order.CustomerName,
            order.CustomerPhone,
            order.Address,
            order.OrderStatus,
            order.EstimatedMinutes,
            order.RestaurantComment,
            order.SubTotalAmount,
            order.CgstPercent,
            order.CgstAmount,
            order.SgstPercent,
            order.SgstAmount,
            order.TotalAmount,
            order.Source,
            order.CreatedAt,
            order.Items.Select(x => new OrderItemResponse(
                x.Id,
                x.MenuItemId,
                x.ItemName,
                x.UnitPrice,
                x.Quantity,
                x.LineTotal)).ToArray(),
            order.StatusHistory
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Select(x => new OrderStatusHistoryResponse(
                    x.Id,
                    x.PreviousStatus,
                    x.NewStatus,
                    x.Comment,
                    x.UpdatedBy,
                    x.UpdatedAtUtc))
                .ToArray(),
            messages);
    }

    private static string? BuildCustomerNotification(Domain.Entities.Order order, string? comment)
    {
        if (order.OrderStatus == OrderStatuses.Rejected)
        {
            return MenuTextFormatter.BuildCustomerRejectionUpdate(order, comment ?? "Rejected by restaurant.");
        }

        return OrderStatuses.CustomerVisibleStatuses.Contains(order.OrderStatus)
            ? MenuTextFormatter.BuildCustomerStatusUpdate(order)
            : null;
    }

    private async Task NotifyCustomerAsync(
        Domain.Entities.Order order,
        string notificationText,
        CancellationToken cancellationToken)
    {
        var messageText = notificationText;
        var result = await notificationService.NotifyCustomerOrderStatusAsync(
            order.Restaurant,
            order.Customer,
            order,
            messageText,
            cancellationToken);
        var conversationId = await dbContext.Conversations
            .Where(x => x.RestaurantId == order.RestaurantId && x.CustomerId == order.CustomerId)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        dbContext.MessageLogs.Add(new MessageLog
        {
            RestaurantId = order.RestaurantId,
            CustomerId = order.CustomerId,
            ConversationId = conversationId,
            Direction = MessageDirections.Outgoing,
            MessageText = messageText,
            WhatsAppMessageId = result.ProviderMessageId,
            Status = result.IsSuccess ? MessageStatuses.Sent : MessageStatuses.Failed
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
