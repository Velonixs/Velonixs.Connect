using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface ICustomerService
{
    Task<CustomerSummaryResponse?> GetCustomerAsync(
        Guid id,
        bool includeOrderCount = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CustomerSummaryResponse>> GetRestaurantCustomersAsync(
        Guid restaurantId,
        int take = 100,
        bool includeOrderCount = false,
        CancellationToken cancellationToken = default);

    Task<CustomerSummaryResponse?> UpdateCustomerAsync(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default);
}
