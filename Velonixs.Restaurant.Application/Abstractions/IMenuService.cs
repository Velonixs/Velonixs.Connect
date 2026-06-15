using Velonixs.Restaurant.Application.Models;

namespace Velonixs.Restaurant.Application.Abstractions;

public interface IMenuService
{
    Task<MenuResponse?> GetMenuAsync(Guid restaurantId, CancellationToken cancellationToken = default);
    Task<MenuCategoryResponse> CreateCategoryAsync(Guid restaurantId, CreateMenuCategoryRequest request, CancellationToken cancellationToken = default);
    Task<MenuItemResponse> CreateItemAsync(Guid restaurantId, CreateMenuItemRequest request, CancellationToken cancellationToken = default);
    Task<MenuItemResponse?> UpdateItemAsync(Guid id, UpdateMenuItemRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateItemAsync(Guid id, CancellationToken cancellationToken = default);
}
