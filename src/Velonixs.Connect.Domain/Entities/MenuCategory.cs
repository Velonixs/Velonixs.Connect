namespace Velonixs.Connect.Domain.Entities;

public sealed class MenuCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Restaurant Restaurant { get; set; } = null!;
    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}
