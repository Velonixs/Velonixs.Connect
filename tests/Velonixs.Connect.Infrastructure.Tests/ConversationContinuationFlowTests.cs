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

public sealed class ConversationContinuationFlowTests
{
    [Fact]
    public async Task QuantitySelection_ShowsContinueListAndAllowsAnotherItemInSameCategory()
    {
        await using var dbContext = CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "99 Restaurant",
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

        var sender = new FakeMessageSender();
        var service = CreateService(dbContext, sender);

        await Send(service, $"category.select:{category.Id}");
        await Send(service, $"item.select:{paneerPizza.Id}");
        var firstAdd = await Send(service, $"quantity.select:{paneerPizza.Id}:5");

        Assert.Contains("✓ Paneer Pizza x5 added", firstAdd.ReplyText);
        Assert.Contains("What would you like to do?", firstAdd.ReplyText);
        Assert.Equal("Cart total: Rs 1245", sender.LastFooterText);
        Assert.Equal("Continue", sender.LastButtonText);
        Assert.Contains(sender.LastSections.SelectMany(x => x.Rows), row => row.Id == "menu.continue");

        var conversation = await dbContext.Conversations.SingleAsync();
        var firstDraft = ReadDraft(conversation);

        Assert.Equal(category.Id, firstDraft.SelectedCategoryId);
        Assert.Equal("Pizza", firstDraft.SelectedCategoryName);
        Assert.Equal(ConversationStates.ItemSelection, conversation.CurrentState);

        var continued = await Send(service, "menu.continue");

        Assert.Contains("Pizza items", continued.ReplyText);

        await Send(service, $"item.select:{farmhousePizza.Id}");
        await Send(service, $"quantity.select:{farmhousePizza.Id}:1");

        var finalDraft = ReadDraft(conversation);

        Assert.Equal(2, finalDraft.Items.Count);
        Assert.Equal(1544, finalDraft.TotalAmount);
        Assert.Contains(finalDraft.Items, item => item.ItemName == "Paneer Pizza" && item.Quantity == 5);
        Assert.Contains(finalDraft.Items, item => item.ItemName == "Farmhouse Pizza" && item.Quantity == 1);
    }

    private static ConversationService CreateService(
        RestaurantConnectDbContext dbContext,
        IWhatsAppMessageSender sender)
    {
        var menuSearch = new MenuSearchService();
        return new ConversationService(
            dbContext,
            sender,
            new FakeNotificationService(),
            menuSearch,
            new FreeTextOrderParser(menuSearch),
            new OrderingCartService(),
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
        public string? LastButtonText { get; private set; }
        public string? LastFooterText { get; private set; }
        public IReadOnlyCollection<WhatsAppInteractiveListSection> LastSections { get; private set; } =
            Array.Empty<WhatsAppInteractiveListSection>();

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
            CancellationToken cancellationToken = default)
        {
            LastButtonText = buttonText;
            LastFooterText = footerText;
            LastSections = sections;
            return Task.FromResult(new WhatsAppSendResult(true, false));
        }

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
