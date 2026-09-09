using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IWhatsAppCommerceService
{
    Task<WhatsAppCommerceSettingsResult> GetCommerceSettingsAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default);

    Task<WhatsAppCommerceSettingsResult> UpdateCommerceSettingsAsync(
        Guid restaurantId,
        bool isCatalogVisible,
        bool isCartEnabled,
        CancellationToken cancellationToken = default);

    Task<WhatsAppCommerceDiagnostics> ValidateCatalogConnectionAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default);
}
