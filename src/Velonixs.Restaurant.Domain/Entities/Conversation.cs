namespace Velonixs.Restaurant.Domain.Entities;

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid CustomerId { get; set; }
    public string WhatsAppNumber { get; set; } = string.Empty;
    public string CurrentState { get; set; } = ConversationStates.New;
    public string? TempOrderJson { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Restaurant Restaurant { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public ICollection<MessageLog> MessageLogs { get; set; } = new List<MessageLog>();
}
