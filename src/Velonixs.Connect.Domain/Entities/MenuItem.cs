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
    public decimal? DiscountPrice { get; set; }
    public string Currency { get; set; } = "INR";
    public string? ProductRetailerId { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsVegetarian { get; set; }
    public int? PreparationTimeMinutes { get; set; }
    public string? MetaCatalogId { get; set; }
    public string? MetaProductId { get; set; }
    public string SyncStatus { get; set; } = "NotQueued";
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }
    public int RetryCount { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Restaurant Restaurant { get; set; } = null!;
    public MenuCategory Category { get; set; } = null!;
    public MasterMenuItem? MasterMenuItem { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
