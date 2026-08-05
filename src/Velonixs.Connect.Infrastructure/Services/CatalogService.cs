using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class CatalogService(IMenuService menuService) : ICatalogService
{
    public async Task<CatalogResponse?> GetCatalogAsync(Guid businessId, CancellationToken cancellationToken = default)
    {
        var menu = await menuService.GetMenuAsync(businessId, cancellationToken);
        return menu is null ? null : ToCatalogResponse(menu);
    }

    public async Task<CatalogCategoryResponse> CreateCategoryAsync(Guid businessId, CreateCatalogCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await menuService.CreateCategoryAsync(
            businessId,
            new CreateMenuCategoryRequest(request.Name, request.DisplayOrder, request.IsActive),
            cancellationToken);

        return ToCategoryResponse(category);
    }

    public async Task<CatalogCategoryResponse?> UpdateCategoryAsync(Guid id, UpdateCatalogCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await menuService.UpdateCategoryAsync(
            id,
            new UpdateMenuCategoryRequest(request.Name, request.DisplayOrder, request.IsActive),
            cancellationToken);

        return category is null ? null : ToCategoryResponse(category);
    }

    public Task<bool> DeactivateCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return menuService.DeactivateCategoryAsync(id, cancellationToken);
    }

    public Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return menuService.DeleteCategoryAsync(id, cancellationToken);
    }

    public async Task<CatalogProductResponse> CreateProductAsync(Guid businessId, CreateCatalogProductRequest request, CancellationToken cancellationToken = default)
    {
        var item = await menuService.CreateItemAsync(
            businessId,
            new CreateMenuItemRequest(
                request.CategoryId,
                null,
                request.ProductCode,
                request.Name,
                request.Description,
                request.Price,
                request.IsAvailable,
                request.IsActive,
                request.ProductRetailerId,
                request.ImageUrl),
            cancellationToken);

        return ToProductResponse(item);
    }

    public async Task<CatalogProductResponse?> UpdateProductAsync(Guid id, UpdateCatalogProductRequest request, CancellationToken cancellationToken = default)
    {
        var item = await menuService.UpdateItemAsync(
            id,
            new UpdateMenuItemRequest(
                request.CategoryId,
                null,
                request.ProductCode,
                request.Name,
                request.Description,
                request.Price,
                request.IsAvailable,
                request.IsActive,
                request.ProductRetailerId,
                request.ImageUrl),
            cancellationToken);

        return item is null ? null : ToProductResponse(item);
    }

    public Task<bool> DeactivateProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return menuService.DeactivateItemAsync(id, cancellationToken);
    }

    private static CatalogResponse ToCatalogResponse(MenuResponse menu)
    {
        return new CatalogResponse(
            menu.RestaurantId,
            menu.RestaurantName,
            menu.Categories.Select(ToCategoryResponse).ToArray(),
            menu.Items.Select(ToProductResponse).ToArray());
    }

    private static CatalogCategoryResponse ToCategoryResponse(MenuCategoryResponse category)
    {
        return new CatalogCategoryResponse(
            category.Id,
            category.RestaurantId,
            category.Name,
            category.DisplayOrder,
            category.IsActive);
    }

    private static CatalogProductResponse ToProductResponse(MenuItemResponse item)
    {
        return new CatalogProductResponse(
            item.Id,
            item.RestaurantId,
            item.CategoryId,
            item.CategoryName,
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
}
