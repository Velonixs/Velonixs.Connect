namespace Velonixs.Connect.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? LastAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastInteractionAt { get; set; } = DateTimeOffset.UtcNow;

    public Restaurant Restaurant { get; set; } = null!;
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<MessageLog> MessageLogs { get; set; } = new List<MessageLog>();
}
