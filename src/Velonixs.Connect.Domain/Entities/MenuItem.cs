namespace Velonixs.Connect.Domain.Entities;

public sealed class MenuItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? MasterMenuItemId { get; set; }
    public int ItemCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ProductRetailerId { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public Restaurant Restaurant { get; set; } = null!;
    public MenuCategory Category { get; set; } = null!;
    public MasterMenuItem? MasterMenuItem { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
