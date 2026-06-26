using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IAdminService
{
    Task<PlatformDashboardResponse> GetPlatformDashboardAsync(CancellationToken cancellationToken = default);
    Task<RestaurantAdminDetailResponse?> GetRestaurantDetailAsync(Guid restaurantId, CancellationToken cancellationToken = default);
    Task<CreateRestaurantWithOwnerResult> CreateRestaurantWithOwnerAsync(CreateRestaurantWithOwnerRequest request, CancellationToken cancellationToken = default);
    Task<CreateStaffUserResult> CreateBusinessOwnerAsync(Guid businessId, CreateOwnerUserRequest request, CancellationToken cancellationToken = default);
    Task<ResetUserPasswordResult> ResetBusinessUserPasswordAsync(Guid businessId, Guid userId, string newPassword, CancellationToken cancellationToken = default);
}
