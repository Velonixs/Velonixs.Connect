using System.Globalization;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Infrastructure.Services;

public static class WhatsAppOrderingMessageBuilder
{
    public const int CategoryPageSize = 8;
    public const int ItemPageSize = 6;

    public static IReadOnlyCollection<WhatsAppInteractiveListSection> BuildCategorySections(
        IReadOnlyList<MenuCategory> categories,
        int page,
        bool hasCart)
    {
        var safePage = Math.Max(0, page);
        var pageItems = categories.Skip(safePage * CategoryPageSize).Take(CategoryPageSize).ToArray();
        var rows = pageItems
            .Select(category => new WhatsAppInteractiveListRow(
                $"category.select:{category.Id}",
                Truncate(category.Name, 24),
                "Browse available items"))
            .ToList();

        if (safePage > 0)
        {
            rows.Add(new WhatsAppInteractiveListRow($"category.page:{safePage - 1}", "Previous categories"));
        }

        if ((safePage + 1) * CategoryPageSize < categories.Count)
        {
            rows.Add(new WhatsAppInteractiveListRow($"category.page:{safePage + 1}", "More categories"));
        }

        if (hasCart && rows.Count < 10)
        {
            rows.Add(new WhatsAppInteractiveListRow("cart.view", "View cart"));
        }

        return new[] { new WhatsAppInteractiveListSection("Categories", rows.Take(10).ToArray()) };
    }

    public static IReadOnlyCollection<WhatsAppInteractiveListSection> BuildItemSections(
        Guid categoryId,
        IReadOnlyList<MenuItem> items,
        int page,
        bool hasCart)
    {
        var safePage = Math.Max(0, page);
        var rows = items
            .Skip(safePage * ItemPageSize)
            .Take(ItemPageSize)
            .Select(item => new WhatsAppInteractiveListRow(
                $"item.select:{item.Id}",
                Truncate(item.Name, 24),
                $"Rs {FormatAmount(item.Price)}"))
            .ToList();

        if (safePage > 0)
        {
            rows.Add(new WhatsAppInteractiveListRow(
                $"item.page:{categoryId}:{safePage - 1}",
                "Previous items"));
        }

        if ((safePage + 1) * ItemPageSize < items.Count)
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

    public static IReadOnlyCollection<WhatsAppInteractiveListSection> BuildSearchSections(
        IReadOnlyList<MenuItem> items)
    {
        var rows = items.Take(9)
            .Select(item => new WhatsAppInteractiveListRow(
                $"item.select:{item.Id}",
                Truncate(item.Name, 24),
                $"Rs {FormatAmount(item.Price)}"))
            .Append(new WhatsAppInteractiveListRow("category.list", "Browse categories"))
            .Take(10)
            .ToArray();

        return new[] { new WhatsAppInteractiveListSection("Matching items", rows) };
    }

    public static IReadOnlyCollection<WhatsAppInteractiveListSection> BuildQuantitySections(MenuItem item)
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
            .ToArray();

        return new[] { new WhatsAppInteractiveListSection("Quantity", rows) };
    }

    public static IReadOnlyCollection<WhatsAppInteractiveListSection> BuildItemAddedSections(
        string? categoryName)
    {
        var currentCategory = string.IsNullOrWhiteSpace(categoryName)
            ? "Current category"
            : categoryName;

        return new[]
        {
            new WhatsAppInteractiveListSection(
                "Continue",
                new[]
                {
                    new WhatsAppInteractiveListRow(
                        "menu.continue",
                        Truncate($"More {currentCategory}", 24),
                        "Select another item"),
                    new WhatsAppInteractiveListRow(
                        "category.list",
                        "Change category",
                        "Browse other items"),
                    new WhatsAppInteractiveListRow("cart.view", "View cart"),
                    new WhatsAppInteractiveListRow("cart.checkout", "Checkout")
                })
        };
    }

    public static IReadOnlyCollection<WhatsAppReplyButton> BuildMainMenuButtons() =>
        new[]
        {
            new WhatsAppReplyButton("category.list", "Browse Menu"),
            new WhatsAppReplyButton("cart.view", "View Cart"),
            new WhatsAppReplyButton("main.staff", "Help")
        };

    public static IReadOnlyCollection<WhatsAppReplyButton> BuildCartButtons() =>
        new[]
        {
            new WhatsAppReplyButton("cart.add_more", "Add More"),
            new WhatsAppReplyButton("cart.checkout", "Checkout"),
            new WhatsAppReplyButton("cart.cancel", "Cancel")
        };

    public static IReadOnlyCollection<WhatsAppReplyButton> BuildFulfilmentButtons() =>
        new[]
        {
            new WhatsAppReplyButton("checkout.delivery", "Delivery"),
            new WhatsAppReplyButton("checkout.pickup", "Pickup"),
            new WhatsAppReplyButton("cart.cancel", "Cancel")
        };

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.##", CultureInfo.InvariantCulture);
}
