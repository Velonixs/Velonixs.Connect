namespace Velonixs.Connect.Domain.Entities;

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNumber { get; set; } = string.Empty;
    public Guid RestaurantId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = OrderStatuses.Draft;
    public int? EstimatedMinutes { get; set; }
    public string? RestaurantComment { get; set; }
    public decimal SubTotalAmount { get; set; }
    public decimal CgstPercent { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstPercent { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Source { get; set; } = OrderSources.WhatsApp;
    public string? ExternalWhatsAppMessageId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Restaurant Restaurant { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
}
