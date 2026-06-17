using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class MenuService(RestaurantConnectDbContext dbContext) : IMenuService
{
    public async Task<MenuResponse?> GetMenuAsync(Guid restaurantId, CancellationToken cancellationToken = default)
    {
        var restaurant = await dbContext.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == restaurantId, cancellationToken);

        if (restaurant is null)
        {
            return null;
        }

        var categories = await dbContext.MenuCategories
            .AsNoTracking()
            .Where(x => x.RestaurantId == restaurantId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToArrayAsync(cancellationToken);

        var items = await dbContext.MenuItems
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.RestaurantId == restaurantId)
            .OrderBy(x => x.ItemCode)
            .ToArrayAsync(cancellationToken);

        return new MenuResponse(
            restaurant.Id,
            restaurant.Name,
            categories.Select(ToCategoryResponse).ToArray(),
            items.Select(ToItemResponse).ToArray());
    }

    public async Task<MenuCategoryResponse> CreateCategoryAsync(Guid restaurantId, CreateMenuCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = new MenuCategory
        {
            RestaurantId = restaurantId,
            Name = request.Name.Trim(),
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive
        };

        dbContext.MenuCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToCategoryResponse(category);
    }

    public async Task<MenuItemResponse> CreateItemAsync(Guid restaurantId, CreateMenuItemRequest request, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.MenuCategories
            .FirstAsync(x => x.RestaurantId == restaurantId && x.Id == request.CategoryId, cancellationToken);

        var item = new MenuItem
        {
            RestaurantId = restaurantId,
            CategoryId = request.CategoryId,
            ItemCode = request.ItemCode,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            IsAvailable = request.IsAvailable,
            IsActive = request.IsActive,
            Category = category
        };

        dbContext.MenuItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToItemResponse(item);
    }

    public async Task<MenuItemResponse?> UpdateItemAsync(Guid id, UpdateMenuItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.MenuItems
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return null;
        }

        var category = await dbContext.MenuCategories
            .FirstOrDefaultAsync(x => x.RestaurantId == item.RestaurantId && x.Id == request.CategoryId, cancellationToken);

        if (category is null)
        {
            throw new InvalidOperationException("Menu category was not found for this restaurant.");
        }

        item.CategoryId = request.CategoryId;
        item.Category = category;
        item.ItemCode = request.ItemCode;
        item.Name = request.Name.Trim();
        item.Description = request.Description?.Trim();
        item.Price = request.Price;
        item.IsAvailable = request.IsAvailable;
        item.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToItemResponse(item);
    }

    public async Task<bool> DeactivateItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.MenuItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return false;
        }

        item.IsActive = false;
        item.IsAvailable = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static MenuCategoryResponse ToCategoryResponse(MenuCategory category)
    {
        return new MenuCategoryResponse(
            category.Id,
            category.RestaurantId,
            category.Name,
            category.DisplayOrder,
            category.IsActive);
    }

    private static MenuItemResponse ToItemResponse(MenuItem item)
    {
        return new MenuItemResponse(
            item.Id,
            item.RestaurantId,
            item.CategoryId,
            item.Category?.Name ?? string.Empty,
            item.ItemCode,
            item.Name,
            item.Description,
            item.Price,
            item.IsAvailable,
            item.IsActive);
    }
}
