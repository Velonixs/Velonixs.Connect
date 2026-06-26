using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Security;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class MenuImportServiceTests
{
    [Fact]
    public async Task ImportExcelAsync_CreatesCategoriesAndItems()
    {
        await using var dbContext = CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = "phone-id"
        };
        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync();
        var service = new MenuImportService(dbContext);

        await using var workbook = CreateWorkbook(
            ["Meals", "1", "Paneer Meal", "Paneer and rice", "249", "yes", "true"],
            ["Drinks", "2", "Lassi", "", "80", "no", "true"]);

        var result = await service.ImportExcelAsync(restaurant.Id, workbook);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.CreatedCategories);
        Assert.Equal(2, result.CreatedItems);
        Assert.Equal(0, result.UpdatedItems);
        Assert.Equal(2, await dbContext.MenuCategories.CountAsync());
        Assert.Equal(2, await dbContext.MenuItems.CountAsync());
        Assert.False((await dbContext.MenuItems.SingleAsync(x => x.ItemCode == 2)).IsAvailable);
    }

    [Fact]
    public async Task ImportExcelAsync_UpdatesExistingItemByItemCode()
    {
        await using var dbContext = CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = "phone-id"
        };
        var category = new MenuCategory
        {
            Restaurant = restaurant,
            Name = "Meals"
        };
        dbContext.AddRange(
            restaurant,
            category,
            new MenuItem
            {
                Restaurant = restaurant,
                Category = category,
                ItemCode = 1,
                Name = "Old Meal",
                Price = 100,
                IsAvailable = true,
                IsActive = true
            });
        await dbContext.SaveChangesAsync();
        var service = new MenuImportService(dbContext);

        await using var workbook = CreateWorkbook(
            ["Meals", "1", "Updated Meal", "Updated", "199", "true", "true"]);

        var result = await service.ImportExcelAsync(restaurant.Id, workbook);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.CreatedItems);
        Assert.Equal(1, result.UpdatedItems);
        var item = await dbContext.MenuItems.SingleAsync();
        Assert.Equal("Updated Meal", item.Name);
        Assert.Equal(199, item.Price);
    }

    [Fact]
    public async Task ImportExcelAsync_ReturnsRowErrorsWithoutSavingInvalidRows()
    {
        await using var dbContext = CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = "phone-id"
        };
        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync();
        var service = new MenuImportService(dbContext);

        await using var workbook = CreateWorkbook(
            ["Meals", "not-a-code", "Paneer Meal", "Paneer and rice", "249", "yes", "true"]);

        var result = await service.ImportExcelAsync(restaurant.Id, workbook);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, x => x.RowNumber == 2 && x.Message.Contains("ItemCode"));
        Assert.Equal(0, await dbContext.MenuItems.CountAsync());
    }

    private static MemoryStream CreateWorkbook(params string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Menu");
        var headers = new[]
        {
            "Category",
            "ItemCode",
            "Name",
            "Description",
            "Price",
            "IsAvailable",
            "IsActive"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
            {
                worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = rows[rowIndex][columnIndex];
            }
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static RestaurantConnectDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RestaurantConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RestaurantConnectDbContext(options, new PassThroughEncryptionService());
    }

    private sealed class PassThroughEncryptionService : IFieldEncryptionService
    {
        public string Encrypt(string plaintext) => plaintext;
        public string Decrypt(string protectedValue) => protectedValue;
    }
}
