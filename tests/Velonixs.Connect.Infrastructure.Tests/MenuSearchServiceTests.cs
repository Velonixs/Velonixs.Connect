using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Services;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class MenuSearchServiceTests
{
    private readonly MenuSearchService _service = new();

    [Fact]
    public void Search_UsesNameAndIsCaseInsensitive()
    {
        var matches = _service.Search("PANEER", new[]
        {
            Item("Paneer Butter Masala", 101),
            Item("Chicken Biryani", 102)
        });

        var match = Assert.Single(matches);
        Assert.Equal("Paneer Butter Masala", match.Item.Name);
        Assert.Equal(0.95, match.Score);
    }

    [Fact]
    public void Search_MatchesDescriptionCategoryRetailerIdAndItemCode()
    {
        var item = Item("Chef Special", 101, "Starter", "Smoky paneer cooked in a tandoor", "sku-paneer-101");
        var items = new[] { item };

        Assert.Single(_service.Search("tandoor", items));
        Assert.Single(_service.Search("starter", items));
        Assert.Single(_service.Search("sku-paneer", items));
        Assert.Equal(1, Assert.Single(_service.Search("code 101", items)).Score);
    }

    [Fact]
    public void Search_MatchesVegetarianKeywordAndExcludesInactiveOrUnavailableItems()
    {
        var availableVeg = Item("Veg Pulao", 101, isVegetarian: true);
        var unavailableVeg = Item("Veg Spring Roll", 102, isVegetarian: true, isAvailable: false);
        var inactiveVeg = Item("Veg Kebab", 103, isVegetarian: true, isActive: false);

        var matches = _service.Search("vegetarian", new[] { availableVeg, unavailableVeg, inactiveVeg });

        Assert.Single(matches);
        Assert.Same(availableVeg, matches[0].Item);
    }

    [Fact]
    public void Search_HandlesBlankQueriesOrdersDeterministicallyAndHonoursLimit()
    {
        var items = new[]
        {
            Item("Paneer Tikka", 3),
            Item("Paneer Butter Masala", 2),
            Item("Paneer Roll", 1)
        };

        Assert.Empty(_service.Search("   ", items));
        Assert.Empty(_service.Search(null, items));

        var matches = _service.Search("paneer", items, limit: 2);

        Assert.Equal(new[] { 1, 2 }, matches.Select(match => match.Item.ItemCode));
    }

    private static MenuItem Item(
        string name,
        int itemCode,
        string categoryName = "Main Course",
        string? description = null,
        string? retailerId = null,
        bool isVegetarian = false,
        bool isAvailable = true,
        bool isActive = true) =>
        new()
        {
            Name = name,
            ItemCode = itemCode,
            Category = new MenuCategory { Name = categoryName },
            Description = description,
            ProductRetailerId = retailerId,
            IsVegetarian = isVegetarian,
            IsAvailable = isAvailable,
            IsActive = isActive
        };
}
