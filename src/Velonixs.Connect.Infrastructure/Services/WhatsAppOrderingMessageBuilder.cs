using System.Globalization;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Infrastructure.Services;

public static class WhatsAppOrderingMessageBuilder
{
    public const int CategoryPageSize = 8;
    public const int ItemPageSize = 6;
    public const int DirectMenuItemCount = 6;
    public const int NumberedMenuPageSize = 6;
    public const int ProductListItemLimit = 30;

    public static string BuildNativeCatalogBody(string restaurantName) =>
        $"Welcome to {restaurantName}. Please select the items and quantities you would like to order.";

    public static IReadOnlyCollection<WhatsAppInteractiveListSection> BuildDirectMenuSections(
        MenuCategory category,
        IReadOnlyList<MenuItem> items,
        bool hasMoreItems,
        bool hasCart = false)
    {
        return BuildDirectMenuSections(
            new[] { (Category: category, Items: items) },
            hasMoreItems,
            hasCart);
    }

    public static IReadOnlyCollection<WhatsAppInteractiveListSection> BuildDirectMenuSections(
        IReadOnlyList<(MenuCategory Category, IReadOnlyList<MenuItem> Items)> categoryGroups,
        bool hasMoreItems,
        bool hasCart = false)
    {
        var sections = categoryGroups
            .Where(group => group.Items.Count > 0)
            .Select(group => new WhatsAppInteractiveListSection(
                Truncate(group.Category.Name, 24),
                group.Items
                    .Select(item => new WhatsAppInteractiveListRow(
                        $"item.select:{item.Id}",
                        Truncate(item.Name, 24),
                        $"Rs {FormatAmount(item.Price)}"))
                    .ToArray()))
            .ToList();

        var moreRows = new List<WhatsAppInteractiveListRow>();
        if (hasMoreItems)
        {
            moreRows.Add(new WhatsAppInteractiveListRow(
                "menu.more",
                "View More Items",
                "Browse full menu"));
        }

        moreRows.Add(new WhatsAppInteractiveListRow("cart.view", "View Cart"));
        if (hasCart)
        {
            moreRows.Add(new WhatsAppInteractiveListRow("cart.checkout", "Checkout"));
            moreRows.Add(new WhatsAppInteractiveListRow("cart.cancel", "Cancel"));
        }

        moreRows.Add(new WhatsAppInteractiveListRow(
            "main.staff",
            "Contact Us",
            "Connect restaurant"));

        sections.Add(new WhatsAppInteractiveListSection("More", moreRows));

        return sections;
    }

    public static IReadOnlyCollection<WhatsAppProductListSection> BuildProductListSections(
        IReadOnlyList<(MenuCategory Category, IReadOnlyList<MenuItem> Items)> categoryGroups)
    {
        var remaining = ProductListItemLimit;
        var sections = new List<WhatsAppProductListSection>();

        foreach (var group in categoryGroups.Where(group => group.Items.Count > 0))
        {
            if (remaining <= 0)
            {
                break;
            }

            var items = group.Items
                .Where(item => !string.IsNullOrWhiteSpace(item.ProductRetailerId))
                .Take(remaining)
                .Select(item => new WhatsAppProductListItem(item.ProductRetailerId!))
                .ToArray();

            if (items.Length == 0)
            {
                continue;
            }

            sections.Add(new WhatsAppProductListSection(Truncate(group.Category.Name, 24), items));
            remaining -= items.Length;
        }

        return sections;
    }

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

    /// <summary>
    /// Builds a plain-text numbered menu so customers can reply with the item number shown on the current page.
    /// </summary>
    public static string BuildNumberedMenuText(
        string restaurantName,
        IReadOnlyList<(MenuCategory Category, MenuItem Item)> pageItems,
        int page,
        int totalPages,
        string? prefix = null,
        int startNumber = 1,
        bool showWelcome = true,
        string? instruction = null)
    {
        var builder = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            builder.AppendLine(prefix.Trim());
            builder.AppendLine();
        }

