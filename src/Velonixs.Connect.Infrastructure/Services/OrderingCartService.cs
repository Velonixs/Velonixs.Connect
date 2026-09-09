using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class OrderingCartService
{
    public void AddOrUpdate(
        PendingOrderDraft draft,
        MenuItem menuItem,
        int quantity,
        decimal cgstPercent = 0,
        decimal sgstPercent = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        draft.CartId ??= Guid.NewGuid();
        var effectivePrice = menuItem.DiscountPrice ?? menuItem.Price;
        draft.Currency = menuItem.Currency;

        var existingItem = draft.Items.FirstOrDefault(x => x.MenuItemId == menuItem.Id);
        if (existingItem is null)
        {
            draft.Items.Add(new PendingOrderItemDraft
            {
                MenuItemId = menuItem.Id,
                ItemCode = menuItem.ItemCode,
                ItemName = menuItem.Name,
                ProductRetailerId = menuItem.ProductRetailerId,
                UnitPrice = effectivePrice,
                Quantity = quantity,
                LineTotal = effectivePrice * quantity
            });
        }
        else
        {
            existingItem.ItemName = menuItem.Name;
            existingItem.ProductRetailerId = menuItem.ProductRetailerId;
            existingItem.UnitPrice = effectivePrice;
            existingItem.Quantity += quantity;
            existingItem.LineTotal = existingItem.UnitPrice * existingItem.Quantity;
        }

        RecalculateTotals(draft, cgstPercent, sgstPercent);
    }

    public void RecalculateTotals(PendingOrderDraft draft, decimal cgstPercent, decimal sgstPercent)
    {
        draft.SubTotalAmount = draft.Items.Sum(x => x.LineTotal);
        draft.CgstPercent = cgstPercent;
        draft.SgstPercent = sgstPercent;
        draft.CgstAmount = RoundCurrency(draft.SubTotalAmount * cgstPercent / 100);
        draft.SgstAmount = RoundCurrency(draft.SubTotalAmount * sgstPercent / 100);
        draft.TotalAmount = draft.SubTotalAmount + draft.CgstAmount + draft.SgstAmount;
    }

    private static decimal RoundCurrency(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
