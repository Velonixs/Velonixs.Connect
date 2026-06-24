using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Services;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class InteractiveOrderingTests
{
    [Fact]
    public void CategoryListGeneration_PaginatesWithinWhatsAppLimit()
    {
        var categories = Enumerable.Range(1, 12)
            .Select(index => new MenuCategory { Name = $"Category {index}" })
            .ToArray();

        var sections = WhatsAppOrderingMessageBuilder.BuildCategorySections(categories, 0, false);

        Assert.Single(sections);
        Assert.Equal(9, sections.Single().Rows.Count);
        Assert.Equal("More categories", sections.Single().Rows.Last().Title);
        Assert.All(sections.Single().Rows, row => Assert.True(row.Title.Length <= 24));
    }

    [Fact]
    public void ItemPagination_AddsNavigationWithoutExceedingTenRows()
    {
        var categoryId = Guid.NewGuid();
        var items = Enumerable.Range(1, 15)
            .Select(index => Item($"Pizza {index}", index * 10, categoryId))
            .ToArray();

        var sections = WhatsAppOrderingMessageBuilder.BuildItemSections(categoryId, items, 1, true);
        var rows = sections.Single().Rows;

        Assert.Equal(10, rows.Count);
        Assert.Contains(rows, row => row.Title == "Previous items");
        Assert.Contains(rows, row => row.Title == "Next page");
        Assert.Contains(rows, row => row.Title == "Back to categories");
        Assert.Contains(rows, row => row.Title == "View cart");
    }

    [Fact]
    public void QuantitySelection_OffersOneThroughFiveAndCustom()
    {
        var item = Item("Paneer Pizza", 249);

        var rows = WhatsAppOrderingMessageBuilder.BuildQuantitySections(item).Single().Rows;

        Assert.Equal(6, rows.Count);
        Assert.Equal(new[] { "1", "2", "3", "4", "5", "Custom quantity" }, rows.Select(x => x.Title));
    }

    [Fact]
    public void DirectMenu_ShowsItemsAndUtilityActionsWithinWhatsAppLimit()
    {
        var category = new MenuCategory { Name = "Pizza" };
        var items = new[]
        {
            Item("Paneer Pizza", 249),
            Item("Margherita Pizza", 199),
            Item("Farmhouse Pizza", 299)
        };

        var sections = WhatsAppOrderingMessageBuilder.BuildDirectMenuSections(category, items, true);
        var rows = sections.SelectMany(section => section.Rows).ToArray();

        Assert.Equal(new[] { "Pizza", "More" }, sections.Select(section => section.Title));
        Assert.Contains(rows, row => row.Title == "Paneer Pizza" && row.Description == "Rs 249");
        Assert.Contains(rows, row => row.Id == "menu.more" && row.Title == "View More Items");
        Assert.Contains(rows, row => row.Id == "cart.view" && row.Title == "View Cart");
        Assert.Contains(rows, row => row.Id == "main.staff" && row.Title == "Contact Us");
        Assert.True(rows.Length <= 10);
    }

    [Fact]
    public void DirectMenu_WithCart_ShowsCheckoutAndCancelWithinWhatsAppLimit()
    {
        var category = new MenuCategory { Name = "Pizza" };
        var items = Enumerable.Range(1, 5)
            .Select(index => Item($"Pizza {index}", index * 10))
            .ToArray();

        var sections = WhatsAppOrderingMessageBuilder.BuildDirectMenuSections(category, items, true, true);
        var rows = sections.SelectMany(section => section.Rows).ToArray();

        Assert.Contains(rows, row => row.Id == "cart.checkout" && row.Title == "Checkout");
        Assert.Contains(rows, row => row.Id == "cart.cancel" && row.Title == "Cancel");
        Assert.True(rows.Length <= 10);
    }

    [Fact]
    public void ConfirmationNavigation_OffersYesAndNoButtons()
    {
        var buttons = WhatsAppOrderingMessageBuilder.BuildConfirmationButtons();

        Assert.Equal(new[] { "yes", "no" }, buttons.Select(x => x.Id));
        Assert.Equal(new[] { "Yes", "No" }, buttons.Select(x => x.Title));
    }

    [Fact]
    public void MainMenuNavigation_UsesConnectRestaurantButton()
    {
        var buttons = WhatsAppOrderingMessageBuilder.BuildMainMenuButtons();

        Assert.Contains(buttons, button =>
            button.Id == "main.staff" && button.Title == "Connect Restaurant");
        Assert.All(buttons, button => Assert.True(button.Title.Length <= 20));
    }

    [Fact]
    public void CartCalculation_MergesItemsAndRecalculatesTotals()
    {
        var draft = new PendingOrderDraft();
        var item = Item("Paneer Pizza", 249);
        var service = new OrderingCartService();

        service.AddOrUpdate(draft, item, 2);
        service.AddOrUpdate(draft, item, 1);

        Assert.Single(draft.Items);
        Assert.Equal(3, draft.Items[0].Quantity);
        Assert.Equal(747, draft.Items[0].LineTotal);
        Assert.Equal(747, draft.TotalAmount);
    }

    [Fact]
    public void SearchMatching_ReturnsOnlyActiveAvailableTenantInput()
    {
        var service = new MenuSearchService();
        var items = new[]
        {
            Item("Paneer Pizza", 249),
            Item("Paneer Butter Masala", 220),
            Item("Paneer Pizza Combo", 399, isAvailable: false)
        };

        var matches = service.Search("paneer pizza", items);

        Assert.Single(matches);
        Assert.Equal("Paneer Pizza", matches[0].Item.Name);
        Assert.Equal(1, matches[0].Score);
    }

    [Theory]
    [InlineData("2 Paneer Pizza and 1 Veg Burger")]
    [InlineData("I need 2 paneer pizza and 1 veg burger")]
    public void FreeTextOrderParsing_ExtractsQuantitiesAndExactItems(string input)
    {
        var search = new MenuSearchService();
        var parser = new FreeTextOrderParser(search);
        var items = new[]
        {
            Item("Paneer Pizza", 249),
            Item("Veg Burger", 99)
        };

        var result = parser.Parse(input, items);

        Assert.True(result.IsHighConfidence);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.Items.Sum(x => x.Quantity));
    }

    [Fact]
    public void CheckoutConfirmation_ShowsNameLineTotalsGrandTotalAndPickup()
    {
        var draft = new PendingOrderDraft
        {
            CustomerName = "Rajesh",
            Address = "Pickup",
            IsPickup = true,
            TotalAmount = 747,
            Items =
            {
                new PendingOrderItemDraft
                {
                    ItemName = "Paneer Pizza",
                    Quantity = 3,
                    UnitPrice = 249,
                    LineTotal = 747
                }
            }
        };

        var message = MenuTextFormatter.BuildFinalConfirmation(draft);

        Assert.Contains("Name: Rajesh", message);
        Assert.Contains("3 x Paneer Pizza", message);
        Assert.Contains("Rs 747", message);
        Assert.Contains("Address: Pickup", message);
        Assert.Contains("buttons below", message);
    }

    private static MenuItem Item(
        string name,
        decimal price,
        Guid? categoryId = null,
        bool isAvailable = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            RestaurantId = Guid.NewGuid(),
            CategoryId = categoryId ?? Guid.NewGuid(),
            Name = name,
            Price = price,
            IsActive = true,
            IsAvailable = isAvailable
        };
}
