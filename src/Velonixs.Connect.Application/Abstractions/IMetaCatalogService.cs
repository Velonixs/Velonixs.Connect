using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

/// <summary>
/// Tenant-scoped application facade for catalog reads and synchronization.
/// Local menu data remains authoritative; remote delivery is delegated to the
/// durable Meta catalog outbox.
/// </summary>
public interface IMetaCatalogService
{
    Task<MetaCatalogOverview?> GetCatalogAsync(Guid restaurantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MenuItemResponse>> GetCatalogProductsAsync(Guid restaurantId, CancellationToken cancellationToken = default);
    Task<bool> SyncProductAsync(Guid restaurantId, Guid menuItemId, CancellationToken cancellationToken = default);
    Task<int> SyncRestaurantCatalogAsync(Guid restaurantId, CancellationToken cancellationToken = default);
    Task<MetaCatalogSyncStatusResponse> GetCatalogSyncStatusAsync(Guid restaurantId, CancellationToken cancellationToken = default);
    Task<string?> CreateCatalogAsync(Guid restaurantId, string name, CancellationToken cancellationToken = default);
}
