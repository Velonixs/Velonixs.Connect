using System.Net;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Configuration;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class MetaCatalogSyncServiceBehaviorTests
{
    [Fact]
    public async Task QueueProductSyncAsync_RequiresAnEnabledConfiguredNonManualConnection()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (_, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        var service = MetaCatalogTestSupport.CreateSyncService(dbContext);

        item.SyncStatus = "Synced";
        await dbContext.SaveChangesAsync();
        Assert.False(await service.QueueProductSyncAsync(item.RestaurantId, item.Id, "create"));
        Assert.Equal("NotQueued", (await dbContext.MenuItems.AsNoTracking().SingleAsync()).SyncStatus);

        var setting = new MetaCatalogSetting
        {
            BusinessId = item.RestaurantId,
            CatalogId = "catalog-123",
            AccessTokenEncrypted = "token",
            IsEnabled = false
        };
        dbContext.MetaCatalogSettings.Add(setting);
        await dbContext.SaveChangesAsync();

        Assert.False(await service.QueueProductSyncAsync(item.RestaurantId, item.Id, "create"));

        setting.IsEnabled = true;
        setting.SyncMode = "manual";
        await dbContext.SaveChangesAsync();

        Assert.False(await service.QueueProductSyncAsync(item.RestaurantId, item.Id, "create"));
        Assert.True(await service.QueueProductSyncAsync(item.RestaurantId, item.Id, "create", force: true));
        Assert.Single(await dbContext.CatalogSyncQueue.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task QueueProductSyncAsync_GeneratesSkuAndCoalescesAutomaticChangesWithoutAnAuthorizationContext()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext, retailerId: null);
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id, syncMode: "automatic");
        var service = MetaCatalogTestSupport.CreateSyncService(dbContext);

        // The application service has no HTTP/user authorization dependency: a
        // configured business can use it directly from a worker or menu service.
        Assert.True(await service.QueueProductSyncAsync(restaurant.Id, item.Id, " create "));
        Assert.True(await service.QueueProductSyncAsync(restaurant.Id, item.Id, "update"));
        Assert.True(await service.QueueProductSyncAsync(restaurant.Id, item.Id, "delete"));

        var queued = Assert.Single(await dbContext.CatalogSyncQueue.AsNoTracking().ToArrayAsync());
        var product = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        var expectedSku = $"vxc-{restaurant.Id:N}-{item.Id:N}";

        Assert.Equal(expectedSku, product.ProductRetailerId);
        Assert.Equal(expectedSku, queued.ProductRetailerId);
        Assert.Equal("delete", queued.EventType);
        Assert.Equal("Pending", queued.Status);
        Assert.Equal("Pending", product.SyncStatus);
        Assert.Contains("\"eventType\":\"delete\"", queued.PayloadJson);
    }

    [Fact]
    public async Task QueueAllProductsAsync_ForcesManualModeAndReconcilesInactiveProducts()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, category, activeItem) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        var inactiveItem = new MenuItem
        {
            RestaurantId = restaurant.Id,
            CategoryId = category.Id,
            ItemCode = 2,
            Name = "Retired pizza",
            Price = 110,
            ProductRetailerId = "retired-002",
            IsActive = false,
            IsAvailable = false
        };
        dbContext.MenuItems.Add(inactiveItem);
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id, syncMode: "manual");
        await dbContext.SaveChangesAsync();
        var service = MetaCatalogTestSupport.CreateSyncService(dbContext);

        var queuedCount = await service.QueueAllProductsAsync(restaurant.Id);

        Assert.Equal(2, queuedCount);
        var queued = await dbContext.CatalogSyncQueue.AsNoTracking().ToDictionaryAsync(item => item.ProductId);
        Assert.Equal("update", queued[activeItem.Id].EventType);
        Assert.Equal("delete", queued[inactiveItem.Id].EventType);
    }

    [Fact]
    public async Task SaveSettingsAsync_ValidatesConnectionAndMirrorsCatalogIdToRestaurant()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "Settings Bistro",
            WhatsAppPhoneNumberId = "settings-phone"
        };
        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync();
        var service = MetaCatalogTestSupport.CreateSyncService(dbContext);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveSettingsAsync(
            Guid.Empty,
            new MetaCatalogSettingsInput(null, null, null, null, null)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SaveSettingsAsync(
            Guid.NewGuid(),
            new MetaCatalogSettingsInput(null, null, null, null, null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveSettingsAsync(
            restaurant.Id,
            new MetaCatalogSettingsInput(null, "catalog", null, null, null, true)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveSettingsAsync(
            restaurant.Id,
            new MetaCatalogSettingsInput(null, "catalog", "different-phone", "token", null, true)));

        await service.SaveSettingsAsync(
            restaurant.Id,
            new MetaCatalogSettingsInput(
                " waba-1 ",
                " catalog-a ",
                " settings-phone ",
                " token-a ",
                " verify-a ",
                IsEnabled: true,
                SyncMode: " AUTOMATIC "));

        var setting = await dbContext.MetaCatalogSettings.AsNoTracking().SingleAsync();
        var summary = await service.GetSettingsAsync(restaurant.Id);

        Assert.Equal("waba-1", setting.WabaId);
        Assert.Equal("catalog-a", setting.CatalogId);
        Assert.Equal("settings-phone", setting.PhoneNumberId);
        Assert.Equal("token-a", setting.AccessTokenEncrypted);
        Assert.Equal("automatic", setting.SyncMode);
        Assert.Equal("catalog-a", restaurant.WhatsAppCatalogId);
        Assert.NotNull(summary);
        Assert.Equal("catalog-a", summary!.CatalogId);
        Assert.Equal("automatic", summary.SyncMode);

        await service.SaveSettingsAsync(
            restaurant.Id,
            new MetaCatalogSettingsInput(null, "catalog-b", null, null, null, true, "manual"));

        setting = await dbContext.MetaCatalogSettings.AsNoTracking().SingleAsync();
        Assert.Equal("token-a", setting.AccessTokenEncrypted);
        Assert.Equal("verify-a", setting.WebhookVerifyTokenEncrypted);
        Assert.Equal("catalog-b", restaurant.WhatsAppCatalogId);
        Assert.Equal("manual", setting.SyncMode);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveSettingsAsync(
            restaurant.Id,
            new MetaCatalogSettingsInput(null, "catalog-b", null, null, null, true, "unsupported")));
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsNullWhenNoConnectionIsConfigured()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var service = MetaCatalogTestSupport.CreateSyncService(dbContext);

        Assert.Null(await service.GetSettingsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ProcessPendingAsync_SendsExpectedMetaFormAndCompletesTheProduct()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(
            dbContext,
            retailerId: "pizza-001",
            description: "Fresh tomato & mozzarella",
            price: 199.5m);
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        var handler = new RecordingHttpMessageHandler();
        handler.Respond(HttpStatusCode.Created, "{\"id\":\"meta-item-42\"}");
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions
            {
                DisableSending = false,
                GraphApiBaseUrl = "https://graph.example.test/v20.0/",
                Currency = "INR"
            });

        Assert.True(await service.QueueProductSyncAsync(restaurant.Id, item.Id, "create"));
        await service.ProcessPendingAsync();

        var request = Assert.Single(handler.Requests);
        var form = MetaCatalogTestSupport.ParseForm(Assert.IsType<string>(request.Body));
        var queueItem = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        var product = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        var log = await dbContext.CatalogSyncLogs.AsNoTracking().SingleAsync();

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://graph.example.test/v20.0/catalog-123/products", request.Uri.AbsoluteUri);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("test-access-token", request.AuthorizationParameter);
        Assert.Equal("pizza-001", form["retailer_id"]);
        Assert.Equal("Margherita", form["name"]);
        Assert.Equal("Fresh tomato & mozzarella", form["description"]);
        Assert.Equal("in stock", form["availability"]);
        Assert.Equal("new", form["condition"]);
        Assert.Equal("19950", form["price"]);
        Assert.Equal("INR", form["currency"]);
        Assert.Equal("true", form["allow_upsert"]);
        Assert.Equal("Pizza", form["product_type"]);
        Assert.Equal("https://images.example.test/pizza.jpg", form["image_url"]);
        Assert.Equal("Synced", queueItem.Status);
        Assert.Equal("Synced", product.SyncStatus);
        Assert.Equal("meta-item-42", product.MetaProductId);
        Assert.Equal("Synced", log.Status);
        Assert.Equal(201, log.ResponseCode);
    }

    [Fact]
    public async Task ProcessPendingAsync_SimulatesDeliveryWhenSendingIsDisabled()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        var handler = new RecordingHttpMessageHandler();
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = true });

        await service.QueueProductSyncAsync(restaurant.Id, item.Id, "update");
        await service.ProcessPendingAsync();

        var queueItem = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        var product = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        var log = await dbContext.CatalogSyncLogs.AsNoTracking().SingleAsync();

        Assert.Empty(handler.Requests);
        Assert.Equal("Simulated", queueItem.Status);
        Assert.Equal(0, queueItem.RetryCount);
        Assert.Null(queueItem.NextAttemptAt);
        Assert.Equal("Simulated", product.SyncStatus);
        Assert.Equal("Simulated", log.Status);
        Assert.Equal(0, log.ResponseCode);
    }

    [Fact]
    public async Task ProcessPendingAsync_RecoversUnqueuedProductsForAnAutomaticConnection()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        var setting = await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id, syncMode: "automatic");
        item.SyncStatus = "NotQueued";
        setting.ReconciliationRequired = true;
        await dbContext.SaveChangesAsync();

        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            options: new MetaCatalogOptions { DisableSending = true });

        await service.ProcessPendingAsync();

        var queueItem = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        var product = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        Assert.Equal("catalog-123", queueItem.CatalogId);
        Assert.Equal("Simulated", queueItem.Status);
        Assert.Equal("Simulated", product.SyncStatus);
        Assert.False((await dbContext.MetaCatalogSettings.AsNoTracking().SingleAsync()).ReconciliationRequired);
    }

    [Fact]
    public async Task ProcessPendingAsync_ResumesPausedWorkAfterAnInterruptedReenable()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        var setting = await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        item.SyncStatus = "Paused";
        setting.ReconciliationRequired = true;
        dbContext.CatalogSyncQueue.Add(new CatalogSyncQueueItem
        {
            BusinessId = restaurant.Id,
            ProductId = item.Id,
            CatalogId = setting.CatalogId,
            ProductRetailerId = item.ProductRetailerId!,
            EventType = "update",
            Status = "Paused",
            PayloadJson = "{}"
        });
        await dbContext.SaveChangesAsync();

        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            options: new MetaCatalogOptions { DisableSending = true });

        await service.ProcessPendingAsync();

        var queueItem = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        var product = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        Assert.Equal("Simulated", queueItem.Status);
        Assert.Equal("Simulated", product.SyncStatus);
        Assert.False((await dbContext.MetaCatalogSettings.AsNoTracking().SingleAsync()).ReconciliationRequired);
    }

    [Fact]
    public async Task ProcessPendingAsync_RecoveryDoesNotResetAnUnrelatedFailedItem()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, category, recoveredItem) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        var failedItem = new MenuItem
        {
            RestaurantId = restaurant.Id,
            CategoryId = category.Id,
            ItemCode = 2,
            Name = "Failed pizza",
            Price = 100,
            ProductRetailerId = "failed-pizza",
            ImageUrl = "https://images.example.test/failed-pizza.jpg",
            IsActive = true,
            IsAvailable = true,
            SyncStatus = "Failed"
        };
        dbContext.MenuItems.Add(failedItem);
        var setting = await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id, syncMode: "automatic");
        setting.ReconciliationRequired = true;
        dbContext.CatalogSyncQueue.Add(new CatalogSyncQueueItem
        {
            BusinessId = restaurant.Id,
            ProductId = failedItem.Id,
            CatalogId = setting.CatalogId,
            ProductRetailerId = failedItem.ProductRetailerId!,
            EventType = "update",
            Status = "Failed",
            RetryCount = 5,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            PayloadJson = "{}"
        });
        await dbContext.SaveChangesAsync();

        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            options: new MetaCatalogOptions { DisableSending = true, MaxRetryCount = 5, BatchSize = 1 });

        await service.ProcessPendingAsync();

        var failedQueueItem = await dbContext.CatalogSyncQueue.AsNoTracking()
            .SingleAsync(queueItem => queueItem.ProductId == failedItem.Id);
        Assert.Equal("Failed", failedQueueItem.Status);
        Assert.Equal(5, failedQueueItem.RetryCount);
        Assert.Equal("Simulated", (await dbContext.MenuItems.AsNoTracking().SingleAsync(menuItem => menuItem.Id == recoveredItem.Id)).SyncStatus);
    }

    [Fact]
    public async Task SaveSettingsAsync_ReconcilesMarkedProductsWhenManualModeBecomesAutomatic()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id, syncMode: "manual");
        item.SyncStatus = "Synced";
        await dbContext.SaveChangesAsync();
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            options: new MetaCatalogOptions { DisableSending = true });

        Assert.False(await service.QueueProductSyncAsync(restaurant.Id, item.Id, "update"));
        Assert.Equal("NotQueued", (await dbContext.MenuItems.AsNoTracking().SingleAsync()).SyncStatus);

        await service.SaveSettingsAsync(
            restaurant.Id,
            new MetaCatalogSettingsInput(null, "catalog-123", null, null, null, IsEnabled: true, SyncMode: "automatic"));

        var queueItem = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        var setting = await dbContext.MetaCatalogSettings.AsNoTracking().SingleAsync();
        Assert.Equal("Pending", queueItem.Status);
        Assert.Equal("automatic", setting.SyncMode);
        Assert.False(setting.ReconciliationRequired);
    }

    [Fact]
    public async Task SaveSettingsAsync_CancelsAutomaticWorkWhenSwitchingToManualMode()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id, syncMode: "automatic");
        var service = MetaCatalogTestSupport.CreateSyncService(dbContext);
        Assert.True(await service.QueueProductSyncAsync(restaurant.Id, item.Id, "update"));

        await service.SaveSettingsAsync(
            restaurant.Id,
            new MetaCatalogSettingsInput(null, "catalog-123", null, null, null, IsEnabled: true, SyncMode: "manual"));

        var queueItem = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        var product = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        Assert.Equal("Cancelled", queueItem.Status);
        Assert.Contains("manual mode", queueItem.LastError);
        Assert.Equal("NotQueued", product.SyncStatus);
    }

    [Fact]
    public async Task QueueProductSyncAsync_CancelsAnExplicitManualSnapshotAfterALocalEdit()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id, syncMode: "manual");
        var handler = new RecordingHttpMessageHandler();
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false });

        Assert.True(await service.QueueProductSyncAsync(restaurant.Id, item.Id, "update", force: true));
        item.Name = "Manually edited pizza";
        await dbContext.SaveChangesAsync();

        Assert.False(await service.QueueProductSyncAsync(restaurant.Id, item.Id, "update"));
        await service.ProcessPendingAsync();

        var queueItem = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        var product = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        Assert.Equal("Cancelled", queueItem.Status);
        Assert.Contains("local product change", queueItem.LastError);
        Assert.Equal("NotQueued", product.SyncStatus);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ProcessPendingAsync_ReconcilesCurrentSnapshotBeforeReplayingSimulatedWork()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext, itemName: "Current pizza");
        var setting = await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        setting.ReconciliationRequired = true;
        item.SyncStatus = "NotQueued";
        dbContext.CatalogSyncQueue.Add(new CatalogSyncQueueItem
        {
            BusinessId = restaurant.Id,
            ProductId = item.Id,
            CatalogId = setting.CatalogId,
            ProductRetailerId = item.ProductRetailerId!,
            EventType = "update",
            Status = "Simulated",
            PayloadJson = "{\"name\":\"Stale pizza\"}"
        });
        await dbContext.SaveChangesAsync();
        var handler = new RecordingHttpMessageHandler();
        handler.Respond(HttpStatusCode.OK, "{\"id\":\"meta-current\"}");
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false });

        await service.ProcessPendingAsync();

        var request = Assert.Single(handler.Requests);
        var form = MetaCatalogTestSupport.ParseForm(request.Body!);
        var queueItem = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        Assert.Equal("Current pizza", form["name"]);
        Assert.Equal("Synced", queueItem.Status);
    }

    [Fact]
    public async Task ProcessPendingAsync_ReplaysSimulatedWorkWhenLiveSendingIsEnabled()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);

        var dryRunService = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            options: new MetaCatalogOptions { DisableSending = true });
        await dryRunService.QueueProductSyncAsync(restaurant.Id, item.Id, "update");
        await dryRunService.ProcessPendingAsync();

        var handler = new RecordingHttpMessageHandler();
        handler.Respond(HttpStatusCode.OK, "{\"id\":\"meta-item-live\"}");
        var liveService = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false });

        await liveService.ProcessPendingAsync();

        var queueItem = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        var product = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        Assert.Single(handler.Requests);
        Assert.Equal("Synced", queueItem.Status);
        Assert.Equal("Synced", product.SyncStatus);
        Assert.Equal("meta-item-live", product.MetaProductId);
        Assert.Equal(2, await dbContext.CatalogSyncLogs.CountAsync());
    }

    [Fact]
    public async Task SaveSettingsAsync_ResumesPausedWorkWhenTheSameConnectionIsReenabled()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            options: new MetaCatalogOptions { DisableSending = false });

        await service.QueueProductSyncAsync(restaurant.Id, item.Id, "update");
        await service.SaveSettingsAsync(
            restaurant.Id,
            new MetaCatalogSettingsInput(null, "catalog-123", null, null, null, IsEnabled: false));

        var paused = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        Assert.Equal("Paused", paused.Status);

        await service.SaveSettingsAsync(
            restaurant.Id,
            new MetaCatalogSettingsInput(null, "catalog-123", null, null, null, IsEnabled: true));

        var resumed = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        var product = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        Assert.Equal("Pending", resumed.Status);
        Assert.Equal("Pending", product.SyncStatus);
    }

    [Fact]
    public async Task ProcessPendingAsync_RecordsHttpFailureAndAllowsAnExplicitRetry()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        var handler = new RecordingHttpMessageHandler();
        handler.Respond(HttpStatusCode.BadGateway, "gateway unavailable");
        handler.Respond(HttpStatusCode.OK, "{\"product_id\":42}");
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false });

        await service.QueueProductSyncAsync(restaurant.Id, item.Id, "update");
        await service.ProcessPendingAsync();

        var failed = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        var failedProduct = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        var failureLog = await dbContext.CatalogSyncLogs.AsNoTracking().SingleAsync();
        Assert.Equal("Failed", failed.Status);
        Assert.Equal(1, failed.RetryCount);
        Assert.Equal("Meta catalog request failed with HTTP 502.", failed.LastError);
        Assert.True(failed.NextAttemptAt > DateTimeOffset.UtcNow);
        Assert.Equal("Failed", failedProduct.SyncStatus);
        Assert.Equal("Failed", failureLog.Status);
        Assert.Equal(502, failureLog.ResponseCode);
        Assert.False(await service.RetryQueueItemAsync(Guid.NewGuid(), failed.Id));

        Assert.True(await service.RetryQueueItemAsync(restaurant.Id, failed.Id));
        var pending = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        Assert.Equal("Pending", pending.Status);
        Assert.Equal(0, pending.RetryCount);
        Assert.Null(pending.LastError);

        await service.ProcessPendingAsync();

        var completed = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        var completedProduct = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        Assert.Equal("Synced", completed.Status);
        Assert.Equal("42", completedProduct.MetaProductId);
        Assert.Equal(2, await dbContext.CatalogSyncLogs.CountAsync());
    }

    [Fact]
    public async Task ProcessPendingAsync_ExposesMetaValidationMessageForBadRequests()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        var handler = new RecordingHttpMessageHandler();
        handler.Respond(
            HttpStatusCode.BadRequest,
            "{\"error\":{\"message\":\"The catalog ID is invalid for this WhatsApp business account.\"}}");
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false });

        await service.QueueProductSyncAsync(restaurant.Id, item.Id, "update");
        await service.ProcessPendingAsync();

        var failed = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        Assert.Equal(
            "Meta catalog request failed with HTTP 400: The catalog ID is invalid for this WhatsApp business account.",
            failed.LastError);
    }

    [Fact]
    public async Task ProcessPendingAsync_UsesDeleteEndpointWithoutAFormBody()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext, retailerId: "delete-sku");
        item.MetaProductId = "meta-item-delete";
        await dbContext.SaveChangesAsync();
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        var handler = new RecordingHttpMessageHandler();
        handler.Respond(HttpStatusCode.OK, "{}");
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false, GraphApiBaseUrl = "https://graph.example.test/v20.0" });

        await service.QueueProductSyncAsync(restaurant.Id, item.Id, "delete");
        await service.ProcessPendingAsync();

        var request = Assert.Single(handler.Requests);
        var product = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/v20.0/meta-item-delete", request.Uri.AbsolutePath);
        Assert.Equal(string.Empty, request.Uri.Query);
        Assert.Null(request.Body);
        Assert.Null(product.MetaProductId);
    }

    [Fact]
    public async Task ProcessPendingAsync_TreatsMetaDeleteNotFoundAsIdempotentSuccess()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext, retailerId: "gone-sku");
        item.MetaProductId = "already-gone";
        await dbContext.SaveChangesAsync();
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        var handler = new RecordingHttpMessageHandler();
        handler.Respond(HttpStatusCode.NotFound, "not found");
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false });

        await service.QueueProductSyncAsync(restaurant.Id, item.Id, "delete");
        await service.ProcessPendingAsync();

        var queueItem = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        var product = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        Assert.Single(handler.Requests);
        Assert.Equal("Synced", queueItem.Status);
        Assert.Null(product.MetaProductId);
    }

    [Fact]
    public async Task ProcessPendingAsync_TreatsDeleteOfNeverSyncedProductAsAnIdempotentNoOp()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext, retailerId: "never-synced");
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        var handler = new RecordingHttpMessageHandler();
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false });

        await service.QueueProductSyncAsync(restaurant.Id, item.Id, "delete");
        await service.ProcessPendingAsync();

        var queueItem = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        var log = await dbContext.CatalogSyncLogs.AsNoTracking().SingleAsync();
        Assert.Empty(handler.Requests);
        Assert.Equal("Synced", queueItem.Status);
        Assert.Equal(204, log.ResponseCode);
    }

    [Fact]
    public async Task ProcessPendingAsync_DeliversAQueuedDeleteOnlyAfterItsInFlightCreate()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext, retailerId: "ordered-sku");
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        var handler = new RecordingHttpMessageHandler();
        handler.Respond(HttpStatusCode.Created, "{\"id\":\"meta-ordered\"}");
        handler.Respond(HttpStatusCode.OK, "{}");
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false, BatchSize = 1 });

        await service.QueueProductSyncAsync(restaurant.Id, item.Id, "create");
        var predecessor = await dbContext.CatalogSyncQueue.SingleAsync();
        predecessor.Status = "Processing";
        predecessor.LeaseId = Guid.NewGuid();
        predecessor.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        item.IsActive = false;
        await dbContext.SaveChangesAsync();

        await service.QueueProductSyncAsync(restaurant.Id, item.Id, "delete");
        var successor = await dbContext.CatalogSyncQueue
            .SingleAsync(queueItem => queueItem.Id != predecessor.Id);
        Assert.Equal(predecessor.Id, successor.PredecessorQueueItemId);

        await service.ProcessPendingAsync();
        Assert.Empty(handler.Requests);

        predecessor.Status = "Pending";
        predecessor.LeaseId = null;
        predecessor.LeaseExpiresAt = null;
        predecessor.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await dbContext.SaveChangesAsync();

        await service.ProcessPendingAsync();
        var productAwaitingDelete = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        Assert.Equal("Pending", productAwaitingDelete.SyncStatus);
        await service.ProcessPendingAsync();

        var requests = handler.Requests;
        var product = await dbContext.MenuItems.AsNoTracking().SingleAsync();
        Assert.Equal(2, requests.Count);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.Equal(HttpMethod.Delete, requests[1].Method);
        Assert.Null(product.MetaProductId);
        Assert.All(await dbContext.CatalogSyncQueue.AsNoTracking().ToArrayAsync(), queueItem =>
            Assert.Equal("Synced", queueItem.Status));
    }

    [Fact]
    public async Task ProcessPendingAsync_FailsDeliveryWithoutAPublicHttpsImage()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(
            dbContext,
            imageUrl: "http://localhost/catalog-item.jpg");
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        var handler = new RecordingHttpMessageHandler();
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false });

        await service.QueueProductSyncAsync(restaurant.Id, item.Id, "create");
        await service.ProcessPendingAsync();

        var queueItem = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        Assert.Empty(handler.Requests);
        Assert.Equal("Failed", queueItem.Status);
        Assert.Contains("publicly accessible HTTPS image", queueItem.LastError);
    }

    [Fact]
    public async Task ProcessPendingAsync_UsesItsQueuedSnapshotAfterTheProductIsRemoved()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext, retailerId: "removed-sku");
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        var handler = new RecordingHttpMessageHandler();
        handler.Respond(HttpStatusCode.OK, "{\"retailer_id\":\"removed-sku\"}");
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false });

        await service.QueueProductSyncAsync(restaurant.Id, item.Id, "update");
        dbContext.MenuItems.Remove(item);
        await dbContext.SaveChangesAsync();

        await service.ProcessPendingAsync();

        var queueSummary = Assert.Single(await service.GetQueueAsync(restaurant.Id));
        var logSummary = Assert.Single(await service.GetLogsAsync(restaurant.Id));
        Assert.Single(handler.Requests);
        Assert.Equal("Synced", queueSummary.Status);
        Assert.Equal("Deleted product", queueSummary.ProductName);
        Assert.Equal("Synced", logSummary.Status);
        Assert.Equal("Deleted product", logSummary.ProductName);
    }

    [Fact]
    public async Task ProcessPendingAsync_FailsCorruptSnapshotsAndExpiredLeasesAreReclaimed()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        var corrupt = new CatalogSyncQueueItem
        {
            BusinessId = restaurant.Id,
            ProductId = Guid.NewGuid(),
            CatalogId = "catalog-123",
            ProductRetailerId = "corrupt-sku",
            EventType = "update",
            PayloadJson = "not-json",
            Status = "Pending",
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var expiredLease = new CatalogSyncQueueItem
        {
            BusinessId = restaurant.Id,
            ProductId = item.Id,
            CatalogId = "catalog-123",
            ProductRetailerId = item.ProductRetailerId!,
            EventType = "update",
            Status = "Processing",
            RetryCount = 0,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            LeaseId = Guid.NewGuid(),
            LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        dbContext.CatalogSyncQueue.AddRange(corrupt, expiredLease);
        await dbContext.SaveChangesAsync();
        var handler = new RecordingHttpMessageHandler();
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false });

        await service.ProcessPendingAsync();

        var items = await dbContext.CatalogSyncQueue.AsNoTracking().ToDictionaryAsync(x => x.Id);
        Assert.Equal("Failed", items[corrupt.Id].Status);
        Assert.Contains("snapshot is unavailable", items[corrupt.Id].LastError);
        Assert.Equal("Synced", items[expiredLease.Id].Status);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ProcessPendingAsync_PausesAQueuedItemWhenTheConnectionIsRemoved()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        var queueItem = new CatalogSyncQueueItem
        {
            BusinessId = restaurant.Id,
            ProductId = item.Id,
            ProductRetailerId = item.ProductRetailerId!,
            EventType = "update",
            Status = "Pending",
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        dbContext.CatalogSyncQueue.Add(queueItem);
        await dbContext.SaveChangesAsync();
        var handler = new RecordingHttpMessageHandler();
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false });

        await service.ProcessPendingAsync();

        var paused = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        Assert.Equal("Paused", paused.Status);
        Assert.Contains("disabled or incomplete", paused.LastError);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ProcessPendingAsync_CancelsWorkCapturedForASupersededCatalog()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id, catalogId: "new-catalog");
        dbContext.CatalogSyncQueue.Add(new CatalogSyncQueueItem
        {
            BusinessId = restaurant.Id,
            ProductId = item.Id,
            CatalogId = "old-catalog",
            ProductRetailerId = item.ProductRetailerId!,
            EventType = "update",
            Status = "Pending",
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        await dbContext.SaveChangesAsync();
        var handler = new RecordingHttpMessageHandler();
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false });

        await service.ProcessPendingAsync();

        var queueItem = await dbContext.CatalogSyncQueue.AsNoTracking().SingleAsync();
        Assert.Equal("Cancelled", queueItem.Status);
        Assert.Contains("connection changed", queueItem.LastError);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ProcessPendingAsync_CancelsAStaleEventBeforeRecoveringItsReplacement()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(dbContext);
        var setting = await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id, catalogId: "new-catalog");
        setting.ReconciliationRequired = true;
        item.SyncStatus = "NotQueued";
        dbContext.CatalogSyncQueue.Add(new CatalogSyncQueueItem
        {
            BusinessId = restaurant.Id,
            ProductId = item.Id,
            CatalogId = "old-catalog",
            ProductRetailerId = item.ProductRetailerId!,
            EventType = "update",
            Status = "Pending",
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        await dbContext.SaveChangesAsync();
        var handler = new RecordingHttpMessageHandler();
        var service = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = true });

        await service.ProcessPendingAsync();

        var queueItems = await dbContext.CatalogSyncQueue.AsNoTracking().ToArrayAsync();
        Assert.Contains(queueItems, queueItem => queueItem.CatalogId == "old-catalog" && queueItem.Status == "Cancelled");
        Assert.Contains(queueItems, queueItem => queueItem.CatalogId == "new-catalog" && queueItem.Status == "Simulated");
        Assert.Empty(handler.Requests);
    }
}
