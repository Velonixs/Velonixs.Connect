using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class MenuService(
    RestaurantConnectDbContext dbContext,
    IMetaCatalogSyncService metaCatalogSyncService) : IMenuService
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
        ValidateRequiredName(request.Name, "Master category name");
        var normalizedName = request.Name.Trim();
        if (await dbContext.MasterMenuCategories.AnyAsync(x => x.Name == normalizedName, cancellationToken))
        {
            throw new InvalidOperationException("A master category with this name already exists.");
        }

        var category = new MasterMenuCategory
        {
            Name = normalizedName,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive
        };

        dbContext.MasterMenuCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToMasterCategoryResponse(category);
    }

    public async Task<MasterMenuCategoryResponse?> UpdateMasterCategoryAsync(
        Guid id,
        UpdateMasterMenuCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredName(request.Name, "Master category name");
        var category = await dbContext.MasterMenuCategories
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null)
        {
            return null;
        }

        var normalizedName = request.Name.Trim();
        if (await dbContext.MasterMenuCategories.AnyAsync(x => x.Id != id && x.Name == normalizedName, cancellationToken))
        {
            throw new InvalidOperationException("A master category with this name already exists.");
        }

        category.Name = normalizedName;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToMasterCategoryResponse(category);
    }

    public async Task<bool> DeleteMasterCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.MasterMenuCategories
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null)
        {
            return false;
        }

        var isInUse = await dbContext.MasterMenuItems.AnyAsync(x => x.MasterCategoryId == id, cancellationToken) ||
                      await dbContext.MenuCategories.AnyAsync(x => x.MasterCategoryId == id, cancellationToken);
        if (isInUse)
        {
            throw new InvalidOperationException("This master category is in use. Mark it inactive instead of deleting it.");
        }

        dbContext.MasterMenuCategories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MasterMenuItemResponse> CreateMasterItemAsync(
        CreateMasterMenuItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredName(request.Name, "Master item name");
        var category = await dbContext.MasterMenuCategories
            .FirstOrDefaultAsync(x => x.Id == request.MasterCategoryId, cancellationToken);

        if (category is null)
        {
            throw new InvalidOperationException("Master category was not found.");
        }

        var normalizedName = request.Name.Trim();
        if (await dbContext.MasterMenuItems.AnyAsync(
                x => x.MasterCategoryId == category.Id && x.Name == normalizedName,
                cancellationToken))
        {
            throw new InvalidOperationException("A master item with this name already exists in the selected category.");
        }

        var item = new MasterMenuItem
        {
            MasterCategoryId = category.Id,
            MasterCategory = category,
            Name = normalizedName,
            Description = request.Description?.Trim(),
            IsActive = request.IsActive
        };

        dbContext.MasterMenuItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToMasterItemResponse(item);
    }

    public async Task<MasterMenuItemResponse?> UpdateMasterItemAsync(
        Guid id,
        UpdateMasterMenuItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredId(request.MasterCategoryId, "Master category");
        ValidateRequiredName(request.Name, "Master item name");
        var item = await dbContext.MasterMenuItems
            .Include(x => x.MasterCategory)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        var category = await dbContext.MasterMenuCategories
            .FirstOrDefaultAsync(x => x.Id == request.MasterCategoryId, cancellationToken);
        if (category is null)
        {
            throw new InvalidOperationException("Master category was not found.");
        }

        var normalizedName = request.Name.Trim();
        if (await dbContext.MasterMenuItems.AnyAsync(
                x => x.Id != id && x.MasterCategoryId == category.Id && x.Name == normalizedName,
                cancellationToken))
        {
            throw new InvalidOperationException("A master item with this name already exists in the selected category.");
        }

        item.MasterCategoryId = category.Id;
        item.MasterCategory = category;
        item.Name = normalizedName;
        item.Description = NormalizeOptional(request.Description);
        item.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToMasterItemResponse(item);
    }

    public async Task<bool> DeleteMasterItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.MasterMenuItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        if (await dbContext.MenuItems.AnyAsync(x => x.MasterMenuItemId == id, cancellationToken))
        {
            throw new InvalidOperationException("This master item is mapped by businesses. Mark it inactive instead of deleting it.");
        }

        dbContext.MasterMenuItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MenuCategoryResponse> CreateCategoryAsync(Guid restaurantId, CreateMenuCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequiredId(restaurantId, "Business");
        ValidateRequiredName(request.Name, "Category name");
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

    public async Task<MenuCategoryResponse?> UpdateCategoryAsync(Guid id, UpdateMenuCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequiredName(request.Name, "Category name");
        var category = await dbContext.MenuCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (category is null)
        {
            return null;
        }

        var catalogUpdates = await GetActiveCategoryProductQueueRequestsAsync(category.Id, cancellationToken);
        category.Name = request.Name.Trim();
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        await SaveChangesAndQueueProductsAsync(catalogUpdates, cancellationToken);

        return ToCategoryResponse(category);
    }

    public async Task<bool> DeactivateCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.MenuCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (category is null)
        {
            return false;
        }

        var catalogUpdates = await GetActiveCategoryProductQueueRequestsAsync(category.Id, cancellationToken);
        category.IsActive = false;
        await SaveChangesAndQueueProductsAsync(catalogUpdates, cancellationToken);

        return true;
    }

    public async Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.MenuCategories
            .Include(x => x.MenuItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (category is null)
        {
            return false;
        }

        if (category.MenuItems.Count > 0)
        {
            throw new InvalidOperationException("Category cannot be deleted while it has menu items.");
        }

        dbContext.MenuCategories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<MenuItemResponse> CreateItemAsync(Guid restaurantId, CreateMenuItemRequest request, CancellationToken cancellationToken = default)
    {
        ValidateMenuItemRequest(restaurantId, request.CategoryId, request.ItemCode, request.Name, request.Price);
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
            ImageUrl = NormalizeOptional(request.ImageUrl),
            IsAvailable = request.IsAvailable,
            IsActive = request.IsActive,
            Category = category
        };

        item.ProductRetailerId ??= CreateGeneratedRetailerId(restaurantId, item.Id);
        await EnsureRetailerIdIsUniqueAsync(restaurantId, item.ProductRetailerId, null, cancellationToken);

        dbContext.MenuItems.Add(item);
        await SaveChangesAndQueueProductsAsync(
            [new CatalogQueueRequest(restaurantId, item.Id, item.IsActive ? "create" : "delete")],
            cancellationToken);

        return ToItemResponse(item);
    }

    public async Task<MenuItemResponse?> UpdateItemAsync(Guid id, UpdateMenuItemRequest request, CancellationToken cancellationToken = default)
    {
        ValidateMenuItemRequest(Guid.Empty, request.CategoryId, request.ItemCode, request.Name, request.Price, requireBusinessId: false);
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
        var retailerId = NormalizeOptional(request.ProductRetailerId) ?? item.ProductRetailerId;
        retailerId ??= CreateGeneratedRetailerId(item.RestaurantId, item.Id);
        if (!string.IsNullOrWhiteSpace(item.ProductRetailerId) &&
            !string.Equals(item.ProductRetailerId, retailerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The SKU cannot be changed after a product is created. Create a replacement product instead.");
        }

        await EnsureRetailerIdIsUniqueAsync(item.RestaurantId, retailerId, item.Id, cancellationToken);
        item.ProductRetailerId = retailerId;
        item.ImageUrl = NormalizeOptional(request.ImageUrl);

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

        await SaveChangesAndQueueProductsAsync(
            [new CatalogQueueRequest(item.RestaurantId, item.Id, item.IsActive ? "update" : "delete")],
            cancellationToken);

        return ToItemResponse(item);
    }

    public async Task<MenuItemResponse?> SetItemAvailabilityAsync(
        Guid id,
        bool isAvailable,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.MenuItems
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return null;
        }

        if (item.IsAvailable == isAvailable)
        {
            return ToItemResponse(item);
        }

        item.IsAvailable = isAvailable;
        await SaveChangesAndQueueProductsAsync(
            [new CatalogQueueRequest(item.RestaurantId, item.Id, item.IsActive ? "update" : "delete")],
            cancellationToken);

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
        await SaveChangesAndQueueProductsAsync(
            [new CatalogQueueRequest(item.RestaurantId, item.Id, "delete")],
            cancellationToken);

        return true;
    }

    public async Task<bool> DeleteItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.MenuItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return false;
        }

        if (await dbContext.OrderItems.AnyAsync(x => x.MenuItemId == id, cancellationToken))
        {
            throw new InvalidOperationException("This item has order history. Deactivate it instead of deleting it.");
        }

        if (!dbContext.Database.IsRelational())
        {
            // The queue captures the remote Meta product ID and other product
            // data while the local row still exists. That snapshot lets the
            // worker deliver a delete after this item is physically removed.
            var wasQueued = await metaCatalogSyncService.QueueProductSyncAsync(
                item.RestaurantId,
                item.Id,
                "delete",
                force: true,
                cancellationToken: cancellationToken);
            if (!wasQueued)
            {
                throw new InvalidOperationException(
                    "The product could not be deleted because its Meta catalog deletion could not be queued. Configure or enable catalog synchronization, or deactivate the product instead.");
            }

            dbContext.MenuItems.Remove(item);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Keep the outbox snapshot and the physical deletion atomic. The
            // forced enqueue is intentional: an explicit deletion must retain
            // a durable remote-delete instruction even in manual sync mode.
            var wasQueued = await metaCatalogSyncService.QueueProductSyncAsync(
                item.RestaurantId,
                item.Id,
                "delete",
                force: true,
                cancellationToken: cancellationToken);
            if (!wasQueued)
            {
                throw new InvalidOperationException(
                    "The product could not be deleted because its Meta catalog deletion could not be queued. Configure or enable catalog synchronization, or deactivate the product instead.");
            }

            dbContext.MenuItems.Remove(item);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
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
            item.ProductRetailerId,
            item.ImageUrl,
            item.MetaProductId,
            item.SyncStatus);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CreateGeneratedRetailerId(Guid restaurantId, Guid itemId) =>
        $"vxc-{restaurantId:N}-{itemId:N}";

    private async Task<CatalogQueueRequest[]> GetActiveCategoryProductQueueRequestsAsync(
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        return await dbContext.MenuItems
            .AsNoTracking()
            .Where(item => item.CategoryId == categoryId && item.IsActive)
            .Select(item => new CatalogQueueRequest(item.RestaurantId, item.Id, "update"))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Persists a menu mutation and its outbox records in one SQL transaction.
    /// The in-memory provider used by unit tests has no transaction support, so
    /// it takes the equivalent sequential path only in tests.
    /// </summary>
    private async Task SaveChangesAndQueueProductsAsync(
        IReadOnlyCollection<CatalogQueueRequest> queueRequests,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            foreach (var request in queueRequests)
            {
                await metaCatalogSyncService.QueueProductSyncAsync(
                    request.BusinessId,
                    request.ProductId,
                    request.EventType,
                    cancellationToken: cancellationToken);
            }

            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            foreach (var request in queueRequests)
            {
                await metaCatalogSyncService.QueueProductSyncAsync(
                    request.BusinessId,
                    request.ProductId,
                    request.EventType,
                    cancellationToken: cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task EnsureRetailerIdIsUniqueAsync(
        Guid restaurantId,
        string retailerId,
        Guid? currentItemId,
        CancellationToken cancellationToken)
    {
        var isUsed = await dbContext.MenuItems.AnyAsync(
            item => item.RestaurantId == restaurantId &&
                    item.ProductRetailerId == retailerId &&
                    item.Id != currentItemId,
            cancellationToken);

        if (isUsed)
        {
            throw new InvalidOperationException("The product SKU must be unique within a business.");
        }
    }

    private static void ValidateMenuItemRequest(
        Guid restaurantId,
        Guid categoryId,
        int itemCode,
        string? name,
        decimal price,
        bool requireBusinessId = true)
    {
        if (requireBusinessId)
        {
            ValidateRequiredId(restaurantId, "Business");
        }

        ValidateRequiredId(categoryId, "Menu category");
        ValidateRequiredName(name, "Product name");

        if (itemCode <= 0)
        {
            throw new ArgumentException("Product code must be greater than zero.", nameof(itemCode));
        }

        if (price < 0)
        {
            throw new ArgumentException("Product price cannot be negative.", nameof(price));
        }
    }

    private static void ValidateRequiredName(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", nameof(value));
        }
    }

    private static void ValidateRequiredId(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{fieldName} is required.", nameof(value));
        }
    }

    private readonly record struct CatalogQueueRequest(Guid BusinessId, Guid ProductId, string EventType);

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
