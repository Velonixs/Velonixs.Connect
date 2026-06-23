using System.Globalization;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Infrastructure.Services;

/// <summary>Builds WhatsApp category list messages with bounded pagination.</summary>
public sealed class CategoryMessageBuilder : ICategoryMessageBuilder
{
    public int PageSize => 8;

    public IReadOnlyCollection<WhatsAppInteractiveListSection> BuildSections(
        IReadOnlyList<MenuCategory> categories,
        int page,
        bool hasCart)
    {
        var safePage = Math.Max(0, page);
        var rows = categories
            .Skip(safePage * PageSize)
            .Take(PageSize)
            .Select(category => new WhatsAppInteractiveListRow(
                $"category.select:{category.Id}",
                MessageText.Truncate(category.Name),
                "Browse available items"))
            .ToList();

        if (safePage > 0)
        {
            rows.Add(new WhatsAppInteractiveListRow($"category.page:{safePage - 1}", "Previous categories"));
        }

        if ((safePage + 1) * PageSize < categories.Count)
        {
            rows.Add(new WhatsAppInteractiveListRow($"category.page:{safePage + 1}", "More categories"));
        }

        if (hasCart && rows.Count < 10)
        {
            rows.Add(new WhatsAppInteractiveListRow("cart.view", "View cart"));
        }

        return new[] { new WhatsAppInteractiveListSection("Categories", rows.Take(10).ToArray()) };
    }
}

/// <summary>Builds category-scoped WhatsApp menu lists and search results.</summary>
public sealed class MenuMessageBuilder : IMenuMessageBuilder
{
    public int PageSize => 6;

    public IReadOnlyCollection<WhatsAppInteractiveListSection> BuildSections(
        Guid categoryId,
        IReadOnlyList<MenuItem> items,
        int page,
        bool hasCart)
    {
        var safePage = Math.Max(0, page);
        var rows = items
            .Skip(safePage * PageSize)
            .Take(PageSize)
            .Select(item => new WhatsAppInteractiveListRow(
                $"item.select:{item.Id}",
                MessageText.Truncate(item.Name),
                $"Rs {MessageText.FormatAmount(item.Price)}"))
            .ToList();

        if (safePage > 0)
        {
            rows.Add(new WhatsAppInteractiveListRow(
                $"item.page:{categoryId}:{safePage - 1}",
                "Previous items"));
        }

        if ((safePage + 1) * PageSize < items.Count)
        {
            rows.Add(new WhatsAppInteractiveListRow(
                $"item.page:{categoryId}:{safePage + 1}",
                "Next page"));
        }

        rows.Add(new WhatsAppInteractiveListRow("category.list", "Back to categories"));

        if (hasCart && rows.Count < 10)
        {
            rows.Add(new WhatsAppInteractiveListRow("cart.view", "View cart"));
        }

        return new[] { new WhatsAppInteractiveListSection("Available items", rows.Take(10).ToArray()) };
    }

    public IReadOnlyCollection<WhatsAppInteractiveListSection> BuildSearchSections(
        IReadOnlyList<MenuItem> items)
    {
        var rows = items.Take(9)
            .Select(item => new WhatsAppInteractiveListRow(
                $"item.select:{item.Id}",
                MessageText.Truncate(item.Name),
                $"Rs {MessageText.FormatAmount(item.Price)}"))
            .Append(new WhatsAppInteractiveListRow("category.list", "Browse categories"))
            .Take(10)
            .ToArray();

        return new[] { new WhatsAppInteractiveListSection("Matching items", rows) };
    }
}

/// <summary>Builds quantity choices and non-destructive back navigation.</summary>
public sealed class QuantityMessageBuilder : IQuantityMessageBuilder
{
    public IReadOnlyCollection<WhatsAppInteractiveListSection> BuildSections(MenuItem item)
    {
        var rows = Enumerable.Range(1, 5)
            .Select(quantity => new WhatsAppInteractiveListRow(
                $"quantity.select:{item.Id}:{quantity}",
                quantity.ToString(CultureInfo.InvariantCulture),
                $"{item.Name} x {quantity}"))
            .Append(new WhatsAppInteractiveListRow(
                $"quantity.custom:{item.Id}",
                "Custom quantity",
                "Type the quantity you need"))
            .Append(new WhatsAppInteractiveListRow("menu.back", "Back to menu"))
            .Append(new WhatsAppInteractiveListRow("category.list", "Back to categories"))
            .ToArray();

        return new[] { new WhatsAppInteractiveListSection("Quantity", rows) };
    }
}

/// <summary>Builds cart, checkout, and add-more navigation controls.</summary>
public sealed class CartNavigationMessageBuilder : ICartNavigationMessageBuilder
{
    public IReadOnlyCollection<WhatsAppReplyButton> BuildMainMenuButtons() =>
        new[]
        {
            new WhatsAppReplyButton("category.list", "Browse Menu"),
            new WhatsAppReplyButton("cart.view", "View Cart"),
            new WhatsAppReplyButton("main.staff", "Help")
        };

    public IReadOnlyCollection<WhatsAppReplyButton> BuildCartButtons() =>
        new[]
        {
            new WhatsAppReplyButton("cart.add_more", "Add More"),
            new WhatsAppReplyButton("cart.checkout", "Checkout"),
            new WhatsAppReplyButton("cart.cancel", "Cancel")
        };

    public IReadOnlyCollection<WhatsAppReplyButton> BuildFulfilmentButtons() =>
        new[]
        {
            new WhatsAppReplyButton("checkout.delivery", "Delivery"),
            new WhatsAppReplyButton("checkout.pickup", "Pickup"),
            new WhatsAppReplyButton("cart.cancel", "Cancel")
        };
}

internal static class MessageText
{
    public static string Truncate(string value, int length = 24) =>
        value.Length <= length ? value : value[..length];

    public static string FormatAmount(decimal amount) =>
        amount.ToString("0.##", CultureInfo.InvariantCulture);
}
