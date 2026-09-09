using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class WhatsAppCommerceService(
    RestaurantConnectDbContext dbContext,
    HttpClient httpClient,
    IOptions<MetaCatalogOptions> options) : IWhatsAppCommerceService
{
    private readonly MetaCatalogOptions _options = options.Value;

    public async Task<WhatsAppCommerceSettingsResult> GetCommerceSettingsAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        var setting = await GetSettingAsync(restaurantId, cancellationToken);
        if (_options.DisableSending)
        {
            return new WhatsAppCommerceSettingsResult(
                restaurantId,
                setting.IsEnabled && !string.IsNullOrWhiteSpace(setting.CatalogId),
                setting.IsCartEnabled,
                false,
                "Remote validation is disabled by MetaCatalog:DisableSending.");
        }

        using var request = CreateRequest(
            HttpMethod.Get,
            $"{BuildGraphBaseUrl()}/{Uri.EscapeDataString(setting.PhoneNumberId!)}/whatsapp_commerce_settings",
            setting.AccessTokenEncrypted!);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, "read commerce settings");

        using var document = JsonDocument.Parse(responseBody);
        var data = document.RootElement.TryGetProperty("data", out var dataElement) &&
                   dataElement.ValueKind == JsonValueKind.Array &&
                   dataElement.GetArrayLength() > 0
            ? dataElement[0]
            : default;
        var isVisible = data.ValueKind == JsonValueKind.Object &&
                        data.TryGetProperty("is_catalog_visible", out var visible) &&
                        visible.ValueKind is JsonValueKind.True;
        var isCartEnabled = data.ValueKind == JsonValueKind.Object &&
                            data.TryGetProperty("is_cart_enabled", out var cart) &&
                            cart.ValueKind is JsonValueKind.True;

        return new WhatsAppCommerceSettingsResult(
            restaurantId,
            isVisible,
            isCartEnabled,
            true);
    }

    public async Task<WhatsAppCommerceSettingsResult> UpdateCommerceSettingsAsync(
        Guid restaurantId,
        bool isCatalogVisible,
        bool isCartEnabled,
        CancellationToken cancellationToken = default)
    {
        var setting = await GetSettingAsync(restaurantId, cancellationToken);
        if (!_options.DisableSending)
        {
            var query = $"is_catalog_visible={isCatalogVisible.ToString().ToLowerInvariant()}&is_cart_enabled={isCartEnabled.ToString().ToLowerInvariant()}";
            using var request = CreateRequest(
                HttpMethod.Post,
                $"{BuildGraphBaseUrl()}/{Uri.EscapeDataString(setting.PhoneNumberId!)}/whatsapp_commerce_settings?{query}",
                setting.AccessTokenEncrypted!);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            EnsureSuccess(response, "update commerce settings");
        }

        var trackedSetting = await dbContext.MetaCatalogSettings
            .FirstAsync(x => x.BusinessId == restaurantId, cancellationToken);
        trackedSetting.IsCartEnabled = isCartEnabled;
        trackedSetting.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new WhatsAppCommerceSettingsResult(
            restaurantId,
            isCatalogVisible,
            isCartEnabled,
            !_options.DisableSending,
            _options.DisableSending ? "Remote update was simulated because sending is disabled." : null);
    }

    public async Task<WhatsAppCommerceDiagnostics> ValidateCatalogConnectionAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await dbContext.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == restaurantId, cancellationToken)
            ?? throw new KeyNotFoundException("Restaurant was not found.");
        var setting = await dbContext.MetaCatalogSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == restaurantId, cancellationToken);
        var issues = new List<string>();
        var configured = setting is { IsEnabled: true } &&
                         !string.IsNullOrWhiteSpace(setting.CatalogId) &&
                         !string.IsNullOrWhiteSpace(setting.PhoneNumberId) &&
                         !string.IsNullOrWhiteSpace(setting.AccessTokenEncrypted);
        if (!configured)
        {
            issues.Add("Catalog ID, phone number ID, and credential must be configured and enabled.");
        }

        var phoneMatched = configured && string.Equals(
            setting!.PhoneNumberId,
            restaurant.WhatsAppPhoneNumberId,
            StringComparison.Ordinal);
        if (!phoneMatched)
        {
            issues.Add("The catalog phone number ID does not match the webhook routing phone number ID.");
        }

        WhatsAppCommerceSettingsResult? commerce = null;
        if (configured && phoneMatched)
        {
            commerce = await GetCommerceSettingsAsync(restaurantId, cancellationToken);
            if (!commerce.IsCatalogVisible)
            {
                issues.Add("The WhatsApp catalog is not visible.");
            }

            if (!commerce.IsCartEnabled)
            {
                issues.Add("The WhatsApp cart is not enabled.");
            }

            if (!commerce.IsRemoteValidated)
            {
                issues.Add(commerce.Diagnostic ?? "Commerce settings were not validated against Meta.");
            }
        }

        return new WhatsAppCommerceDiagnostics(
            restaurantId,
            configured,
            phoneMatched,
            commerce?.IsCatalogVisible == true,
            commerce?.IsCartEnabled == true,
            configured && phoneMatched && commerce is { IsCatalogVisible: true, IsCartEnabled: true, IsRemoteValidated: true },
            issues);
    }

    private async Task<MetaCatalogSetting> GetSettingAsync(
        Guid restaurantId,
        CancellationToken cancellationToken)
    {
        var setting = await dbContext.MetaCatalogSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == restaurantId, cancellationToken);
        if (setting is not { IsEnabled: true } ||
            string.IsNullOrWhiteSpace(setting.PhoneNumberId) ||
            string.IsNullOrWhiteSpace(setting.AccessTokenEncrypted) ||
            string.IsNullOrWhiteSpace(setting.CatalogId))
        {
            throw new InvalidOperationException("An enabled Meta catalog connection is required.");
        }

        return setting;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string accessToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private string BuildGraphBaseUrl() => _options.GetVersionedGraphApiBaseUrl();

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Meta could not {operation} (HTTP {(int)response.StatusCode}).");
        }
    }
}
