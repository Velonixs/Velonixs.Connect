using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Security;

namespace Velonixs.Connect.Infrastructure.Tests;

internal static class MetaCatalogTestSupport
{
    public static RestaurantConnectDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RestaurantConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new RestaurantConnectDbContext(options, new PassThroughEncryptionService());
    }

    public static MetaCatalogSyncService CreateSyncService(
        RestaurantConnectDbContext dbContext,
        RecordingHttpMessageHandler? handler = null,
        MetaCatalogOptions? options = null) =>
        new(
            dbContext,
            new HttpClient(handler ?? new RecordingHttpMessageHandler()),
            Options.Create(options ?? new MetaCatalogOptions()),
            NullLogger<MetaCatalogSyncService>.Instance);

    public static async Task<(Restaurant Restaurant, MenuCategory Category, MenuItem Item)> SeedProductAsync(
        RestaurantConnectDbContext dbContext,
        string? retailerId = "pizza-001",
        bool isActive = true,
        bool isAvailable = true,
        string categoryName = "Pizza",
        string itemName = "Margherita",
        string? description = "Tomato and mozzarella",
        decimal price = 199.5m,
        string? imageUrl = "https://images.example.test/pizza.jpg")
    {
        var restaurant = new Restaurant
        {
            Name = "Catalog Test Bistro",
            WhatsAppPhoneNumberId = $"phone-{Guid.NewGuid():N}"
        };
        var category = new MenuCategory
        {
            Restaurant = restaurant,
            Name = categoryName,
            DisplayOrder = 1
        };
        var item = new MenuItem
        {
            Restaurant = restaurant,
            Category = category,
            ItemCode = 1,
            Name = itemName,
            Description = description,
            Price = price,
            ProductRetailerId = retailerId,
            ImageUrl = imageUrl,
            IsActive = isActive,
            IsAvailable = isAvailable
        };

        dbContext.MenuItems.Add(item);
        await dbContext.SaveChangesAsync();

        return (restaurant, category, item);
    }

    public static async Task<MetaCatalogSetting> EnableCatalogAsync(
        RestaurantConnectDbContext dbContext,
        Guid businessId,
        string syncMode = "default",
        string catalogId = "catalog-123",
        string accessToken = "test-access-token")
    {
        var setting = new MetaCatalogSetting
        {
            BusinessId = businessId,
            CatalogId = catalogId,
            AccessTokenEncrypted = accessToken,
            IsEnabled = true,
            SyncMode = syncMode
        };
        dbContext.MetaCatalogSettings.Add(setting);
        await dbContext.SaveChangesAsync();
        return setting;
    }

    public static Dictionary<string, string> ParseForm(string formBody)
    {
        return formBody
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
                pair => Uri.UnescapeDataString((pair.Length == 2 ? pair[1] : string.Empty).Replace('+', ' ')),
                StringComparer.Ordinal);
    }
}

internal sealed class PassThroughEncryptionService : IFieldEncryptionService
{
    public string Encrypt(string plaintext) => plaintext;
    public string Decrypt(string protectedValue) => protectedValue;
}

internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<CapturedHttpRequest> Requests { get; } = [];

    public void Respond(HttpStatusCode statusCode, string? responseBody = null)
    {
        _responses.Enqueue(() => new HttpResponseMessage(statusCode)
        {
            Content = responseBody is null ? null : new StringContent(responseBody, Encoding.UTF8, "application/json")
        });
    }

    public void Throw(Exception exception) => _responses.Enqueue(() => throw exception);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedHttpRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization?.Scheme,
            request.Headers.Authorization?.Parameter,
            body));

        return _responses.Count == 0
            ? new HttpResponseMessage(HttpStatusCode.OK)
            : _responses.Dequeue().Invoke();
    }
}

internal sealed record CapturedHttpRequest(
    HttpMethod Method,
    Uri Uri,
    string? AuthorizationScheme,
    string? AuthorizationParameter,
    string? Body);

internal sealed class RecordingMetaCatalogSyncService : IMetaCatalogSyncService
{
    public List<QueueCall> QueueCalls { get; } = [];
    public bool QueueResult { get; set; } = true;

    public Task<bool> QueueProductSyncAsync(
        Guid businessId,
        Guid productId,
        string eventType,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        QueueCalls.Add(new QueueCall(businessId, productId, eventType, force));
        return Task.FromResult(QueueResult);
    }

    public Task<int> QueueAllProductsAsync(Guid businessId, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task ProcessPendingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<MetaCatalogSettingsSummary?> GetSettingsAsync(Guid businessId, CancellationToken cancellationToken = default) =>
        Task.FromResult<MetaCatalogSettingsSummary?>(null);

    public Task SaveSettingsAsync(
        Guid businessId,
        MetaCatalogSettingsInput input,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyCollection<CatalogSyncQueueSummary>> GetQueueAsync(
        Guid businessId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<CatalogSyncQueueSummary>>(Array.Empty<CatalogSyncQueueSummary>());

    public Task<IReadOnlyCollection<CatalogSyncLogSummary>> GetLogsAsync(
        Guid businessId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<CatalogSyncLogSummary>>(Array.Empty<CatalogSyncLogSummary>());

    public Task<bool> RetryQueueItemAsync(Guid businessId, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

internal sealed record QueueCall(Guid BusinessId, Guid ProductId, string EventType, bool Force);
