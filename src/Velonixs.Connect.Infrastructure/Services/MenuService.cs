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

    public async Task<MasterCatalogResponse> GetMasterCatalogAsync(CancellationToken cancellationToken = default)
    {
        var categories = await dbContext.MasterMenuCategories
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
        var items = await dbContext.MasterMenuItems
            .AsNoTracking()
            .Include(x => x.MasterCategory)
            .OrderBy(x => x.MasterCategory.DisplayOrder)
            .ThenBy(x => x.MasterCategory.Name)
            .ThenBy(x => x.Name)
            .ToArrayAsync(cancellationToken);

        return new MasterCatalogResponse(
            categories.Select(ToMasterCategoryResponse).ToArray(),
            items.Select(ToMasterItemResponse).ToArray());
    }

    public async Task<MasterMenuCategoryResponse> CreateMasterCategoryAsync(
        CreateMasterMenuCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = new MasterMenuCategory
        {
            Name = request.Name.Trim(),
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive
        };

        dbContext.MasterMenuCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToMasterCategoryResponse(category);
    }

    public async Task<MasterMenuItemResponse> CreateMasterItemAsync(
        CreateMasterMenuItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await dbContext.MasterMenuCategories
            .FirstOrDefaultAsync(x => x.Id == request.MasterCategoryId, cancellationToken);

        if (category is null)
        {
            throw new InvalidOperationException("Master category was not found.");
        }

        var item = new MasterMenuItem
        {
            MasterCategoryId = category.Id,
            MasterCategory = category,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive
        };

        dbContext.MasterMenuItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToMasterItemResponse(item);
    }

    public async Task<MenuCategoryResponse> CreateCategoryAsync(Guid restaurantId, CreateMenuCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (request.MasterCategoryId is Guid masterCategoryId)
        {
            var masterCategory = await dbContext.MasterMenuCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == masterCategoryId, cancellationToken);

            if (masterCategory is null)
            {
                throw new InvalidOperationException("Master category was not found.");
            }

            name = masterCategory.Name;
        }

        var category = new MenuCategory
        {
            RestaurantId = restaurantId,
            MasterCategoryId = request.MasterCategoryId,
            Name = name,
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
        var name = request.Name.Trim();
        var description = request.Description?.Trim();

        if (request.MasterMenuItemId is Guid masterMenuItemId)
        {
            var masterItem = await dbContext.MasterMenuItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == masterMenuItemId && x.IsActive, cancellationToken);

            if (masterItem is null)
            {
                throw new InvalidOperationException("Master menu item was not found or is inactive.");
            }

            name = masterItem.Name;
            description = masterItem.Description;
        }

        var item = new MenuItem
        {
            RestaurantId = restaurantId,
            CategoryId = request.CategoryId,
            MasterMenuItemId = request.MasterMenuItemId,
            ItemCode = request.ItemCode,
            Name = name,
            Description = description,
            Price = request.Price,
            ProductRetailerId = NormalizeOptional(request.ProductRetailerId),
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
        item.MasterMenuItemId = request.MasterMenuItemId;
        item.ItemCode = request.ItemCode;
        item.Name = request.Name.Trim();
        item.Description = request.Description?.Trim();
        item.ProductRetailerId = NormalizeOptional(request.ProductRetailerId);

        if (request.MasterMenuItemId is Guid masterMenuItemId)
        {
            var masterItem = await dbContext.MasterMenuItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == masterMenuItemId && x.IsActive, cancellationToken);

            if (masterItem is null)
            {
                throw new InvalidOperationException("Master menu item was not found or is inactive.");
            }

            item.Name = masterItem.Name;
            item.Description = masterItem.Description;
        }

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
            category.MasterCategoryId,
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
            item.MasterMenuItemId,
            item.Category?.Name ?? string.Empty,
            item.ItemCode,
            item.Name,
            item.Description,
            item.Price,
            item.IsAvailable,
            item.IsActive,
            item.ProductRetailerId);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MasterMenuCategoryResponse ToMasterCategoryResponse(MasterMenuCategory category)
    {
        return new MasterMenuCategoryResponse(
            category.Id,
            category.Name,
            category.DisplayOrder,
            category.IsActive);
    }

    private static MasterMenuItemResponse ToMasterItemResponse(MasterMenuItem item)
    {
        return new MasterMenuItemResponse(
            item.Id,
            item.MasterCategoryId,
            item.MasterCategory?.Name ?? string.Empty,
            item.Name,
            item.Description,
            item.IsActive);
    }
}
