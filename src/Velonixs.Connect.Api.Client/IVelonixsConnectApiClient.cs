using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Contracts.Menu;
using Velonixs.Connect.Contracts.Orders;
using Velonixs.Connect.Contracts.Restaurants;

namespace Velonixs.Connect.Api.Client;

public interface IVelonixsConnectApiClient
{
    Task<AuthTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokenResponse> RefreshAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task<PlatformDashboardResponse> GetPlatformDashboardAsync(CancellationToken cancellationToken = default);
    Task<RestaurantDashboardResponse> GetRestaurantDashboardAsync(Guid? restaurantId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RestaurantResponse>> GetRestaurantsAsync(CancellationToken cancellationToken = default);
    Task<RestaurantResponse> GetRestaurantAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RestaurantResponse> CreateRestaurantAsync(CreateRestaurantRequest request, CancellationToken cancellationToken = default);
    Task<CreateRestaurantWithOwnerResult> OnboardRestaurantAsync(CreateRestaurantWithOwnerRequest request, CancellationToken cancellationToken = default);
    Task<RestaurantResponse> UpdateRestaurantAsync(Guid id, UpdateRestaurantRequest request, CancellationToken cancellationToken = default);
    Task<RestaurantResponse> UpdateRestaurantTaxSettingsAsync(Guid id, UpdateRestaurantTaxSettingRequest request, CancellationToken cancellationToken = default);
    Task<RestaurantResponse> UpdateRestaurantAvailabilityAsync(Guid id, UpdateRestaurantAvailabilityRequest request, CancellationToken cancellationToken = default);

    Task<MenuResponse> GetMenuAsync(Guid restaurantId, CancellationToken cancellationToken = default);
    Task<MenuCategoryResponse> CreateMenuCategoryAsync(Guid restaurantId, CreateMenuCategoryRequest request, CancellationToken cancellationToken = default);
    Task<MenuItemResponse> CreateMenuItemAsync(Guid restaurantId, CreateMenuItemRequest request, CancellationToken cancellationToken = default);
    Task<MenuItemResponse> UpdateMenuItemAsync(Guid id, UpdateMenuItemRequest request, CancellationToken cancellationToken = default);
    Task<MenuItemResponse> UpdateMenuItemAvailabilityAsync(Guid id, UpdateMenuItemAvailabilityRequest request, CancellationToken cancellationToken = default);
    Task DeleteMenuItemAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrderSummaryResponse>> GetOrdersAsync(Guid? restaurantId = null, CancellationToken cancellationToken = default);
    Task<OrderDetailResponse> GetOrderAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OrderDetailResponse> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusRequest request, CancellationToken cancellationToken = default);
    Task<OrderDetailResponse> ConfirmOrderAsync(Guid id, ConfirmOrderRequest request, CancellationToken cancellationToken = default);
    Task<OrderDetailResponse> RejectOrderAsync(Guid id, RejectOrderRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CustomerSummaryResponse>> GetCustomersAsync(Guid? restaurantId = null, int take = 100, CancellationToken cancellationToken = default);
    Task<CustomerSummaryResponse> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<StaffUserResponse>> GetStaffAsync(Guid? restaurantId = null, CancellationToken cancellationToken = default);
    Task<StaffUserResponse> CreateStaffAsync(Guid? restaurantId, CreateStaffUserRequest request, CancellationToken cancellationToken = default);
    Task<StaffUserResponse> UpdateStaffStatusAsync(Guid id, Guid? restaurantId, UpdateStaffStatusRequest request, CancellationToken cancellationToken = default);

    Task<MasterCatalogResponse> GetMasterCatalogAsync(CancellationToken cancellationToken = default);
    Task<MasterMenuCategoryResponse> CreateMasterCategoryAsync(CreateMasterMenuCategoryRequest request, CancellationToken cancellationToken = default);
    Task<MasterMenuCategoryResponse> UpdateMasterCategoryAsync(Guid id, CreateMasterMenuCategoryRequest request, CancellationToken cancellationToken = default);
    Task DeleteMasterCategoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MasterMenuItemResponse> CreateMasterItemAsync(CreateMasterMenuItemRequest request, CancellationToken cancellationToken = default);
    Task<MasterMenuItemResponse> UpdateMasterItemAsync(Guid id, CreateMasterMenuItemRequest request, CancellationToken cancellationToken = default);
    Task DeleteMasterItemAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NotificationLogResponse>> GetNotificationsAsync(Guid? restaurantId = null, int take = 50, CancellationToken cancellationToken = default);
}
