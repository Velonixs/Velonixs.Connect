using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IWhatsAppCatalogService
{
    Task<CatalogMessageSendResult> SendCatalogMessageAsync(
        Guid restaurantId,
        string customerPhoneNumber,
        bool isTest = false,
        CancellationToken cancellationToken = default);
}
