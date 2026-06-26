using System.ComponentModel.DataAnnotations;

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
    bool IsActive)
{
    public Guid BusinessId => RestaurantId;
    public int ProductCode => ItemCode;
}

public sealed record CreateMenuCategoryRequest(
    [Required]
    string Name,
    int DisplayOrder = 0,
    bool IsActive = true,
    Guid? MasterCategoryId = null);

public sealed record CreateMenuItemRequest(
    Guid CategoryId,
    Guid? MasterMenuItemId,

    [Range(1, int.MaxValue)]
    int ItemCode,

    [Required]
    string Name,
    string? Description,

    [Range(typeof(decimal), "0", "999999999")]
    decimal Price,
    bool IsAvailable = true,
    bool IsActive = true);

public sealed record UpdateMenuItemRequest(
    Guid CategoryId,
    Guid? MasterMenuItemId,

    [Range(1, int.MaxValue)]
    int ItemCode,

    [Required]
    string Name,
    string? Description,

    [Range(typeof(decimal), "0", "999999999")]
    decimal Price,
    bool IsAvailable,
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
    [Required]
    string Name,
    int DisplayOrder = 0,
    bool IsActive = true);

public sealed record CreateMasterMenuItemRequest(
    Guid MasterCategoryId,

    [Required]
    string Name,
    string? Description,
    bool IsActive = true);
