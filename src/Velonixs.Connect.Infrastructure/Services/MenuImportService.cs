using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class MenuImportService(RestaurantConnectDbContext dbContext) : IMenuImportService
{
    private static readonly IReadOnlyDictionary<string, string> RequiredColumns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Category"] = "Category",
            ["ItemCode"] = "ItemCode",
            ["Name"] = "Name",
            ["Price"] = "Price"
        };

    public async Task<MenuImportResult> ImportExcelAsync(
        Guid restaurantId,
        Stream workbookStream,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Restaurants.AnyAsync(x => x.Id == restaurantId, cancellationToken))
        {
            throw new KeyNotFoundException("Restaurant was not found.");
        }

        using var workbook = new XLWorkbook(workbookStream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("Workbook does not contain a worksheet.");
        var headerMap = ReadHeaderMap(worksheet);
        var missingColumns = RequiredColumns.Keys
            .Where(column => !headerMap.ContainsKey(column))
            .ToArray();

        if (missingColumns.Length > 0)
        {
            return new MenuImportResult(
                0,
                0,
                0,
                missingColumns
                    .Select(column => new MenuImportRowError(1, $"Missing required column '{column}'."))
                    .ToArray());
        }

        var categories = await dbContext.MenuCategories
            .Where(x => x.RestaurantId == restaurantId)
            .ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var itemsByCode = await dbContext.MenuItems
            .Include(x => x.Category)
            .Where(x => x.RestaurantId == restaurantId)
            .ToDictionaryAsync(x => x.ItemCode, cancellationToken);
        var errors = new List<MenuImportRowError>();
        var createdCategories = 0;
        var createdItems = 0;
        var updatedItems = 0;
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);

            if (row.IsEmpty())
            {
                continue;
            }

            var parsed = TryParseRow(row, headerMap, rowNumber, errors);

            if (parsed is null)
            {
                continue;
            }

            if (!categories.TryGetValue(parsed.CategoryName, out var category))
            {
                category = new MenuCategory
                {
                    RestaurantId = restaurantId,
                    Name = parsed.CategoryName,
                    DisplayOrder = categories.Count + 1,
                    IsActive = true
                };
                dbContext.MenuCategories.Add(category);
                categories[category.Name] = category;
                createdCategories++;
            }

            if (itemsByCode.TryGetValue(parsed.ItemCode, out var item))
            {
                item.Category = category;
                item.CategoryId = category.Id;
                item.Name = parsed.Name;
                item.Description = parsed.Description;
                item.Price = parsed.Price;
                item.IsAvailable = parsed.IsAvailable;
                item.IsActive = parsed.IsActive;
                updatedItems++;
            }
            else
            {
                item = new MenuItem
                {
                    RestaurantId = restaurantId,
                    Category = category,
                    CategoryId = category.Id,
                    ItemCode = parsed.ItemCode,
                    Name = parsed.Name,
                    Description = parsed.Description,
                    Price = parsed.Price,
                    IsAvailable = parsed.IsAvailable,
                    IsActive = parsed.IsActive
                };
                dbContext.MenuItems.Add(item);
                itemsByCode[item.ItemCode] = item;
                createdItems++;
            }
        }

        if (errors.Count == 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new MenuImportResult(
            createdCategories,
            createdItems,
            updatedItems,
            errors);
    }

    private static Dictionary<string, int> ReadHeaderMap(IXLWorksheet worksheet)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = worksheet.Row(1);
        var lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var column = 1; column <= lastColumn; column++)
        {
            var header = headerRow.Cell(column).GetString().Trim();

            if (!string.IsNullOrWhiteSpace(header))
            {
                map[header] = column;
            }
        }

        return map;
    }

    private static ParsedMenuImportRow? TryParseRow(
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap,
        int rowNumber,
        ICollection<MenuImportRowError> errors)
    {
        var categoryName = ReadString(row, headerMap, "Category");
        var name = ReadString(row, headerMap, "Name");

        if (string.IsNullOrWhiteSpace(categoryName))
        {
            errors.Add(new MenuImportRowError(rowNumber, "Category is required."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new MenuImportRowError(rowNumber, "Name is required."));
        }

        if (!int.TryParse(ReadString(row, headerMap, "ItemCode"), out var itemCode) || itemCode <= 0)
        {
            errors.Add(new MenuImportRowError(rowNumber, "ItemCode must be a positive number."));
        }

        if (!decimal.TryParse(ReadString(row, headerMap, "Price"), out var price) || price < 0)
        {
            errors.Add(new MenuImportRowError(rowNumber, "Price must be zero or greater."));
        }

        if (errors.Any(x => x.RowNumber == rowNumber))
        {
            return null;
        }

        return new ParsedMenuImportRow(
            categoryName.Trim(),
            itemCode,
            name.Trim(),
            NullIfWhiteSpace(ReadString(row, headerMap, "Description")),
            price,
            ReadBoolean(row, headerMap, "IsAvailable", defaultValue: true),
            ReadBoolean(row, headerMap, "IsActive", defaultValue: true));
    }

    private static string ReadString(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string columnName) =>
        headerMap.TryGetValue(columnName, out var column)
            ? row.Cell(column).GetFormattedString().Trim()
            : string.Empty;

    private static bool ReadBoolean(
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap,
        string columnName,
        bool defaultValue)
    {
        var value = ReadString(row, headerMap, columnName);

        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("y", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ParsedMenuImportRow(
        string CategoryName,
        int ItemCode,
        string Name,
        string? Description,
        decimal Price,
        bool IsAvailable,
        bool IsActive);
}
