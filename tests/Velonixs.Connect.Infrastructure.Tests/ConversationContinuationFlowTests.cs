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
    public async Task Greeting_ShowsNumberedMenuAndSelectionGoesToQuantity()
    {
        await using var dbContext = CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = "phone-id"
        };
        var pizza = new MenuCategory
        {
            Restaurant = restaurant,
            Name = "Pizza",
            DisplayOrder = 1
        };
        var paneerPizza = MenuItem(restaurant, pizza, "Paneer Pizza", 249, 1);
        var margheritaPizza = MenuItem(restaurant, pizza, "Margherita Pizza", 199, 2);
        var farmhousePizza = MenuItem(restaurant, pizza, "Farmhouse Pizza", 299, 3);

        dbContext.AddRange(restaurant, pizza, paneerPizza, margheritaPizza, farmhousePizza);
        await dbContext.SaveChangesAsync();

        var sender = new FakeMessageSender();
        var service = CreateService(dbContext, sender);

        var greeting = await Send(service, "Hi");

        Assert.Contains("Welcome to 99 Restaurant!", greeting.ReplyText);
        Assert.Contains("Pizza", greeting.ReplyText);
        Assert.Contains("1. Paneer Pizza - Rs 249", greeting.ReplyText);
        Assert.Contains("2. Margherita Pizza - Rs 199", greeting.ReplyText);
        Assert.Empty(sender.LastSections);
        Assert.Equal(new[] { "cart.view", "cart.checkout", "main.staff" }, sender.LastButtons.Select(x => x.Id));
        Assert.All(sender.LastButtons, button => Assert.True(button.Title.Length <= 20));

        var conversation = await dbContext.Conversations.SingleAsync();
        var draft = ReadDraft(conversation);

        Assert.Equal(restaurant.Id, draft.CurrentRestaurantId);
        Assert.Equal(pizza.Id, draft.CurrentCategoryId);
        Assert.Equal(ConversationStates.ItemSelection, draft.CurrentStep);

        var quantity = await Send(service, "1");

        Assert.Contains("Paneer Pizza — Rs 249", quantity.ReplyText);
        Assert.Equal(new[] { "1", "2", "3" }, sender.LastButtons.Select(x => x.Title));
        Assert.Empty(sender.LastSections);
    }

    [Fact]
    public async Task Greeting_DoesNotUseInteractiveList()
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
        var item = MenuItem(restaurant, category, "Paneer Pizza", 249, 1);

        dbContext.AddRange(restaurant, category, item);
        await dbContext.SaveChangesAsync();

        var sender = new FakeMessageSender { FailInteractiveLists = true };
        var service = CreateService(dbContext, sender);

        await Send(service, "Hi");

        Assert.Equal(0, sender.InteractiveListSendCount);
        Assert.Empty(sender.TextMessages);
        Assert.Contains("Welcome to 99 Restaurant", sender.LastButtonBodyText);
        Assert.Contains("1. Paneer Pizza - Rs 249", sender.LastButtonBodyText);
        Assert.Equal(new[] { "View Cart", "Checkout", "Contact Us" }, sender.LastButtons.Select(x => x.Title));
    }

    [Fact]
    public async Task DuplicateIncomingMessageId_IsIgnoredWithoutSendingSecondPrompt()
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
        var item = MenuItem(restaurant, category, "Paneer Pizza", 249, 1);

        dbContext.AddRange(restaurant, category, item);
        await dbContext.SaveChangesAsync();

        var sender = new FakeMessageSender();
        var service = CreateService(dbContext, sender);
        const string messageId = "wamid.same-message";

        var first = await SendWithId(service, "Hi", messageId);
        var duplicate = await SendWithId(service, "Hi", messageId);

        Assert.True(first.IsProcessed);
        Assert.False(duplicate.IsProcessed);
        Assert.Equal("Duplicate WhatsApp message.", duplicate.Error);
        Assert.Equal(1, sender.ReplyButtonSendCount);
        Assert.Equal(2, await dbContext.MessageLogs.CountAsync());
    }

    [Fact]
    public async Task EmptyCartView_ShowsDirectMenuAgain()
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
        var item = MenuItem(restaurant, category, "Paneer Pizza", 249, 1);

        dbContext.AddRange(restaurant, category, item);
        await dbContext.SaveChangesAsync();

        var sender = new FakeMessageSender();
        var service = CreateService(dbContext, sender);

        var cart = await Send(service, "cart.view");

        Assert.Contains("Your cart is currently empty", cart.ReplyText);
        Assert.Contains("Select an item from the menu", cart.ReplyText);
        Assert.Empty(sender.LastSections);
        Assert.Contains("1. Paneer Pizza - Rs 249", cart.ReplyText);
        Assert.Equal(new[] { "View Cart", "Checkout", "Contact Us" }, sender.LastButtons.Select(x => x.Title));
    }

    [Fact]
    public async Task NumberedMenu_PaginatesWithExpectedThreeButtonSets()
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
        var items = Enumerable.Range(1, 14)
            .Select(index => MenuItem(restaurant, category, $"Pizza {index}", 100 + index, index))
            .ToArray();

        dbContext.AddRange(restaurant, category);
        dbContext.AddRange(items);
        await dbContext.SaveChangesAsync();

        var sender = new FakeMessageSender();
        var service = CreateService(dbContext, sender);

        var firstPage = await Send(service, "Hi");

        Assert.Contains("Page 1 of 3", firstPage.ReplyText);
        Assert.Contains("1. Pizza 1 - Rs 101", firstPage.ReplyText);
        Assert.Equal(new[] { "Next", "View Cart", "Checkout" }, sender.LastButtons.Select(x => x.Title));

        var middlePage = await Send(service, "menu.next");

        Assert.Contains("Page 2 of 3", middlePage.ReplyText);
        Assert.Contains("1. Pizza 7 - Rs 107", middlePage.ReplyText);
        Assert.Equal(new[] { "Previous", "Next", "View Cart" }, sender.LastButtons.Select(x => x.Title));

        var lastPage = await Send(service, "menu.next");

        Assert.Contains("Page 3 of 3", lastPage.ReplyText);
        Assert.Contains("1. Pizza 13 - Rs 113", lastPage.ReplyText);
        Assert.Equal(new[] { "Previous", "View Cart", "Checkout" }, sender.LastButtons.Select(x => x.Title));
        Assert.All(sender.LastButtons, _ => Assert.True(sender.LastButtons.Count <= 3));
    }

    [Fact]
    public async Task NumberedMenu_SelectsItemByNumberAndKeepsCartWhilePaging()
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
        var items = Enumerable.Range(1, 8)
            .Select(index => MenuItem(restaurant, category, $"Pizza {index}", 100 + index, index))
            .ToArray();

        dbContext.AddRange(restaurant, category);
        dbContext.AddRange(items);
        await dbContext.SaveChangesAsync();

        var sender = new FakeMessageSender();
        var service = CreateService(dbContext, sender);

        await Send(service, "Hi");
        await Send(service, "2");
        var firstAdd = await Send(service, "4");

        Assert.Contains("✅ Added to cart", firstAdd.ReplyText);
        Assert.Contains("4 × Pizza 2", firstAdd.ReplyText);
        Assert.Contains("Cart total: Rs 408", firstAdd.ReplyText);
        Assert.Contains("Page 1 of 2", firstAdd.ReplyText);

        await Send(service, "menu.next");
        await Send(service, "1");
        var secondAdd = await Send(service, "2");

        Assert.Contains("2 × Pizza 7", secondAdd.ReplyText);
        Assert.Contains("Page 2 of 2", secondAdd.ReplyText);

        var cart = await Send(service, "cart.view");

        Assert.Contains("4 × Pizza 2", cart.ReplyText);
        Assert.Contains("2 × Pizza 7", cart.ReplyText);
        Assert.Contains("Total: Rs 622", cart.ReplyText);
    }

    [Fact]
    public async Task QuantitySelection_ShowsAddedConfirmationAndDirectMenuForSameCategory()
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

        await Send(service, "Hi");
        await Send(service, "1");
        var firstAdd = await Send(service, "5");

        Assert.Contains("✅ Added to cart", firstAdd.ReplyText);
        Assert.Contains("5 × Paneer Pizza", firstAdd.ReplyText);
        Assert.Contains("Cart total: Rs 1245", firstAdd.ReplyText);
        Assert.DoesNotContain("What would you like to do?", firstAdd.ReplyText);
        Assert.Empty(sender.LastSections);
        Assert.Contains("Pizza", firstAdd.ReplyText);
        Assert.Contains("2. Farmhouse Pizza - Rs 299", firstAdd.ReplyText);
        Assert.Contains(sender.LastButtons, button => button.Id == "cart.checkout");
        Assert.All(sender.LastButtons, button => Assert.True(sender.LastButtons.Count <= 3));

        var conversation = await dbContext.Conversations.SingleAsync();
        var firstDraft = ReadDraft(conversation);

        Assert.Equal(category.Id, firstDraft.SelectedCategoryId);
        Assert.Equal("Pizza", firstDraft.SelectedCategoryName);
        Assert.Equal(ConversationStates.ItemSelection, conversation.CurrentState);

        await Send(service, "2");
        await Send(service, "1");

        var finalDraft = ReadDraft(conversation);

        Assert.Equal(2, finalDraft.Items.Count);
        Assert.Equal(1544, finalDraft.TotalAmount);
        Assert.Contains(finalDraft.Items, item => item.ItemName == "Paneer Pizza" && item.Quantity == 5);
        Assert.Contains(finalDraft.Items, item => item.ItemName == "Farmhouse Pizza" && item.Quantity == 1);
    }

    [Fact]
    public async Task QuantitySelection_FromSearchResult_ReturnsToDefaultDirectMenu()
    {
        await using var dbContext = CreateDbContext();
        var restaurant = new Restaurant
        {
            Name = "99 Restaurant",
            WhatsAppPhoneNumberId = "phone-id"
        };
        var burgers = new MenuCategory
        {
            Restaurant = restaurant,
            Name = "Burgers",
            DisplayOrder = 1
        };
        var pizza = new MenuCategory
        {
            Restaurant = restaurant,
            Name = "Pizza",
            DisplayOrder = 2
        };
        var vegBurger = MenuItem(restaurant, burgers, "Veg Burger", 99, 1);
        var paneerPizza = MenuItem(restaurant, pizza, "Paneer Pizza", 249, 2);
        var farmhousePizza = MenuItem(restaurant, pizza, "Farmhouse Pizza", 299, 3);

        dbContext.AddRange(restaurant, burgers, pizza, vegBurger, paneerPizza, farmhousePizza);
        await dbContext.SaveChangesAsync();

        var sender = new FakeMessageSender();
        var service = CreateService(dbContext, sender);

        await Send(service, "pizza");
        await Send(service, $"item.select:{farmhousePizza.Id}");
        var added = await Send(service, "1");

        Assert.Contains("1 × Farmhouse Pizza", added.ReplyText);
        Assert.Contains("Burgers", added.ReplyText);
        Assert.Contains("1. Veg Burger - Rs 99", added.ReplyText);
    }

    [Fact]
    public async Task DeliveryCheckout_WithSavedAddress_SkipsAddressPromptAndShowsYesNoButtons()
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
            Name = "Main",
            DisplayOrder = 1
        };
        var item = MenuItem(restaurant, category, "Paneer Butter Masala", 220, 1);
        var customer = new Customer
        {
            Restaurant = restaurant,
            PhoneNumber = "919999999999",
            Name = "Rajesh Pandit",
            LastAddress = "Saved Street 123"
        };

        dbContext.AddRange(restaurant, category, item, customer);
        await dbContext.SaveChangesAsync();

        var sender = new FakeMessageSender();
        var service = CreateService(dbContext, sender);

        await AddOneItemToCart(service, category, item, 4);
        await Send(service, "cart.checkout");
        var confirmation = await Send(service, "checkout.delivery");

        Assert.DoesNotContain("Please share your delivery address", confirmation.ReplyText);
        Assert.Contains("Please confirm your order", confirmation.ReplyText);
        Assert.Contains("Address: Saved Street 123", confirmation.ReplyText);
        Assert.Equal(new[] { "yes", "no" }, sender.LastButtons.Select(x => x.Id));

        var conversation = await dbContext.Conversations.SingleAsync();
        var draft = ReadDraft(conversation);

        Assert.Equal("Saved Street 123", draft.Address);
        Assert.Equal(ConversationStates.ConfirmationPending, conversation.CurrentState);
    }

    [Fact]
    public async Task DeliveryCheckout_FirstAddressEntry_SavesAddressAndShowsYesNoButtons()
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
            Name = "Main",
            DisplayOrder = 1
        };
        var item = MenuItem(restaurant, category, "Paneer Butter Masala", 220, 1);

        dbContext.AddRange(restaurant, category, item);
        await dbContext.SaveChangesAsync();

        var sender = new FakeMessageSender();
        var service = CreateService(dbContext, sender);

        await AddOneItemToCart(service, category, item, 4);
        await Send(service, "cart.checkout");
        var addressPrompt = await Send(service, "checkout.delivery");

        Assert.Contains("Please share your delivery address", addressPrompt.ReplyText);

        var confirmation = await Send(service, "Abcds");

        Assert.Contains("Please confirm your order", confirmation.ReplyText);
        Assert.Contains("Address: Abcds", confirmation.ReplyText);
        Assert.Equal(new[] { "yes", "no" }, sender.LastButtons.Select(x => x.Id));

        var customer = await dbContext.Customers.SingleAsync();

        Assert.Equal("Abcds", customer.LastAddress);
    }

    [Fact]
    public async Task FinalCustomerConfirmation_CreatesPendingConfirmationOrderWithHistory()
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
            Name = "Main",
            DisplayOrder = 1
        };
        var item = MenuItem(restaurant, category, "Paneer Butter Masala", 220, 1);

        dbContext.AddRange(restaurant, category, item);
        await dbContext.SaveChangesAsync();

        var sender = new FakeMessageSender();
        var service = CreateService(dbContext, sender);

        await AddOneItemToCart(service, category, item, 2);
        await Send(service, "cart.checkout");
        await Send(service, "checkout.pickup");
        var created = await Send(service, "yes");

        Assert.Contains("✅ Order received", created.ReplyText);
        Assert.Contains("Order ID", created.ReplyText);
        Assert.Contains("sent to 99 Restaurant for confirmation", created.ReplyText);

        var order = await dbContext.Orders
            .Include(x => x.StatusHistory)
            .SingleAsync();

        Assert.Equal(OrderStatuses.PendingConfirmation, order.OrderStatus);
        var history = Assert.Single(order.StatusHistory);
        Assert.Equal(OrderStatuses.Draft, history.PreviousStatus);
        Assert.Equal(OrderStatuses.PendingConfirmation, history.NewStatus);
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
        SendWithId(service, message, Guid.NewGuid().ToString("N"));

    private static Task<WhatsAppWebhookProcessResult> SendWithId(
        IConversationService service,
        string message,
        string? messageId) =>
        service.ProcessIncomingMessageAsync(
            new IncomingWhatsAppMessage(
                "phone-id",
                "919999999999",
                message,
                messageId,
                "Customer"));

    private static async Task AddOneItemToCart(
        IConversationService service,
        MenuCategory category,
        MenuItem item,
        int quantity)
    {
        await Send(service, $"category.select:{category.Id}");
        await Send(service, $"item.select:{item.Id}");
        await Send(service, $"quantity.select:{item.Id}:{quantity}");
    }

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
        public bool FailInteractiveLists { get; init; }
        public int InteractiveListSendCount { get; private set; }
        public int ReplyButtonSendCount { get; private set; }
        public List<string> TextMessages { get; } = new();
        public string? LastButtonBodyText { get; private set; }
        public string? LastButtonText { get; private set; }
        public string? LastFooterText { get; private set; }
        public IReadOnlyCollection<WhatsAppInteractiveListSection> LastSections { get; private set; } =
            Array.Empty<WhatsAppInteractiveListSection>();
        public IReadOnlyCollection<WhatsAppReplyButton> LastButtons { get; private set; } =
            Array.Empty<WhatsAppReplyButton>();

        public Task<WhatsAppSendResult> SendTextMessageAsync(
            string phoneNumberId,
            string recipientPhoneNumber,
            string message,
            CancellationToken cancellationToken = default)
        {
            TextMessages.Add(message);
            LastButtonBodyText = null;
            LastButtonText = null;
            LastFooterText = null;
            LastSections = Array.Empty<WhatsAppInteractiveListSection>();
            LastButtons = Array.Empty<WhatsAppReplyButton>();
            return Task.FromResult(new WhatsAppSendResult(true, false));
        }

        public Task<WhatsAppSendResult> SendInteractiveListMessageAsync(
            string phoneNumberId,
            string recipientPhoneNumber,
            string bodyText,
            string buttonText,
            IReadOnlyCollection<WhatsAppInteractiveListSection> sections,
            string? footerText = null,
            CancellationToken cancellationToken = default)
        {
            InteractiveListSendCount++;
            if (FailInteractiveLists)
            {
                return Task.FromResult(new WhatsAppSendResult(false, false, Error: "Meta rejected list."));
            }

            LastButtonBodyText = null;
            LastButtonText = buttonText;
            LastFooterText = footerText;
            LastSections = sections;
            LastButtons = Array.Empty<WhatsAppReplyButton>();
            return Task.FromResult(new WhatsAppSendResult(true, false));
        }

        public Task<WhatsAppSendResult> SendReplyButtonMessageAsync(
            string phoneNumberId,
            string recipientPhoneNumber,
            string bodyText,
            IReadOnlyCollection<WhatsAppReplyButton> buttons,
            string? footerText = null,
            CancellationToken cancellationToken = default)
        {
            ReplyButtonSendCount++;
            LastButtonBodyText = bodyText;
            LastButtonText = null;
            LastButtons = buttons;
            LastFooterText = footerText;
            LastSections = Array.Empty<WhatsAppInteractiveListSection>();
            return Task.FromResult(new WhatsAppSendResult(true, false));
        }
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

        public Task<WhatsAppSendResult> NotifyCustomerOrderStatusAsync(
            Restaurant restaurant,
            Customer customer,
            Order order,
            string message,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WhatsAppSendResult(true, false));
    }
}
