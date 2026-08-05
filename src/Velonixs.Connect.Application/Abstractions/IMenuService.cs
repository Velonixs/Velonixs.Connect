using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IMenuService
{
    Task<MenuResponse?> GetMenuAsync(Guid restaurantId, CancellationToken cancellationToken = default);
    Task<MasterCatalogResponse> GetMasterCatalogAsync(CancellationToken cancellationToken = default);
    Task<MasterMenuCategoryResponse> CreateMasterCategoryAsync(CreateMasterMenuCategoryRequest request, CancellationToken cancellationToken = default);
    Task<MasterMenuCategoryResponse?> UpdateMasterCategoryAsync(Guid id, UpdateMasterMenuCategoryRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteMasterCategoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MasterMenuItemResponse> CreateMasterItemAsync(CreateMasterMenuItemRequest request, CancellationToken cancellationToken = default);
    Task<MasterMenuItemResponse?> UpdateMasterItemAsync(Guid id, UpdateMasterMenuItemRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteMasterItemAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MenuCategoryResponse> CreateCategoryAsync(Guid restaurantId, CreateMenuCategoryRequest request, CancellationToken cancellationToken = default);
    Task<MenuCategoryResponse?> UpdateCategoryAsync(Guid id, UpdateMenuCategoryRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateCategoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MenuItemResponse> CreateItemAsync(Guid restaurantId, CreateMenuItemRequest request, CancellationToken cancellationToken = default);
    Task<MenuItemResponse?> UpdateItemAsync(Guid id, UpdateMenuItemRequest request, CancellationToken cancellationToken = default);
    Task<MenuItemResponse?> SetItemAvailabilityAsync(Guid id, bool isAvailable, CancellationToken cancellationToken = default);
    Task<bool> DeactivateItemAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(Guid id, CancellationToken cancellationToken = default);
}
