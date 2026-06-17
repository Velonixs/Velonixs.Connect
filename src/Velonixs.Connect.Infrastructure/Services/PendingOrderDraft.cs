namespace Velonixs.Connect.Infrastructure.Services;

internal sealed class PendingOrderDraft
{
    public string? CustomerName { get; set; }
    public string? Address { get; set; }
    public decimal TotalAmount { get; set; }
    public List<PendingOrderItemDraft> Items { get; set; } = new();
}

internal sealed class PendingOrderItemDraft
{
    public Guid MenuItemId { get; set; }
    public int ItemCode { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
