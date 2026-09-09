using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class MetaCatalogService(
    RestaurantConnectDbContext dbContext,
    IMenuService menuService,
    IMetaCatalogSyncService syncService,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IOptions<Velonixs.Connect.Infrastructure.Configuration.MetaCatalogOptions> options) : IMetaCatalogService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IConfiguration _configuration = configuration;
    private readonly Velonixs.Connect.Infrastructure.Configuration.MetaCatalogOptions _options = options.Value;
    public async Task<MetaCatalogOverview?> GetCatalogAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await dbContext.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == restaurantId, cancellationToken);
        if (restaurant is null)
        {
            return null;
        }

        var setting = await dbContext.MetaCatalogSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == restaurantId, cancellationToken);
        var counts = await dbContext.MenuItems
            .AsNoTracking()
            .Where(x => x.RestaurantId == restaurantId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Synced = group.Count(x => x.SyncStatus == "Synced")
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new MetaCatalogOverview(
            restaurant.Id,
            restaurant.Name,
            setting?.CatalogId ?? restaurant.WhatsAppCatalogId,
            setting?.IsEnabled == true,
            setting?.IsCartEnabled == true,
            setting?.SyncMode ?? "default",
            counts?.Total ?? 0,
            counts?.Synced ?? 0,
            setting?.LastSuccessfulSyncAt);
    }

    public async Task<IReadOnlyCollection<MenuItemResponse>> GetCatalogProductsAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default) =>
        (await menuService.GetMenuAsync(restaurantId, cancellationToken))?.Items
        ?? Array.Empty<MenuItemResponse>();

    public async Task<bool> SyncProductAsync(
        Guid restaurantId,
        Guid menuItemId,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.MenuItems
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == menuItemId && x.RestaurantId == restaurantId,
                cancellationToken);

        return item is not null && await syncService.QueueProductSyncAsync(
            restaurantId,
            item.Id,
            item.IsActive ? "update" : "delete",
            force: true,
            cancellationToken: cancellationToken);
    }

    public Task<int> SyncRestaurantCatalogAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default) =>
        syncService.QueueAllProductsAsync(restaurantId, cancellationToken);

    public async Task<MetaCatalogSyncStatusResponse> GetCatalogSyncStatusAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.MenuItems
            .AsNoTracking()
            .Where(x => x.RestaurantId == restaurantId)
            .Select(x => x.SyncStatus)
            .ToArrayAsync(cancellationToken);
        var setting = await dbContext.MetaCatalogSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == restaurantId, cancellationToken);

        return new MetaCatalogSyncStatusResponse(
            restaurantId,
            items.Count(x => x is "Pending" or "Waiting" or "NotQueued"),
            items.Count(x => x == "Processing"),
            items.Count(x => x == "Synced"),
            items.Count(x => x is "Failed" or "PermanentFailure"),
            setting?.LastSuccessfulSyncAt);
    }

    public async Task<string?> CreateCatalogAsync(Guid restaurantId, string name, CancellationToken cancellationToken = default)
    {
        if (restaurantId == Guid.Empty)
        {
            throw new ArgumentException("Restaurant is required.", nameof(restaurantId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Catalog name is required.", nameof(name));
        }

        if (_options.DisableSending)
        {
            throw new InvalidOperationException(
                "Catalog creation is disabled. Set MetaCatalog:DisableSending to false on the API host before creating a live Meta catalog.");
        }

        var restaurant = await dbContext.Restaurants
            .FirstOrDefaultAsync(x => x.Id == restaurantId, cancellationToken)
            ?? throw new KeyNotFoundException("Restaurant was not found.");
        var setting = await dbContext.MetaCatalogSettings
            .FirstOrDefaultAsync(x => x.BusinessId == restaurantId, cancellationToken);

        var metaBusinessId = setting?.MetaBusinessId ?? _configuration["META_CATALOG_BUSINESS_ID"];
        var wabaId = setting?.WabaId;
        var accessToken = setting?.AccessTokenEncrypted ?? _configuration["META_CATALOG_ACCESS_TOKEN"];

        if (string.IsNullOrWhiteSpace(metaBusinessId) ||
            string.IsNullOrWhiteSpace(wabaId) ||
            string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(
                "Save the Meta business ID, WABA ID, and access token before creating a catalog.");
        }

        if (!string.IsNullOrWhiteSpace(setting?.CatalogId))
        {
            throw new InvalidOperationException(
                "This restaurant already has a Meta catalog. Use the existing catalog or configure a different restaurant connection.");
        }

        var baseUrl = _options.GetVersionedGraphApiBaseUrl().TrimEnd('/');
        var client = _httpClientFactory.CreateClient();
        var catalogId = await CreateRemoteCatalogAsync(
            client,
            baseUrl,
            metaBusinessId,
            accessToken,
            name.Trim(),
            cancellationToken);
        await AttachCatalogToWhatsAppBusinessAccountAsync(
            client,
            baseUrl,
            wabaId,
            catalogId,
            accessToken,
            cancellationToken);

        if (setting is not null)
        {
            setting.CatalogId = catalogId;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            // A platform-level secret can be used for controlled automation, but
            // a tenant record is still required to retain the resulting catalog
            // and associate it with the restaurant's WhatsApp number later.
            setting = new Domain.Entities.MetaCatalogSetting
            {
                BusinessId = restaurantId,
                MetaBusinessId = metaBusinessId,
                WabaId = wabaId,
                CatalogId = catalogId,
                AccessTokenEncrypted = accessToken,
                IsEnabled = false,
                IsCartEnabled = true,
                SyncMode = "default"
            };
            dbContext.MetaCatalogSettings.Add(setting);
        }

        restaurant.WhatsAppCatalogId = catalogId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return catalogId;
    }

    private static async Task<string> CreateRemoteCatalogAsync(
        HttpClient client,
        string baseUrl,
        string metaBusinessId,
        string accessToken,
        string name,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl}/{Uri.EscapeDataString(metaBusinessId)}/owned_product_catalogs")
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("name", name),
                new KeyValuePair<string, string>("vertical", "commerce")
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Meta could not create the catalog: {responseText}");
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.TryGetProperty("id", out var id) &&
                id.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(id.GetString()))
            {
                return id.GetString()!;
            }
        }
        catch (JsonException)
        {
            // The caller receives a clear operation-specific error below.
        }

        throw new InvalidOperationException("Meta created the catalog but did not return a catalog ID.");
    }

    private static async Task AttachCatalogToWhatsAppBusinessAccountAsync(
        HttpClient client,
        string baseUrl,
        string wabaId,
        string catalogId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl}/{Uri.EscapeDataString(wabaId)}/product_catalogs")
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("catalog_id", catalogId)
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Meta could not attach the catalog to the WhatsApp business account: {responseText}");
        }
    }
}
