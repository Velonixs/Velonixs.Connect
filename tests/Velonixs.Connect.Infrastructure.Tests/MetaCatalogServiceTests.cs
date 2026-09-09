using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Infrastructure.Services;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class MetaCatalogServiceTests
{
    [Fact]
    public async Task CreateCatalogAsync_CreatesAttachesAndStoresTheCatalogWithoutManualIdCopying()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "Admin Managed Bistro",
            WhatsAppPhoneNumberId = "phone-123"
        };
        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync();

        dbContext.MetaCatalogSettings.Add(new MetaCatalogSetting
        {
            BusinessId = restaurant.Id,
            MetaBusinessId = "meta-business-123",
            WabaId = "waba-123",
            PhoneNumberId = "phone-123",
            AccessTokenEncrypted = "tenant-access-token",
            IsEnabled = false
        });
        await dbContext.SaveChangesAsync();

        var handler = new RecordingHttpMessageHandler();
        handler.Respond(HttpStatusCode.Created, "{\"id\":\"catalog-987\"}");
        handler.Respond(HttpStatusCode.OK, "{\"success\":true}");
        var service = new MetaCatalogService(
            dbContext,
            null!,
            MetaCatalogTestSupport.CreateSyncService(dbContext),
            new FixedHttpClientFactory(new HttpClient(handler)),
            new ConfigurationBuilder().Build(),
            Options.Create(new MetaCatalogOptions
            {
                GraphApiBaseUrl = "https://graph.example.test",
                GraphApiVersion = "v26.0",
                DisableSending = false
            }));

        var catalogId = await service.CreateCatalogAsync(restaurant.Id, "  Bistro WhatsApp menu  ");

        Assert.Equal("catalog-987", catalogId);
        Assert.Collection(
            handler.Requests,
            create =>
            {
                Assert.Equal(HttpMethod.Post, create.Method);
                Assert.Equal("https://graph.example.test/v26.0/meta-business-123/owned_product_catalogs", create.Uri.AbsoluteUri);
                Assert.Equal("Bearer", create.AuthorizationScheme);
                Assert.Equal("tenant-access-token", create.AuthorizationParameter);
                Assert.Equal(
                    new Dictionary<string, string>
                    {
                        ["name"] = "Bistro WhatsApp menu",
                        ["vertical"] = "commerce"
                    },
                    MetaCatalogTestSupport.ParseForm(Assert.IsType<string>(create.Body)));
            },
            attach =>
            {
                Assert.Equal(HttpMethod.Post, attach.Method);
                Assert.Equal("https://graph.example.test/v26.0/waba-123/product_catalogs", attach.Uri.AbsoluteUri);
                Assert.Equal("Bearer", attach.AuthorizationScheme);
                Assert.Equal("tenant-access-token", attach.AuthorizationParameter);
                Assert.Equal(
                    "catalog-987",
                    MetaCatalogTestSupport.ParseForm(Assert.IsType<string>(attach.Body))["catalog_id"]);
            });

        var storedRestaurant = await dbContext.Restaurants.AsNoTracking().SingleAsync();
        var storedSettings = await dbContext.MetaCatalogSettings.AsNoTracking().SingleAsync();
        Assert.Equal("catalog-987", storedRestaurant.WhatsAppCatalogId);
        Assert.Equal("catalog-987", storedSettings.CatalogId);
        Assert.False(storedSettings.IsEnabled);
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
