using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IRestaurantService
{
    Task<IReadOnlyCollection<RestaurantResponse>> GetRestaurantsAsync(CancellationToken cancellationToken = default);
    Task<RestaurantResponse?> GetRestaurantAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RestaurantResponse> CreateRestaurantAsync(CreateRestaurantRequest request, CancellationToken cancellationToken = default);
    Task<RestaurantResponse?> UpdateRestaurantAsync(Guid id, UpdateRestaurantRequest request, CancellationToken cancellationToken = default);
}
