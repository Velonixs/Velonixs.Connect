using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

/// <summary>
/// Application boundary for platform-only business, account, dashboard, and
/// customer administration. It deliberately exposes DTOs rather than EF or
/// Identity entities so presentation projects stay persistence-agnostic.
/// </summary>
public interface IPlatformAdministrationService
{
    Task<PlatformDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<RestaurantResponse> CreateBusinessWithOwnerAsync(
        CreateBusinessWithOwnerRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<BusinessUserResponse>> GetBusinessUsersAsync(
        Guid businessId,
        CancellationToken cancellationToken = default);
    Task<BusinessUserResponse> CreateBusinessOwnerAsync(
        Guid businessId,
        CreateBusinessOwnerRequest request,
        CancellationToken cancellationToken = default);
    Task ResetBusinessUserPasswordAsync(
        Guid businessId,
        Guid userId,
        string temporaryPassword,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CustomerSummaryResponse>> GetCustomerSummariesAsync(
        Guid businessId,
        CancellationToken cancellationToken = default);
}
