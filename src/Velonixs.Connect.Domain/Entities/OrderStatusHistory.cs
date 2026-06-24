namespace Velonixs.Connect.Domain.Entities;

public sealed class OrderStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Order Order { get; set; } = null!;
}
