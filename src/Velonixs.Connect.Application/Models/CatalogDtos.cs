using System.ComponentModel.DataAnnotations;

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
    bool IsActive);

public sealed record CreateCatalogCategoryRequest(
    [Required]
    string Name,
    int DisplayOrder = 0,
    bool IsActive = true);

public sealed record CreateCatalogProductRequest(
    Guid CategoryId,

    [Range(1, int.MaxValue)]
    int ProductCode,

    [Required]
    string Name,
    string? Description,

    [Range(typeof(decimal), "0", "999999999")]
    decimal Price,
    bool IsAvailable = true,
    bool IsActive = true);

public sealed record UpdateCatalogProductRequest(
    Guid CategoryId,

    [Range(1, int.MaxValue)]
    int ProductCode,

    [Required]
    string Name,
    string? Description,

    [Range(typeof(decimal), "0", "999999999")]
    decimal Price,
    bool IsAvailable,
    bool IsActive);
