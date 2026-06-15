namespace Velonixs.Restaurant.Application.Models;

public sealed record OrderSummaryResponse(
    Guid Id,
    string OrderNumber,
    Guid RestaurantId,
    string CustomerName,
    string CustomerPhone,
    string Address,
    string OrderStatus,
    decimal TotalAmount,
    string Source,
    DateTimeOffset CreatedAt);

public sealed record OrderDetailResponse(
    Guid Id,
    string OrderNumber,
    Guid RestaurantId,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    string Address,
    string OrderStatus,
    decimal TotalAmount,
    string Source,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<OrderItemResponse> Items,
    IReadOnlyCollection<MessageLogResponse> Messages);

public sealed record OrderItemResponse(
    Guid Id,
    Guid MenuItemId,
    string ItemName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record MessageLogResponse(
    Guid Id,
    string Direction,
    string MessageText,
    string? WhatsAppMessageId,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record UpdateOrderStatusRequest(string Status);
