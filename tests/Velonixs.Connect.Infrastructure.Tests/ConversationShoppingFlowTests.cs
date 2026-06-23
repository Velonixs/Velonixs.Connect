using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Security;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class ConversationShoppingFlowTests
{
    [Fact]
    public async Task QuantitySelection_ShowsCartAndAddMoreReturnsToCategories()
    {
        await using var dbContext = CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "Test Restaurant",
            WhatsAppPhoneNumberId = "phone-id"
        };
        var category = new MenuCategory
        {
            Restaurant = restaurant,
            Name = "Pizza",
            DisplayOrder = 1
        };
        var paneerPizza = MenuItem(restaurant, category, "Paneer Pizza", 249, 1);
        var farmhousePizza = MenuItem(restaurant, category, "Farmhouse Pizza", 299, 2);

        dbContext.AddRange(restaurant, category, paneerPizza, farmhousePizza);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        await Send(service, $"category.select:{category.Id}");
        await Send(service, $"item.select:{paneerPizza.Id}");
        var firstAdd = await Send(service, $"quantity.select:{paneerPizza.Id}:2");

        Assert.Contains("Paneer Pizza x2 added", firstAdd.ReplyText);
        Assert.Contains("Your Cart", firstAdd.ReplyText);
        Assert.Contains("2 x Paneer Pizza", firstAdd.ReplyText);
        Assert.Contains("Total: Rs 498", firstAdd.ReplyText);
        Assert.DoesNotContain("What would you like to do?", firstAdd.ReplyText);

        var conversation = await dbContext.Conversations.SingleAsync();
        var firstDraft = ReadDraft(conversation);
        var cartId = firstDraft.MenuSelection.CartId;

        Assert.Equal(category.Id, firstDraft.MenuSelection.CurrentCategoryId);
        Assert.Equal("Pizza", firstDraft.MenuSelection.CurrentCategoryName);
        Assert.Equal(0, firstDraft.MenuSelection.CurrentMenuPage);
        Assert.NotNull(cartId);
        Assert.Equal(paneerPizza.Id, firstDraft.MenuSelection.LastSelectedMenuItem?.MenuItemId);
        Assert.Equal(ConversationStates.CartReview, conversation.CurrentState);

        var addMore = await Send(service, "cart.add_more");

        Assert.Contains("Please choose a category", addMore.ReplyText);
        Assert.Single(ReadDraft(conversation).Items);

        await Send(service, $"category.select:{category.Id}");
        await Send(service, $"item.select:{farmhousePizza.Id}");
        var secondAdd = await Send(service, $"quantity.select:{farmhousePizza.Id}:1");

        var finalDraft = ReadDraft(conversation);
        Assert.Equal(cartId, finalDraft.MenuSelection.CartId);
        Assert.Equal(category.Id, finalDraft.MenuSelection.CurrentCategoryId);
        Assert.Equal(2, finalDraft.Items.Count);
        Assert.Equal(797, finalDraft.TotalAmount);
        Assert.Contains("Your Cart", secondAdd.ReplyText);
        Assert.Contains("2 x Paneer Pizza", secondAdd.ReplyText);
        Assert.Contains("1 x Farmhouse Pizza", secondAdd.ReplyText);
    }

    private static ConversationService CreateService(RestaurantConnectDbContext dbContext)
    {
        var menuSearch = new MenuSearchService();
        return new ConversationService(
            dbContext,
            new FakeMessageSender(),
            new FakeNotificationService(),
            menuSearch,
            new FreeTextOrderParser(menuSearch),
            new OrderingCartService(),
            new CategoryMessageBuilder(),
            new MenuMessageBuilder(),
            new QuantityMessageBuilder(),
            new CartNavigationMessageBuilder(),
            NullLogger<ConversationService>.Instance);
    }

    private static RestaurantConnectDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RestaurantConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RestaurantConnectDbContext(options, new PassThroughEncryptionService());
    }

    private static Task<WhatsAppWebhookProcessResult> Send(
        IConversationService service,
        string message) =>
        service.ProcessIncomingMessageAsync(
            new IncomingWhatsAppMessage(
                "phone-id",
                "919999999999",
                message,
                Guid.NewGuid().ToString("N"),
                "Customer"));

    private static PendingOrderDraft ReadDraft(Conversation conversation) =>
        JsonSerializer.Deserialize<PendingOrderDraft>(
            conversation.TempOrderJson!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static MenuItem MenuItem(
        Restaurant restaurant,
        MenuCategory category,
        string name,
        decimal price,
        int itemCode) =>
        new()
        {
            Restaurant = restaurant,
            Category = category,
            Name = name,
            Price = price,
            ItemCode = itemCode
        };

    private sealed class PassThroughEncryptionService : IFieldEncryptionService
    {
        public string Encrypt(string plaintext) => plaintext;
        public string Decrypt(string protectedValue) => protectedValue;
    }

    private sealed class FakeMessageSender : IWhatsAppMessageSender
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
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public Task NotifyOrderConfirmedAsync(
            Restaurant restaurant,
            Customer customer,
            Order order,
            IReadOnlyCollection<OrderItem> items,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NotifyStaffHandoverAsync(
            Restaurant restaurant,
            Customer customer,
            string customerMessage,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
