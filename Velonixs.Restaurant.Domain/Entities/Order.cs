namespace Velonixs.Restaurant.Domain.Entities;

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
    public decimal TotalAmount { get; set; }
    public string Source { get; set; } = OrderSources.WhatsApp;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Restaurant Restaurant { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
