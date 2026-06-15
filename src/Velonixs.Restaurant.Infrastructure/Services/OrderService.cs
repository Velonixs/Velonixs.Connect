using Microsoft.EntityFrameworkCore;
using Velonixs.Restaurant.Application.Abstractions;
using Velonixs.Restaurant.Application.Models;
using Velonixs.Restaurant.Infrastructure.Persistence;

namespace Velonixs.Restaurant.Infrastructure.Services;

public sealed class OrderService(RestaurantConnectDbContext dbContext) : IOrderService
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

    public async Task<OrderDetailResponse?> UpdateStatusAsync(Guid id, UpdateOrderStatusRequest request, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
        {
            return null;
        }

        order.OrderStatus = request.Status.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);

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
            messages);
    }
}
