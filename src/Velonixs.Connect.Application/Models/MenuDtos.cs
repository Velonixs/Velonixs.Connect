namespace Velonixs.Connect.Application.Models;

public sealed record MenuResponse(
    Guid RestaurantId,
    string RestaurantName,
    IReadOnlyCollection<MenuCategoryResponse> Categories,
    IReadOnlyCollection<MenuItemResponse> Items)
{
    public Guid BusinessId => RestaurantId;
    public string BusinessName => RestaurantName;
}

public sealed record MenuCategoryResponse(
    Guid Id,
    Guid RestaurantId,
    Guid? MasterCategoryId,
    string Name,
    int DisplayOrder,
    bool IsActive)
{
    public Guid BusinessId => RestaurantId;
}

public sealed record MenuItemResponse(
    Guid Id,
    Guid RestaurantId,
    Guid CategoryId,
    Guid? MasterMenuItemId,
    string CategoryName,
    int ItemCode,
    string Name,
    string? Description,
    decimal Price,
    bool IsAvailable,
    bool IsActive,
    string? ProductRetailerId = null,
    string? ImageUrl = null,
    string? MetaProductId = null,
    string SyncStatus = "NotQueued")
{
    public Guid BusinessId => RestaurantId;
    public int ProductCode => ItemCode;
}

public sealed record CreateMenuCategoryRequest(
    string Name,
    int DisplayOrder = 0,
    bool IsActive = true,
    Guid? MasterCategoryId = null);

public sealed record CreateMenuItemRequest(
    Guid CategoryId,
    Guid? MasterMenuItemId,
    int ItemCode,
    string Name,
    string? Description,
    decimal Price,
    bool IsAvailable = true,
    bool IsActive = true,
    string? ProductRetailerId = null,
    string? ImageUrl = null);

public sealed record UpdateMenuItemRequest(
    Guid CategoryId,
    Guid? MasterMenuItemId,
    int ItemCode,
    string Name,
    string? Description,
    decimal Price,
    bool IsAvailable,
    bool IsActive,
    string? ProductRetailerId = null,
    string? ImageUrl = null);

public sealed record UpdateMenuCategoryRequest(
    string Name,
    int DisplayOrder,
    bool IsActive);

public sealed record MasterCatalogResponse(
    IReadOnlyCollection<MasterMenuCategoryResponse> Categories,
    IReadOnlyCollection<MasterMenuItemResponse> Items);

public sealed record MasterMenuCategoryResponse(
    Guid Id,
    string Name,
    int DisplayOrder,
    bool IsActive);

public sealed record MasterMenuItemResponse(
    Guid Id,
    Guid MasterCategoryId,
    string MasterCategoryName,
    string Name,
    string? Description,
    bool IsActive);

public sealed record CreateMasterMenuCategoryRequest(
    string Name,
    int DisplayOrder = 0,
    bool IsActive = true);

public sealed record UpdateMasterMenuCategoryRequest(
    string Name,
    int DisplayOrder,
    bool IsActive);

public sealed record CreateMasterMenuItemRequest(
    Guid MasterCategoryId,
    string Name,
    string? Description,
    bool IsActive = true);

public sealed record UpdateMasterMenuItemRequest(
    Guid MasterCategoryId,
    string Name,
    string? Description,
    bool IsActive);
