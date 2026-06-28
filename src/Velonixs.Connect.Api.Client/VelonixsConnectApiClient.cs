using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Contracts.Common;
using Velonixs.Connect.Contracts.Menu;
using Velonixs.Connect.Contracts.Orders;
using Velonixs.Connect.Contracts.Restaurants;

namespace Velonixs.Connect.Api.Client;

public sealed class VelonixsConnectApiClient(
    HttpClient httpClient,
    IApiTokenStore tokenStore) : IVelonixsConnectApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AuthTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<LoginRequest, AuthTokenResponse>(
            HttpMethod.Post,
            "api/v1/auth/login",
            request,
            allowRefresh: false,
            cancellationToken);

        await tokenStore.SaveAsync(InMemoryApiTokenStore.FromResponse(response), cancellationToken);
        return response;
    }

    public async Task<AuthTokenResponse> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var response = await RefreshAccessTokenAsync(cancellationToken)
            ?? throw new ApiClientException(HttpStatusCode.Unauthorized, null, "Refresh token is missing or expired.");

        return response;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var tokenState = await tokenStore.GetAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(tokenState?.RefreshToken))
        {
            await SendAsync<LogoutRequest>(
                HttpMethod.Post,
                "api/v1/auth/logout",
                new LogoutRequest(tokenState.RefreshToken),
                allowRefresh: false,
                cancellationToken);
        }

        await tokenStore.ClearAsync(cancellationToken);
    }

    public Task<PlatformDashboardResponse> GetPlatformDashboardAsync(CancellationToken cancellationToken = default) =>
        SendAsync<PlatformDashboardResponse>(HttpMethod.Get, "api/v1/dashboard/platform", cancellationToken);

    public Task<PlatformTaxSettingResponse> UpdatePlatformTaxSettingsAsync(UpdateRestaurantTaxSettingRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<UpdateRestaurantTaxSettingRequest, PlatformTaxSettingResponse>(HttpMethod.Patch, "api/v1/dashboard/platform/tax-settings", request, cancellationToken);

    public Task<RestaurantDashboardResponse> GetRestaurantDashboardAsync(Guid? restaurantId = null, CancellationToken cancellationToken = default) =>
        SendAsync<RestaurantDashboardResponse>(
            HttpMethod.Get,
            WithQuery("api/v1/dashboard/summary", ("restaurantId", restaurantId?.ToString())),
            cancellationToken);

    public Task<IReadOnlyCollection<RestaurantResponse>> GetRestaurantsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyCollection<RestaurantResponse>>(HttpMethod.Get, "api/v1/restaurants", cancellationToken);

    public Task<RestaurantResponse> GetRestaurantAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<RestaurantResponse>(HttpMethod.Get, $"api/v1/restaurants/{id}", cancellationToken);

    public Task<RestaurantResponse> CreateRestaurantAsync(CreateRestaurantRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CreateRestaurantRequest, RestaurantResponse>(HttpMethod.Post, "api/v1/restaurants", request, cancellationToken);

    public Task<CreateRestaurantWithOwnerResult> OnboardRestaurantAsync(CreateRestaurantWithOwnerRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CreateRestaurantWithOwnerRequest, CreateRestaurantWithOwnerResult>(HttpMethod.Post, "api/v1/restaurants/onboard", request, cancellationToken);

    public Task<RestaurantResponse> UpdateRestaurantAsync(Guid id, UpdateRestaurantRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<UpdateRestaurantRequest, RestaurantResponse>(HttpMethod.Put, $"api/v1/restaurants/{id}", request, cancellationToken);

    public Task<RestaurantResponse> UpdateRestaurantTaxSettingsAsync(Guid id, UpdateRestaurantTaxSettingRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<UpdateRestaurantTaxSettingRequest, RestaurantResponse>(HttpMethod.Patch, $"api/v1/restaurants/{id}/tax-settings", request, cancellationToken);

    public Task<RestaurantResponse> UpdateRestaurantAvailabilityAsync(Guid id, UpdateRestaurantAvailabilityRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<UpdateRestaurantAvailabilityRequest, RestaurantResponse>(HttpMethod.Patch, $"api/v1/restaurants/{id}/availability", request, cancellationToken);

    public Task<MenuResponse> GetMenuAsync(Guid restaurantId, CancellationToken cancellationToken = default) =>
        SendAsync<MenuResponse>(HttpMethod.Get, $"api/v1/restaurants/{restaurantId}/menu", cancellationToken);

    public Task<MenuCategoryResponse> CreateMenuCategoryAsync(Guid restaurantId, CreateMenuCategoryRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CreateMenuCategoryRequest, MenuCategoryResponse>(HttpMethod.Post, $"api/v1/restaurants/{restaurantId}/menu-categories", request, cancellationToken);

    public Task<MenuItemResponse> CreateMenuItemAsync(Guid restaurantId, CreateMenuItemRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CreateMenuItemRequest, MenuItemResponse>(HttpMethod.Post, $"api/v1/restaurants/{restaurantId}/menu-items", request, cancellationToken);

    public Task<MenuItemResponse> UpdateMenuItemAsync(Guid id, UpdateMenuItemRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<UpdateMenuItemRequest, MenuItemResponse>(HttpMethod.Put, $"api/v1/menu-items/{id}", request, cancellationToken);

    public Task<MenuItemResponse> UpdateMenuItemAvailabilityAsync(Guid id, UpdateMenuItemAvailabilityRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<UpdateMenuItemAvailabilityRequest, MenuItemResponse>(HttpMethod.Patch, $"api/v1/menu-items/{id}/availability", request, cancellationToken);

    public Task DeleteMenuItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/v1/menu-items/{id}", cancellationToken);

    public Task<IReadOnlyCollection<OrderSummaryResponse>> GetOrdersAsync(Guid? restaurantId = null, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyCollection<OrderSummaryResponse>>(
            HttpMethod.Get,
            WithQuery("api/v1/orders", ("restaurantId", restaurantId?.ToString())),
            cancellationToken);

    public Task<OrderDetailResponse> GetOrderAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<OrderDetailResponse>(HttpMethod.Get, $"api/v1/orders/{id}", cancellationToken);

    public Task<OrderDetailResponse> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<UpdateOrderStatusRequest, OrderDetailResponse>(HttpMethod.Patch, $"api/v1/orders/{id}/status", request, cancellationToken);

    public Task<OrderDetailResponse> ConfirmOrderAsync(Guid id, ConfirmOrderRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ConfirmOrderRequest, OrderDetailResponse>(HttpMethod.Post, $"api/v1/orders/{id}/confirm", request, cancellationToken);

    public Task<OrderDetailResponse> RejectOrderAsync(Guid id, RejectOrderRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<RejectOrderRequest, OrderDetailResponse>(HttpMethod.Post, $"api/v1/orders/{id}/reject", request, cancellationToken);

    public Task<IReadOnlyCollection<CustomerSummaryResponse>> GetCustomersAsync(Guid? restaurantId = null, int take = 100, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyCollection<CustomerSummaryResponse>>(
            HttpMethod.Get,
            WithQuery("api/v1/customers", ("restaurantId", restaurantId?.ToString()), ("take", take.ToString())),
            cancellationToken);

    public Task<CustomerSummaryResponse> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<UpdateCustomerRequest, CustomerSummaryResponse>(HttpMethod.Put, $"api/v1/customers/{id}", request, cancellationToken);

    public Task<IReadOnlyCollection<StaffUserResponse>> GetStaffAsync(Guid? restaurantId = null, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyCollection<StaffUserResponse>>(
            HttpMethod.Get,
            WithQuery("api/v1/staff", ("restaurantId", restaurantId?.ToString())),
            cancellationToken);

    public Task<StaffUserResponse> CreateStaffAsync(Guid? restaurantId, CreateStaffUserRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CreateStaffUserRequest, StaffUserResponse>(
            HttpMethod.Post,
            WithQuery("api/v1/staff", ("restaurantId", restaurantId?.ToString())),
            request,
            cancellationToken);

    public Task<StaffUserResponse> UpdateStaffStatusAsync(Guid id, Guid? restaurantId, UpdateStaffStatusRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<UpdateStaffStatusRequest, StaffUserResponse>(
            HttpMethod.Patch,
            WithQuery($"api/v1/staff/{id}/status", ("restaurantId", restaurantId?.ToString())),
            request,
            cancellationToken);

    public Task<MasterCatalogResponse> GetMasterCatalogAsync(CancellationToken cancellationToken = default) =>
        SendAsync<MasterCatalogResponse>(HttpMethod.Get, "api/v1/master-catalog", cancellationToken);

    public Task<MasterMenuCategoryResponse> CreateMasterCategoryAsync(CreateMasterMenuCategoryRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CreateMasterMenuCategoryRequest, MasterMenuCategoryResponse>(HttpMethod.Post, "api/v1/master-catalog/categories", request, cancellationToken);

    public Task<MasterMenuCategoryResponse> UpdateMasterCategoryAsync(Guid id, CreateMasterMenuCategoryRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CreateMasterMenuCategoryRequest, MasterMenuCategoryResponse>(HttpMethod.Put, $"api/v1/master-catalog/categories/{id}", request, cancellationToken);

    public Task DeleteMasterCategoryAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/v1/master-catalog/categories/{id}", cancellationToken);

    public Task<MasterMenuItemResponse> CreateMasterItemAsync(CreateMasterMenuItemRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CreateMasterMenuItemRequest, MasterMenuItemResponse>(HttpMethod.Post, "api/v1/master-catalog/items", request, cancellationToken);

    public Task<MasterMenuItemResponse> UpdateMasterItemAsync(Guid id, CreateMasterMenuItemRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CreateMasterMenuItemRequest, MasterMenuItemResponse>(HttpMethod.Put, $"api/v1/master-catalog/items/{id}", request, cancellationToken);

    public Task DeleteMasterItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/v1/master-catalog/items/{id}", cancellationToken);

    public Task<IReadOnlyCollection<NotificationLogResponse>> GetNotificationsAsync(Guid? restaurantId = null, int take = 50, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyCollection<NotificationLogResponse>>(
            HttpMethod.Get,
            WithQuery("api/v1/notifications", ("restaurantId", restaurantId?.ToString()), ("take", take.ToString())),
            cancellationToken);

    private async Task SendAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await SendRequestAsync(method, path, () => null, allowRefresh: true, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task SendAsync<TRequest>(
        HttpMethod method,
        string path,
        TRequest request,
        bool allowRefresh,
        CancellationToken cancellationToken)
    {
        using var response = await SendRequestAsync(
            method,
            path,
            () => JsonContent.Create(request, options: JsonOptions),
            allowRefresh,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        return SendAsync<TResponse>(method, path, allowRefresh: true, cancellationToken);
    }

    private async Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string path,
        bool allowRefresh,
        CancellationToken cancellationToken)
    {
        using var response = await SendRequestAsync(method, path, () => null, allowRefresh, cancellationToken);
        return await ReadSuccessAsync<TResponse>(response, cancellationToken);
    }

    private Task<TResponse> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync<TRequest, TResponse>(method, path, request, allowRefresh: true, cancellationToken);
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest request,
        bool allowRefresh,
        CancellationToken cancellationToken)
    {
        using var response = await SendRequestAsync(
            method,
            path,
            () => JsonContent.Create(request, options: JsonOptions),
            allowRefresh,
            cancellationToken);
        return await ReadSuccessAsync<TResponse>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpMethod method,
        string path,
        Func<HttpContent?> createContent,
        bool allowRefresh,
        CancellationToken cancellationToken)
    {
        var request = await CreateRequestAsync(method, path, createContent(), cancellationToken);
        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized || !allowRefresh)
        {
            return response;
        }

        response.Dispose();

        if (await RefreshAccessTokenAsync(cancellationToken) is null)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                RequestMessage = new HttpRequestMessage(method, path)
            };
        }

        var retryRequest = await CreateRequestAsync(method, path, createContent(), cancellationToken);
        return await httpClient.SendAsync(retryRequest, cancellationToken);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = content
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", Guid.NewGuid().ToString("N"));

        var tokenState = await tokenStore.GetAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(tokenState?.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenState.AccessToken);
        }

        return request;
    }

    private async Task<AuthTokenResponse?> RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        var tokenState = await tokenStore.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenState?.RefreshToken))
        {
            return null;
        }

        try
        {
            var response = await SendAsync<RefreshTokenRequest, AuthTokenResponse>(
                HttpMethod.Post,
                "api/v1/auth/refresh",
                new RefreshTokenRequest(tokenState.RefreshToken),
                allowRefresh: false,
                cancellationToken);
            await tokenStore.SaveAsync(InMemoryApiTokenStore.FromResponse(response), cancellationToken);
            return response;
        }
        catch (ApiClientException) when (!cancellationToken.IsCancellationRequested)
        {
            await tokenStore.ClearAsync(cancellationToken);
            return null;
        }
    }

    private static async Task<TResponse> ReadSuccessAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
        return body ?? throw new ApiClientException(
            response.StatusCode,
            null,
            "The API returned an empty response body.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiErrorResponse? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
        }

        throw new ApiClientException(
            response.StatusCode,
            error,
            error?.Message ?? $"API request failed with status {(int)response.StatusCode}.");
    }

    private static string WithQuery(string path, params (string Key, string? Value)[] values)
    {
        var query = values
            .Where(value => !string.IsNullOrWhiteSpace(value.Value))
            .Select(value => $"{Uri.EscapeDataString(value.Key)}={Uri.EscapeDataString(value.Value!)}")
            .ToArray();

        return query.Length == 0 ? path : $"{path}?{string.Join("&", query)}";
    }
}
