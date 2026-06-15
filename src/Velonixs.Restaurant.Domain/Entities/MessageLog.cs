namespace Velonixs.Restaurant.Domain.Entities;

public sealed class MessageLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? ConversationId { get; set; }
    public string Direction { get; set; } = MessageDirections.Incoming;
    public string MessageText { get; set; } = string.Empty;
    public string? WhatsAppMessageId { get; set; }
    public string Status { get; set; } = MessageStatuses.Received;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Restaurant Restaurant { get; set; } = null!;
    public Customer? Customer { get; set; }
    public Conversation? Conversation { get; set; }
}
