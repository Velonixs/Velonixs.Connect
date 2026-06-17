using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface ICatalogService
{
    Task<CatalogResponse?> GetCatalogAsync(Guid businessId, CancellationToken cancellationToken = default);
    Task<CatalogCategoryResponse> CreateCategoryAsync(Guid businessId, CreateCatalogCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CatalogProductResponse> CreateProductAsync(Guid businessId, CreateCatalogProductRequest request, CancellationToken cancellationToken = default);
    Task<CatalogProductResponse?> UpdateProductAsync(Guid id, UpdateCatalogProductRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateProductAsync(Guid id, CancellationToken cancellationToken = default);
}