        builder.AppendLine($"🍽️ Welcome to {restaurantName}!");
        builder.AppendLine();
        if (!showWelcome)
        {
            var text = builder.ToString();
            var welcomeStart = text.LastIndexOf("Welcome to ", StringComparison.Ordinal);
            if (welcomeStart >= 0)
            {
                builder.Clear();
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    builder.AppendLine(prefix.Trim());
                    builder.AppendLine();
                }
            }
        }

        builder.AppendLine(instruction ?? "Select an item by replying with its number.");

        if (totalPages > 1)
        {
            builder.AppendLine($"Page {page + 1} of {totalPages}");
        }

        Guid? currentCategoryId = null;
        for (var index = 0; index < pageItems.Count; index++)
        {
            var (category, item) = pageItems[index];
            if (currentCategoryId != category.Id)
            {
                currentCategoryId = category.Id;
                builder.AppendLine();
                builder.AppendLine($"{ResolveCategoryIcon(category.Name)} {category.Name}");
                builder.AppendLine("━━━━━━━━━━━━");
            }

            builder.AppendLine($"{startNumber + index}. {item.Name}");
            builder.AppendLine($"   Rs {FormatAmount(item.Price)}");
        }

        builder.AppendLine();
        builder.AppendLine("Reply with an item number, type an item name, or use 'search paneer'.");

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Builds WhatsApp reply buttons for the numbered menu page while respecting the three-button limit.
    /// </summary>
    public static IReadOnlyCollection<WhatsAppReplyButton> BuildMenuNavigationButtons(
        int page,
        int totalPages)
    {
        var safePage = Math.Max(0, page);
        var safeTotalPages = Math.Max(1, totalPages);

        IReadOnlyCollection<WhatsAppReplyButton> buttons = safeTotalPages switch
        {
            <= 1 => new[]
            {
                new WhatsAppReplyButton("cart.view", "View Cart"),
                new WhatsAppReplyButton("cart.checkout", "Checkout"),
                new WhatsAppReplyButton("main.staff", "Contact Us")
            },
            _ when safePage <= 0 => new[]
            {
                new WhatsAppReplyButton("menu.next", "Next"),
                new WhatsAppReplyButton("cart.view", "View Cart"),
                new WhatsAppReplyButton("cart.checkout", "Checkout")
            },
            _ when safePage >= safeTotalPages - 1 => new[]
            {
                new WhatsAppReplyButton("menu.previous", "Previous"),
                new WhatsAppReplyButton("cart.view", "View Cart"),
                new WhatsAppReplyButton("cart.checkout", "Checkout")
            },
            _ => new[]
            {
                new WhatsAppReplyButton("menu.previous", "Previous"),
                new WhatsAppReplyButton("menu.next", "Next"),
                new WhatsAppReplyButton("cart.view", "View Cart")
            }
        };

        return buttons.Take(3).ToArray();
    }

    /// <summary>
    /// Builds compact quantity buttons. Customers can still type any numeric quantity manually.
    /// </summary>
    public static IReadOnlyCollection<WhatsAppReplyButton> BuildQuantityButtons(Guid menuItemId) =>
        new[]
        {
            new WhatsAppReplyButton($"quantity.select:{menuItemId}:1", "1"),
            new WhatsAppReplyButton($"quantity.select:{menuItemId}:2", "2"),
            new WhatsAppReplyButton($"quantity.select:{menuItemId}:3", "3")
        };

    public static IReadOnlyCollection<WhatsAppReplyButton> BuildMainMenuButtons() =>
        new[]
        {
            new WhatsAppReplyButton("category.list", "Browse Menu"),
            new WhatsAppReplyButton("cart.view", "View Cart"),
            new WhatsAppReplyButton("main.staff", "Connect Restaurant")
        };

    public static IReadOnlyCollection<WhatsAppReplyButton> BuildCartButtons() =>
        new[]
        {
            new WhatsAppReplyButton("cart.add_more", "Add Items"),
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

    public static IReadOnlyCollection<WhatsAppReplyButton> BuildConfirmationButtons() =>
        new[]
        {
            new WhatsAppReplyButton("yes", "Yes"),
            new WhatsAppReplyButton("no", "No")
        };

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.##", CultureInfo.InvariantCulture);

    private static string ResolveCategoryIcon(string categoryName)
    {
        var normalized = categoryName.Trim().ToLowerInvariant();

        if (normalized.Contains("pizza"))
        {
            return "🍕";
        }

        if (normalized.Contains("burger"))
        {
            return "🍔";
        }

        if (normalized.Contains("drink") || normalized.Contains("beverage") || normalized.Contains("juice"))
        {
            return "🥤";
        }

        if (normalized.Contains("dessert") || normalized.Contains("sweet") || normalized.Contains("cake"))
        {
            return "🍰";
        }

        if (normalized.Contains("main") || normalized.Contains("course") || normalized.Contains("biryani") || normalized.Contains("rice"))
        {
            return "🍛";
        }

        if (normalized.Contains("starter") || normalized.Contains("snack"))
        {
            return "🥟";
        }

        return "🍽️";
    }
}
