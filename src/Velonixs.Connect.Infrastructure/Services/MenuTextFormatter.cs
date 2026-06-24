using System.Globalization;
using System.Text;
using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Infrastructure.Services;

public static class MenuTextFormatter
{
    public static string BuildWelcomeMessage(Domain.Entities.Restaurant restaurant)
    {
        return $"""
            *Welcome to {restaurant.Name}*

            Please choose an option:
            1. View Menu
            2. Place Order
            3. Restaurant Location
            4. Talk to Staff

            Reply with option number.
            """;
    }

    public static string BuildMenuMessage(
        Domain.Entities.Restaurant restaurant,
        IReadOnlyCollection<MenuCategory> categories,
        IReadOnlyCollection<MenuItem> items)
    {
        if (items.Count == 0)
        {
            return "Menu is currently not available. Please contact restaurant staff.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"*{restaurant.Name} Menu*");
        builder.AppendLine();

        foreach (var category in categories.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name))
        {
            var categoryItems = items
                .Where(x => x.CategoryId == category.Id)
                .OrderBy(x => x.ItemCode)
                .ToArray();

            if (categoryItems.Length == 0)
            {
                continue;
            }

            builder.AppendLine($"*{category.Name}*");

            foreach (var item in categoryItems)
            {
                builder.AppendLine($"{item.ItemCode}. {item.Name}");
                builder.AppendLine($"   Rs {FormatAmount(item.Price)}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("*To place order, reply like:*");
        builder.AppendLine("Order: 1 x 2, 4 x 1");

        return builder.ToString().Trim();
    }

    public static string BuildCartSummary(PendingOrderDraft draft)
    {
        var builder = new StringBuilder();
        builder.AppendLine("🛒 Your Cart");
        builder.AppendLine();

        foreach (var item in draft.Items)
        {
            builder.AppendLine($"{item.Quantity} × {item.ItemName}");
            builder.AppendLine($"Rs {FormatAmount(item.LineTotal)}");
        }

        builder.AppendLine();
        builder.AppendLine($"Total: Rs {FormatAmount(draft.TotalAmount)}");
        builder.AppendLine();
        builder.AppendLine("Select Checkout to continue or Add Items to order more.");

        return builder.ToString().Trim();
    }

    public static string BuildOrderSummary(PendingOrderDraft draft)
    {
        var builder = new StringBuilder();
        builder.AppendLine("*Checkout*");
        builder.AppendLine("--------------------");

        foreach (var item in draft.Items)
        {
            builder.AppendLine($"{item.Quantity} x {item.ItemName}");
            builder.AppendLine($"   Rs {FormatAmount(item.LineTotal)}");
        }

        builder.AppendLine("--------------------");
        builder.AppendLine($"*Total: Rs {FormatAmount(draft.TotalAmount)}*");
        builder.AppendLine();
        builder.AppendLine("Please reply with your name.");

        return builder.ToString().Trim();
    }

    public static string BuildAddressRequest(string customerName)
    {
        return $"""
            Thank you, {customerName}.

            Please share your delivery address.
            If this is pickup, reply: Pickup
            """;
    }

    public static string BuildFinalConfirmation(PendingOrderDraft draft)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Please review your order");
        builder.AppendLine();
        builder.AppendLine($"Customer Name: {draft.CustomerName}");
        builder.AppendLine();
        builder.AppendLine("Items");
        builder.AppendLine("No. | Item | Qty | Price | Amount");

        var serialNumber = 1;
        foreach (var item in draft.Items)
        {
            builder.AppendLine($"{serialNumber} | {item.ItemName} | {item.Quantity} | Rs {FormatAmount(item.UnitPrice)} | Rs {FormatAmount(item.LineTotal)}");
            serialNumber++;
        }

        builder.AppendLine("-----------");
        builder.AppendLine();
        builder.AppendLine($"Total: Rs {FormatAmount(draft.TotalAmount)}");
        builder.AppendLine();
        builder.AppendLine($"Order type: {(draft.IsPickup ? "Pickup" : "Delivery")}");
        if (!draft.IsPickup)
        {
            builder.AppendLine($"Delivery address: {draft.Address}");
        }

        builder.AppendLine();
        builder.AppendLine("Please confirm your order using the buttons below.");

        return builder.ToString().Trim();
    }

    public static string BuildOrderReceived(
        string orderNumber,
        string customerName,
        string restaurantName)
    {
        return $"""
            ✅ Order received

            Thank you, {customerName}. Your order has been sent to {restaurantName} for confirmation.

            Order ID: {orderNumber}

            We will notify you as soon as the restaurant accepts your order.
            """;
    }

    public static string BuildRestaurantNotification(Order order, IReadOnlyCollection<OrderItem> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine("🔔 New Order Received");
        builder.AppendLine();
        builder.AppendLine($"Order ID: {order.OrderNumber}");
        builder.AppendLine($"Customer: {order.CustomerName}");
        builder.AppendLine($"Phone: {order.CustomerPhone}");
        builder.AppendLine();
        builder.AppendLine("Items");

        foreach (var item in items)
        {
            builder.AppendLine($"{item.Quantity} × {item.ItemName}");
            builder.AppendLine($"Rs {FormatAmount(item.LineTotal)}");
        }

        builder.AppendLine("-----------");
        builder.AppendLine();
        builder.AppendLine($"Total: Rs {FormatAmount(order.TotalAmount)}");
        builder.AppendLine();
        builder.AppendLine($"Order type: {ResolveOrderType(order)}");
        if (!IsPickup(order))
        {
            builder.AppendLine($"Address: {order.Address}");
        }

        builder.AppendLine();
        builder.AppendLine("Please accept or reject this order from the restaurant portal.");

        return builder.ToString().Trim();
    }

    public static string BuildCustomerStatusUpdate(Order order)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Order ID: *{order.OrderNumber}*");
        builder.AppendLine($"Status: *{FormatStatus(order.OrderStatus)}*");

        if (order.OrderStatus == OrderStatuses.Confirmed && order.EstimatedMinutes is int estimatedMinutes)
        {
            builder.AppendLine($"Estimated time: *{estimatedMinutes} minutes*");
        }

        if (!string.IsNullOrWhiteSpace(order.RestaurantComment))
        {
            builder.AppendLine();
            builder.AppendLine(order.RestaurantComment);
        }

        return builder.ToString().Trim();
    }

    public static string BuildCustomerRejectionUpdate(Order order, string reason)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Order ID: *{order.OrderNumber}*");
        builder.AppendLine("Status: *Rejected*");
        builder.AppendLine();
        builder.AppendLine($"Reason: {reason}");
        return builder.ToString().Trim();
    }

    private static string FormatStatus(string status) =>
        status switch
        {
            OrderStatuses.PendingConfirmation => "Pending confirmation",
            OrderStatuses.ReadyForPickup => "Ready for pickup",
            OrderStatuses.OutForDelivery => "Out for delivery",
            _ => status
        };

    private static bool IsPickup(Order order) =>
        string.Equals(order.Address, "Pickup", StringComparison.OrdinalIgnoreCase);

    private static string ResolveOrderType(Order order) =>
        IsPickup(order) ? "Pickup" : "Delivery";

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.##", CultureInfo.InvariantCulture);
}
