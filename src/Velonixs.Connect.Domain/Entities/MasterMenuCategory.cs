namespace Velonixs.Connect.Domain.Entities;

public sealed class MasterMenuCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MasterMenuItem> Items { get; set; } = new List<MasterMenuItem>();
    public ICollection<MenuCategory> RestaurantCategories { get; set; } = new List<MenuCategory>();
}
