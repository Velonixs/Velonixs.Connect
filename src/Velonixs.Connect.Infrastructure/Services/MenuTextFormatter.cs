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
        builder.AppendLine("*Your Cart*");
        builder.AppendLine("--------------------");

        foreach (var item in draft.Items)
        {
            builder.AppendLine($"{item.Quantity} x {item.ItemName}");
            builder.AppendLine($"   Rs {FormatAmount(item.LineTotal)}");
        }

        builder.AppendLine("--------------------");
        builder.AppendLine($"*Total: Rs {FormatAmount(draft.TotalAmount)}*");
        builder.AppendLine();
        builder.AppendLine("Add more items or checkout when ready.");

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
        builder.AppendLine("*Please confirm your order*");
        builder.AppendLine("--------------------");
        builder.AppendLine($"Name: {draft.CustomerName}");
        builder.AppendLine();
        builder.AppendLine("*Items*");

        foreach (var item in draft.Items)
        {
            builder.AppendLine($"{item.Quantity} x {item.ItemName}");
            builder.AppendLine($"   Rs {FormatAmount(item.LineTotal)}");
        }

        builder.AppendLine("--------------------");
        builder.AppendLine($"*Total: Rs {FormatAmount(draft.TotalAmount)}*");
        builder.AppendLine($"Address: {draft.Address}");
        builder.AppendLine();
        builder.AppendLine("Please confirm using the buttons below.");

        return builder.ToString().Trim();
    }

    public static string BuildOrderReceived(string orderNumber)
    {
        return $"""
            *Thank you! Your order has been received.*
            Restaurant staff will confirm your order shortly.

            Order ID: *{orderNumber}*
            """;
    }

    public static string BuildRestaurantNotification(Order order, IReadOnlyCollection<OrderItem> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine("*New WhatsApp Order Received*");
        builder.AppendLine("--------------------");
        builder.AppendLine($"Order ID: *{order.OrderNumber}*");
        builder.AppendLine($"Customer: {order.CustomerName}");
        builder.AppendLine($"Phone: {order.CustomerPhone}");
        builder.AppendLine();
        builder.AppendLine("*Items*");

        foreach (var item in items)
        {
            builder.AppendLine($"{item.Quantity} x {item.ItemName}");
            builder.AppendLine($"   Rs {FormatAmount(item.LineTotal)}");
        }

        builder.AppendLine("--------------------");
        builder.AppendLine($"*Total: Rs {FormatAmount(order.TotalAmount)}*");
        builder.AppendLine($"Address: {order.Address}");
        builder.AppendLine();
        builder.AppendLine("Please confirm with customer.");

        return builder.ToString().Trim();
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.##", CultureInfo.InvariantCulture);
}
