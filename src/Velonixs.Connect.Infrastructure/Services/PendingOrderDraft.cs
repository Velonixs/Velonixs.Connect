namespace Velonixs.Connect.Infrastructure.Services;

public sealed class PendingOrderDraft
{
    public Guid? CurrentRestaurantId { get; set; }
    public Guid? CurrentCategoryId { get; set; }
    public int CurrentMenuPage { get; set; }
    public string? CurrentStep { get; set; }
    public Guid? CartId { get; set; }
    public string? CustomerName { get; set; }
    public string? Address { get; set; }
    public bool IsPickup { get; set; }
    public Guid? SelectedCategoryId { get; set; }
    public string? SelectedCategoryName { get; set; }
    public Guid? SelectedMenuItemId { get; set; }
    public bool ReturnToDefaultMenuAfterQuantity { get; set; }
    public int CategoryPage { get; set; }
    public int ItemPage { get; set; }
    public string? CheckoutState { get; set; }
    public decimal TotalAmount { get; set; }
    public List<PendingOrderItemDraft> Items { get; set; } = new();
}

public sealed class PendingOrderItemDraft
{
    public Guid MenuItemId { get; set; }
    public int ItemCode { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
