using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class OrderingCartService
{
    public void AddOrUpdate(PendingOrderDraft draft, MenuItem menuItem, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        draft.CartId ??= Guid.NewGuid();

        var existingItem = draft.Items.FirstOrDefault(x => x.MenuItemId == menuItem.Id);
        if (existingItem is null)
        {
            draft.Items.Add(new PendingOrderItemDraft
            {
                MenuItemId = menuItem.Id,
                ItemCode = menuItem.ItemCode,
                ItemName = menuItem.Name,
                UnitPrice = menuItem.Price,
                Quantity = quantity,
                LineTotal = menuItem.Price * quantity
            });
        }
        else
        {
            existingItem.Quantity += quantity;
            existingItem.LineTotal = existingItem.UnitPrice * existingItem.Quantity;
        }

        draft.TotalAmount = draft.Items.Sum(x => x.LineTotal);
    }
}
