using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Api.Controllers;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Configuration;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class WhatsAppNativeCatalogTransportTests
{
    [Fact]
    public async Task SendCatalogMessageAsync_MapsNativeCatalogInteractivePayload()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var handler = new RecordingHttpMessageHandler();
        handler.Respond(HttpStatusCode.OK, "{\"messages\":[{\"id\":\"wamid.catalog-1\"}]}");
        var sender = new WhatsAppCloudMessageSender(
            new HttpClient(handler),
            Options.Create(new WhatsAppOptions
            {
                BaseUrl = "https://graph.example.test",
                ApiVersion = "v99.0",
                AccessToken = "global-access-token",
                DisableSending = false
            }),
            NullLogger<WhatsAppCloudMessageSender>.Instance,
            dbContext);

        var result = await sender.SendCatalogMessageAsync(
            "phone-number-id",
            "+91 (98765) 43210",
            "Browse today's menu.",
            " pizza-001 ",
            "Freshly prepared.");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsSkipped);
        Assert.Equal("wamid.catalog-1", result.ProviderMessageId);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://graph.example.test/v99.0/phone-number-id/messages", request.Uri.AbsoluteUri);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("global-access-token", request.AuthorizationParameter);

        using var document = JsonDocument.Parse(Assert.IsType<string>(request.Body));
        var payload = document.RootElement;
        var interactive = payload.GetProperty("interactive");

        Assert.Equal("whatsapp", payload.GetProperty("messaging_product").GetString());
        Assert.Equal("individual", payload.GetProperty("recipient_type").GetString());
        Assert.Equal("919876543210", payload.GetProperty("to").GetString());
        Assert.Equal("interactive", payload.GetProperty("type").GetString());
        Assert.Equal("catalog_message", interactive.GetProperty("type").GetString());
        Assert.Equal("Browse today's menu.", interactive.GetProperty("body").GetProperty("text").GetString());
        Assert.Equal("catalog_message", interactive.GetProperty("action").GetProperty("name").GetString());
        Assert.Equal(
            "pizza-001",
            interactive.GetProperty("action").GetProperty("parameters")
                .GetProperty("thumbnail_product_retailer_id").GetString());
        Assert.Equal("Freshly prepared.", interactive.GetProperty("footer").GetProperty("text").GetString());
    }

    [Fact]
    public async Task Receive_MapsEveryNativeOrderLineIncludingQuantitiesAndSubmittedPrices()
    {
        var conversationService = new CapturingConversationService();
        var controller = new WhatsAppWebhookController(
            conversationService,
            Options.Create(new WhatsAppOptions()),
            Options.Create(new RestaurantConnectOptions()),
            new DevelopmentWebHostEnvironment(),
            NullLogger<WhatsAppWebhookController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        const string payload = """
            {
              "entry": [
                {
                  "changes": [
                    {
                      "value": {
                        "metadata": { "phone_number_id": "phone-number-id" },
                        "contacts": [{ "profile": { "name": "Catalog Customer" } }],
                        "messages": [
                          {
                            "from": "919876543210",
                            "id": "wamid.order-1",
                            "type": "order",
                            "order": {
                              "catalog_id": "catalog-123",
                              "product_items": [
                                {
                                  "product_retailer_id": "momo-001",
                                  "quantity": 2,
                                  "item_price": "120.50",
                                  "currency": "INR"
                                },
                                {
                                  "product_retailer_id": "biryani-001",
                                  "quantity": "1",
                                  "item_price": 180,
                                  "currency": "INR"
                                },
                                {
                                  "product_retailer_id": "coke-001",
                                  "quantity": 3,
                                  "item_price": "40",
                                  "currency": "INR"
                                }
                              ]
                            }
                          }
                        ]
                      }
                    }
                  ]
                }
              ]
            }
            """;

        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var actionResult = await controller.Receive(CancellationToken.None);

        Assert.IsType<OkObjectResult>(actionResult);
        var message = Assert.Single(conversationService.Messages);
        Assert.Equal("phone-number-id", message.PhoneNumberId);
        Assert.Equal("919876543210", message.FromPhoneNumber);
        Assert.Equal("wamid.order-1", message.WhatsAppMessageId);
        Assert.Equal("Catalog Customer", message.ProfileName);
        Assert.Equal("order", message.MessageType);
        Assert.Equal("catalog.order", message.MessageText);
        Assert.Equal("catalog-123", message.OrderCatalogId);

        Assert.NotNull(message.OrderItems);
        var orderItems = message.OrderItems!.ToArray();
        Assert.Collection(
            orderItems,
            item =>
            {
                Assert.Equal("momo-001", item.ProductRetailerId);
                Assert.Equal(2, item.Quantity);
                Assert.Equal(120.50m, item.ItemPrice);
                Assert.Equal("INR", item.Currency);
            },
            item =>
            {
                Assert.Equal("biryani-001", item.ProductRetailerId);
                Assert.Equal(1, item.Quantity);
                Assert.Equal(180m, item.ItemPrice);
                Assert.Equal("INR", item.Currency);
            },
            item =>
            {
                Assert.Equal("coke-001", item.ProductRetailerId);
                Assert.Equal(3, item.Quantity);
                Assert.Equal(40m, item.ItemPrice);
                Assert.Equal("INR", item.Currency);
            });
    }

    [Fact]
    public async Task CatalogCartSubmission_CreatesOneCatalogOrderWithAllAuthoritativeLines()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "Spice Garden",
            WhatsAppPhoneNumberId = "phone-number-id",
            WhatsAppCatalogId = "catalog-123"
        };
        var category = new MenuCategory
        {
            Restaurant = restaurant,
            Name = "Mains",
            DisplayOrder = 1
        };
        var momo = CatalogItem(restaurant, category, 1, "Chicken Momo", "momo-001", 120m);
        var biryani = CatalogItem(restaurant, category, 2, "Chicken Biryani", "biryani-001", 180m);
        var coke = CatalogItem(restaurant, category, 3, "Coke", "coke-001", 40m);

        dbContext.AddRange(restaurant, category, momo, biryani, coke);
        await dbContext.SaveChangesAsync();

        var notifications = new RecordingNotificationService();
        var service = CreateConversationService(dbContext, notifications);

        var nativeOrder = new IncomingWhatsAppMessage(
            "phone-number-id",
            "919876543210",
            "catalog.order",
            "wamid.catalog-order-1",
            "Catalog Customer",
            DateTimeOffset.UtcNow,
            new[]
            {
                new IncomingWhatsAppOrderItem("momo-001", 2, 1m, "INR"),
                new IncomingWhatsAppOrderItem("biryani-001", 1, 180m, "INR"),
                new IncomingWhatsAppOrderItem("coke-001", 3, 40m, "INR")
            },
            "catalog-123",
            "order");
        var received = await service.ProcessIncomingMessageAsync(nativeOrder);

        Assert.Contains("price changed", received.ReplyText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Total: Rs 540", received.ReplyText);

        await SendTextAsync(service, "cart.checkout", "wamid.checkout-1");
        await SendTextAsync(service, "checkout.pickup", "wamid.pickup-1");
        await SendTextAsync(service, "yes", "wamid.confirm-1");
        var replay = await service.ProcessIncomingMessageAsync(nativeOrder);

        var order = await dbContext.Orders
            .Include(x => x.Items)
            .SingleAsync();
        var itemsByRetailerId = order.Items.ToDictionary(item => item.ProductRetailerId!);

        Assert.Equal(OrderSources.WhatsAppCatalog, order.Source);
        Assert.Equal("wamid.catalog-order-1", order.ExternalWhatsAppMessageId);
        Assert.Equal("INR", order.Currency);
        Assert.Equal(540m, order.SubTotalAmount);
        Assert.Equal(540m, order.TotalAmount);
        Assert.Equal(3, itemsByRetailerId.Count);
        Assert.Equal((2, 120m, 240m), (
            itemsByRetailerId["momo-001"].Quantity,
            itemsByRetailerId["momo-001"].UnitPrice,
            itemsByRetailerId["momo-001"].LineTotal));
        Assert.Equal((1, 180m, 180m), (
            itemsByRetailerId["biryani-001"].Quantity,
            itemsByRetailerId["biryani-001"].UnitPrice,
            itemsByRetailerId["biryani-001"].LineTotal));
        Assert.Equal((3, 40m, 120m), (
            itemsByRetailerId["coke-001"].Quantity,
            itemsByRetailerId["coke-001"].UnitPrice,
            itemsByRetailerId["coke-001"].LineTotal));
        Assert.False(replay.IsProcessed);
        Assert.Equal("Duplicate WhatsApp message.", replay.Error);
        Assert.Single(await dbContext.Orders.ToArrayAsync());
        Assert.Equal(1, notifications.ConfirmedOrderCount);
    }

    [Fact]
    public async Task CatalogCartSubmission_RejectsSoldOutLineBeforeCreatingACart()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "Spice Garden",
            WhatsAppPhoneNumberId = "phone-number-id",
            WhatsAppCatalogId = "catalog-123"
        };
        var category = new MenuCategory
        {
            Restaurant = restaurant,
            Name = "Mains",
            DisplayOrder = 1
        };
        var availableMomo = CatalogItem(restaurant, category, 1, "Chicken Momo", "momo-001", 120m);
        var soldOutBiryani = CatalogItem(restaurant, category, 2, "Chicken Biryani", "biryani-001", 180m);
        soldOutBiryani.IsAvailable = false;

        dbContext.AddRange(restaurant, category, availableMomo, soldOutBiryani);
        await dbContext.SaveChangesAsync();

        var notifications = new RecordingNotificationService();
        var service = CreateConversationService(dbContext, notifications);

        var result = await service.ProcessIncomingMessageAsync(
            new IncomingWhatsAppMessage(
                "phone-number-id",
                "919876543210",
                "catalog.order",
                "wamid.sold-out-cart-1",
                "Catalog Customer",
                DateTimeOffset.UtcNow,
                new[]
                {
                    new IncomingWhatsAppOrderItem("momo-001", 2, 120m, "INR"),
                    new IncomingWhatsAppOrderItem("biryani-001", 1, 180m, "INR")
                },
                "catalog-123",
                "order"));

        Assert.Contains("Chicken Biryani", result.ReplyText);
        Assert.Contains("sold out", result.ReplyText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await dbContext.Orders.ToArrayAsync());
        Assert.Null((await dbContext.Conversations.SingleAsync()).TempOrderJson);
        Assert.Equal(0, notifications.ConfirmedOrderCount);
    }

    private static ConversationService CreateConversationService(
        Velonixs.Connect.Persistence.Persistence.RestaurantConnectDbContext dbContext,
        INotificationService notificationService)
    {
        var menuSearch = new MenuSearchService();
        return new ConversationService(
            dbContext,
            new SuccessfulMessageSender(),
            notificationService,
            menuSearch,
            new FreeTextOrderParser(menuSearch),
            new OrderingCartService(),
            NullLogger<ConversationService>.Instance);
    }

    private static Task<WhatsAppWebhookProcessResult> SendTextAsync(
        IConversationService service,
        string text,
        string messageId) =>
        service.ProcessIncomingMessageAsync(new IncomingWhatsAppMessage(
            "phone-number-id",
            "919876543210",
            text,
            messageId,
            "Catalog Customer"));

    private static MenuItem CatalogItem(
        Restaurant restaurant,
        MenuCategory category,
        int itemCode,
        string name,
        string retailerId,
        decimal price) =>
        new()
        {
            Restaurant = restaurant,
            Category = category,
            ItemCode = itemCode,
            Name = name,
            Price = price,
            ProductRetailerId = retailerId,
            MetaProductId = $"meta-{retailerId}",
            SyncStatus = "Synced",
            IsActive = true,
            IsAvailable = true
        };

    private sealed class CapturingConversationService : IConversationService
    {
        public List<IncomingWhatsAppMessage> Messages { get; } = [];

        public Task<WhatsAppWebhookProcessResult> ProcessIncomingMessageAsync(
            IncomingWhatsAppMessage message,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.FromResult(new WhatsAppWebhookProcessResult(
                true,
                "Processed",
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "CartReview"));
        }
    }

    private sealed class SuccessfulMessageSender : IWhatsAppMessageSender
    {
        public Task<WhatsAppSendResult> SendTextMessageAsync(
            string phoneNumberId,
            string recipientPhoneNumber,
            string message,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WhatsAppSendResult(true, false));

        public Task<WhatsAppSendResult> SendInteractiveListMessageAsync(
            string phoneNumberId,
            string recipientPhoneNumber,
            string bodyText,
            string buttonText,
            IReadOnlyCollection<WhatsAppInteractiveListSection> sections,
            string? footerText = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WhatsAppSendResult(true, false));

        public Task<WhatsAppSendResult> SendReplyButtonMessageAsync(
            string phoneNumberId,
            string recipientPhoneNumber,
            string bodyText,
            IReadOnlyCollection<WhatsAppReplyButton> buttons,
            string? footerText = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WhatsAppSendResult(true, false));

        public Task<WhatsAppSendResult> SendCatalogMessageAsync(
            string phoneNumberId,
            string recipientPhoneNumber,
            string bodyText,
            string? thumbnailProductRetailerId = null,
            string? footerText = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WhatsAppSendResult(true, false));

        public Task<WhatsAppSendResult> SendMultiProductMessageAsync(
            string phoneNumberId,
            string recipientPhoneNumber,
            string catalogId,
            string headerText,
            string bodyText,
            IReadOnlyCollection<WhatsAppProductListSection> sections,
            string? footerText = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WhatsAppSendResult(true, false));
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public int ConfirmedOrderCount { get; private set; }

        public Task NotifyOrderConfirmedAsync(
            Restaurant restaurant,
            Customer customer,
            Order order,
            IReadOnlyCollection<OrderItem> items,
            CancellationToken cancellationToken = default)
        {
            ConfirmedOrderCount++;
            return Task.CompletedTask;
        }

        public Task NotifyStaffHandoverAsync(
            Restaurant restaurant,
            Customer customer,
            string customerMessage,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<WhatsAppSendResult> NotifyCustomerOrderStatusAsync(
            Restaurant restaurant,
            Customer customer,
            Order order,
            string message,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WhatsAppSendResult(true, false));
    }

    private sealed class DevelopmentWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Velonixs.Connect.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
