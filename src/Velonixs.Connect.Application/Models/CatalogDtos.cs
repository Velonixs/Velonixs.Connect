namespace Velonixs.Connect.Application.Models;

public sealed record CatalogResponse(
    Guid BusinessId,
    string BusinessName,
    IReadOnlyCollection<CatalogCategoryResponse> Categories,
    IReadOnlyCollection<CatalogProductResponse> Products);

public sealed record CatalogCategoryResponse(
    Guid Id,
    Guid BusinessId,
    string Name,
    int DisplayOrder,
    bool IsActive);

public sealed record CatalogProductResponse(
    Guid Id,
    Guid BusinessId,
    Guid CategoryId,
    string CategoryName,
    int ProductCode,
    string Name,
    string? Description,
    decimal Price,
    bool IsAvailable,
    bool IsActive,
    string? ProductRetailerId = null);

public sealed record CreateCatalogCategoryRequest(
    string Name,
    int DisplayOrder = 0,
    bool IsActive = true);

public sealed record CreateCatalogProductRequest(
    Guid CategoryId,
    int ProductCode,
    string Name,
    string? Description,
    decimal Price,
    bool IsAvailable = true,
    bool IsActive = true,
    string? ProductRetailerId = null);

public sealed record UpdateCatalogProductRequest(
    Guid CategoryId,
    int ProductCode,
    string Name,
    string? Description,
    decimal Price,
    bool IsAvailable,
    bool IsActive,
    string? ProductRetailerId = null);
