namespace Velonixs.Connect.Domain.Entities;

public sealed class Restaurant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string BusinessType { get; set; } = BusinessTypes.Restaurant;
    public string WhatsAppPhoneNumberId { get; set; } = string.Empty;
    public string? BusinessPhone { get; set; }
    public string? NotificationEmail { get; set; }
    public string? StaffWhatsAppNumber { get; set; }
    public string? Address { get; set; }
    public decimal CgstPercent { get; set; }
    public decimal SgstPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MenuCategory> MenuCategories { get; set; } = new List<MenuCategory>();
    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<MessageLog> MessageLogs { get; set; } = new List<MessageLog>();
}
