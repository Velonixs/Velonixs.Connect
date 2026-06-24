namespace Velonixs.Connect.Domain.Entities;

public sealed class MasterMenuItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MasterCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MasterMenuCategory MasterCategory { get; set; } = null!;
    public ICollection<MenuItem> RestaurantMenuItems { get; set; } = new List<MenuItem>();
}
