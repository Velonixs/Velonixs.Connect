using System.ComponentModel.DataAnnotations;

namespace Velonixs.Connect.Application.Models;

public sealed record PlatformDashboardResponse(
    IReadOnlyCollection<RestaurantAdminSummaryResponse> Restaurants,
    PlatformTaxSettingResponse TaxSetting,
    int TotalRestaurants,
    int ActiveRestaurants,
    int TotalOrders,
    decimal TotalRevenue);

public sealed record RestaurantAdminSummaryResponse(
    RestaurantResponse Restaurant,
    int MenuItemCount,
    int CustomerCount,
    int OrderCount,
    decimal Revenue,
    DateTimeOffset? LastOrderAt);

public sealed record RestaurantAdminDetailResponse(
    RestaurantResponse Restaurant,
    MenuResponse? Menu,
    IReadOnlyCollection<OrderSummaryResponse> RecentOrders,
    IReadOnlyCollection<CustomerSummaryResponse> RecentCustomers,
    IReadOnlyCollection<StaffUserResponse> Users,
    int ActiveMenuItemCount,
    int PendingOrderCount,
    decimal Revenue);

public sealed record CreateRestaurantWithOwnerRequest(
    [Required]
    CreateRestaurantRequest Restaurant,

    [Required]
    CreateOwnerUserRequest Owner);

public sealed record CreateOwnerUserRequest(
    [Required]
    string DisplayName,

    [Required]
    [EmailAddress]
    string Email,

    [Required]
    [MinLength(8)]
    string Password);

public sealed record CreateRestaurantWithOwnerResult(
    bool Succeeded,
    RestaurantResponse? Restaurant,
    StaffUserResponse? Owner,
    IReadOnlyCollection<string> Errors)
{
    public static CreateRestaurantWithOwnerResult Success(RestaurantResponse restaurant, StaffUserResponse owner) =>
        new(true, restaurant, owner, Array.Empty<string>());

    public static CreateRestaurantWithOwnerResult Failed(IEnumerable<string> errors) =>
        new(false, null, null, errors.ToArray());
}

public sealed record ResetUserPasswordResult(
    bool Succeeded,
    StaffUserResponse? User,
    IReadOnlyCollection<string> Errors)
{
    public static ResetUserPasswordResult Success(StaffUserResponse user) =>
        new(true, user, Array.Empty<string>());

    public static ResetUserPasswordResult Failed(IEnumerable<string> errors) =>
        new(false, null, errors.ToArray());
}
