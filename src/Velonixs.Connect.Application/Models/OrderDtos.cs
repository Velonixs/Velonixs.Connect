namespace Velonixs.Connect.Application.Models;

public sealed record OrderSummaryResponse(
    Guid Id,
    string OrderNumber,
    Guid RestaurantId,
    string CustomerName,
    string CustomerPhone,
    string Address,
    string OrderStatus,
    int? EstimatedMinutes,
    string? RestaurantComment,
    decimal SubTotalAmount,
    decimal CgstPercent,
    decimal CgstAmount,
    decimal SgstPercent,
    decimal SgstAmount,
    decimal TotalAmount,
    string Source,
    DateTimeOffset CreatedAt,
    string Currency = "INR",
    string? ExternalWhatsAppMessageId = null);

public sealed record OrderDetailResponse(
    Guid Id,
    string OrderNumber,
    Guid RestaurantId,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    string Address,
    string OrderStatus,
    int? EstimatedMinutes,
    string? RestaurantComment,
    decimal SubTotalAmount,
    decimal CgstPercent,
    decimal CgstAmount,
    decimal SgstPercent,
    decimal SgstAmount,
    decimal TotalAmount,
    string Source,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<OrderItemResponse> Items,
    IReadOnlyCollection<OrderStatusHistoryResponse> StatusHistory,
    IReadOnlyCollection<MessageLogResponse> Messages,
    string Currency = "INR",
    string? ExternalWhatsAppMessageId = null);

public sealed record OrderItemResponse(
    Guid Id,
    Guid MenuItemId,
    string ItemName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    string? ProductRetailerId = null,
    string? CustomerInstructions = null);

public sealed record MessageLogResponse(
    Guid Id,
    string Direction,
    string MessageText,
    string? WhatsAppMessageId,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record OrderStatusHistoryResponse(
    Guid Id,
    string PreviousStatus,
    string NewStatus,
    string? Comment,
    string UpdatedBy,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpdateOrderStatusRequest(
    string Status,
    string? Comment = null,
    string? UpdatedBy = null,
    int? EstimatedMinutes = null);
