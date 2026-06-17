using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed partial class ConversationService(
    RestaurantConnectDbContext dbContext,
    IWhatsAppMessageSender whatsAppMessageSender,
    INotificationService notificationService,
    ILogger<ConversationService> logger) : IConversationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string MainViewMenuId = "main.view_menu";
    private const string MainPlaceOrderId = "main.place_order";
    private const string MainLocationId = "main.location";
    private const string MainStaffId = "main.staff";
    private const string SelectItemPrefix = "select_item:";
    private const string CartAddMoreId = "cart.add_more";
    private const string CartCheckoutId = "cart.checkout";
    private const string CartCancelId = "cart.cancel";

    public async Task<WhatsAppWebhookProcessResult> ProcessIncomingMessageAsync(
        IncomingWhatsAppMessage message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.PhoneNumberId) ||
            string.IsNullOrWhiteSpace(message.FromPhoneNumber) ||
            string.IsNullOrWhiteSpace(message.MessageText))
        {
            return WhatsAppWebhookProcessResult.Ignored("Missing phone number id, sender, or message text.");
        }

        if (!string.IsNullOrWhiteSpace(message.WhatsAppMessageId))
        {
            var alreadyProcessed = await dbContext.MessageLogs
                .AnyAsync(x => x.WhatsAppMessageId == message.WhatsAppMessageId, cancellationToken);

            if (alreadyProcessed)
            {
                return WhatsAppWebhookProcessResult.Ignored("Duplicate WhatsApp message.");
            }
        }

        var restaurant = await dbContext.Restaurants
            .FirstOrDefaultAsync(
                x => x.WhatsAppPhoneNumberId == message.PhoneNumberId && x.IsActive,
                cancellationToken);

        if (restaurant is null)
        {
            return WhatsAppWebhookProcessResult.Ignored($"No active restaurant found for phone number id '{message.PhoneNumberId}'.");
        }

        var customer = await GetOrCreateCustomerAsync(restaurant.Id, message, cancellationToken);
        var conversation = await GetOrCreateConversationAsync(restaurant.Id, customer, cancellationToken);

        dbContext.MessageLogs.Add(new MessageLog
        {
            RestaurantId = restaurant.Id,
            CustomerId = customer.Id,
            ConversationId = conversation.Id,
            Direction = MessageDirections.Incoming,
            MessageText = message.MessageText,
            WhatsAppMessageId = message.WhatsAppMessageId,
            Status = MessageStatuses.Received,
            CreatedAt = message.ReceivedAt ?? DateTimeOffset.UtcNow
        });

        var reply = await BuildReplyAndUpdateStateAsync(
            restaurant,
            customer,
            conversation,
            message.MessageText,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(reply.Text))
        {
            var sendResult = reply.InteractiveSections.Count > 0
                ? await whatsAppMessageSender.SendInteractiveListMessageAsync(
                    restaurant.WhatsAppPhoneNumberId,
                    customer.PhoneNumber,
                    reply.Text,
                    reply.ButtonText ?? "Select",
                    reply.InteractiveSections,
                    reply.FooterText,
                    cancellationToken)
                : await whatsAppMessageSender.SendTextMessageAsync(
                    restaurant.WhatsAppPhoneNumberId,
                    customer.PhoneNumber,
                    reply.Text,
                    cancellationToken);

            dbContext.MessageLogs.Add(new MessageLog
            {
                RestaurantId = restaurant.Id,
                CustomerId = customer.Id,
                ConversationId = conversation.Id,
                Direction = MessageDirections.Outgoing,
                MessageText = reply.Text,
                WhatsAppMessageId = sendResult.ProviderMessageId,
                Status = sendResult.IsSuccess ? MessageStatuses.Sent : MessageStatuses.Failed
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new WhatsAppWebhookProcessResult(
            true,
            reply.Text,
            restaurant.Id,
            customer.Id,
            conversation.Id,
            conversation.CurrentState);
    }

    private async Task<Customer> GetOrCreateCustomerAsync(
        Guid restaurantId,
        IncomingWhatsAppMessage message,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(
                x => x.RestaurantId == restaurantId && x.PhoneNumber == message.FromPhoneNumber,
                cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                RestaurantId = restaurantId,
                PhoneNumber = message.FromPhoneNumber,
                Name = message.ProfileName,
                CreatedAt = DateTimeOffset.UtcNow,
                LastInteractionAt = DateTimeOffset.UtcNow
            };

            dbContext.Customers.Add(customer);
        }
        else
        {
            customer.LastInteractionAt = DateTimeOffset.UtcNow;
            customer.Name ??= message.ProfileName;
        }

        return customer;
    }

    private async Task<Conversation> GetOrCreateConversationAsync(
        Guid restaurantId,
        Customer customer,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(
                x => x.RestaurantId == restaurantId && x.CustomerId == customer.Id && x.IsActive,
                cancellationToken);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                RestaurantId = restaurantId,
                CustomerId = customer.Id,
                WhatsAppNumber = customer.PhoneNumber,
                CurrentState = ConversationStates.New,
                IsActive = true
            };

            dbContext.Conversations.Add(conversation);
        }

        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        return conversation;
    }

    private async Task<OutgoingWhatsAppReply> BuildReplyAndUpdateStateAsync(
        Domain.Entities.Restaurant restaurant,
        Customer customer,
        Conversation conversation,
        string incomingText,
        CancellationToken cancellationToken)
    {
        var text = incomingText.Trim();
        var normalized = text.ToLowerInvariant();

        if (IsGreetingInput(normalized))
        {
            conversation.CurrentState = ConversationStates.New;
            conversation.IsActive = true;
            conversation.TempOrderJson = null;
            return BuildWelcomeReply(restaurant);
        }

        if (IsMenuInput(normalized))
        {
            var shouldKeepCart = conversation.CurrentState == ConversationStates.CartReview;
            conversation.CurrentState = ConversationStates.MenuSent;
            conversation.IsActive = true;
            if (!shouldKeepCart)
            {
                conversation.TempOrderJson = null;
            }
            return await BuildMenuReplyAsync(restaurant, cancellationToken);
        }

        if (conversation.CurrentState == ConversationStates.CustomerNamePending)
        {
            return OutgoingWhatsAppReply.TextOnly(CaptureCustomerName(customer, conversation, text));
        }

        if (conversation.CurrentState == ConversationStates.AddressPending)
        {
            return OutgoingWhatsAppReply.TextOnly(CaptureAddress(customer, conversation, text));
        }

        if (conversation.CurrentState == ConversationStates.ConfirmationPending)
        {
            return OutgoingWhatsAppReply.TextOnly(await ConfirmOrCancelOrderAsync(restaurant, customer, conversation, normalized, cancellationToken));
        }

        if (conversation.CurrentState is ConversationStates.MenuSent or ConversationStates.OrderInputPending &&
            int.TryParse(normalized, out var typedItemCode))
        {
            return await BuildQuantityReplyAsync(restaurant, typedItemCode, cancellationToken);
        }

        if (conversation.CurrentState == ConversationStates.CartReview)
        {
            if (IsAddMoreInput(normalized))
            {
                conversation.CurrentState = ConversationStates.MenuSent;
                return await BuildMenuReplyAsync(restaurant, cancellationToken);
            }

            if (IsCheckoutInput(normalized))
            {
                return ContinueCheckout(customer, conversation);
            }

            if (IsCancelInput(normalized))
            {
                conversation.CurrentState = ConversationStates.Cancelled;
                conversation.IsActive = false;
                conversation.TempOrderJson = null;
                return OutgoingWhatsAppReply.TextOnly("Your cart has been cancelled. Reply Hi anytime to start again.");
            }
        }

        if (IsOrderInput(text))
        {
            return await CaptureOrderAsync(restaurant, conversation, text, cancellationToken);
        }

        if (normalized.StartsWith(SelectItemPrefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(text[SelectItemPrefix.Length..], out var selectedItemCode))
        {
            return await BuildQuantityReplyAsync(restaurant, selectedItemCode, cancellationToken);
        }

        if (normalized is "1" or "menu" or "view menu")
        {
            conversation.CurrentState = ConversationStates.MenuSent;
            return await BuildMenuReplyAsync(restaurant, cancellationToken);
        }

        if (normalized is "2" or "order" or "place order")
        {
            conversation.CurrentState = ConversationStates.OrderInputPending;
            return OutgoingWhatsAppReply.TextOnly("Please send your order like: Order: 1 x 2, 4 x 1");
        }

        if (normalized is "3" or "location" or "address")
        {
            return OutgoingWhatsAppReply.TextOnly(string.IsNullOrWhiteSpace(restaurant.Address)
                ? "Restaurant location is not configured yet. Please talk to staff."
                : restaurant.Address);
        }

        if (normalized is "4" or "staff" or "talk to staff" or "help")
        {
            conversation.CurrentState = ConversationStates.StaffHandover;
            await notificationService.NotifyStaffHandoverAsync(restaurant, customer, text, cancellationToken);
            return OutgoingWhatsAppReply.TextOnly("Our staff has been notified and will contact you shortly.");
        }

        if (conversation.CurrentState is ConversationStates.New or ConversationStates.Cancelled or ConversationStates.Confirmed)
        {
            conversation.CurrentState = ConversationStates.New;
            conversation.IsActive = true;
            return BuildWelcomeReply(restaurant);
        }

        return OutgoingWhatsAppReply.TextOnly("""
            Sorry, I could not understand your message.
            Please reply with a valid option or type Menu.
            """);
    }

    private static OutgoingWhatsAppReply BuildWelcomeReply(Domain.Entities.Restaurant restaurant)
    {
        var text = MenuTextFormatter.BuildWelcomeMessage(restaurant);
        var sections = new[]
        {
            new WhatsAppInteractiveListSection(
                "Options",
                new[]
                {
                    new WhatsAppInteractiveListRow(MainViewMenuId, "View Menu", "See available items"),
                    new WhatsAppInteractiveListRow(MainPlaceOrderId, "Place Order", "Send item number and quantity"),
                    new WhatsAppInteractiveListRow(MainLocationId, "Restaurant Location", "View address"),
                    new WhatsAppInteractiveListRow(MainStaffId, "Talk to Staff", "Request manual help")
                })
        };

        return new OutgoingWhatsAppReply(text, sections, "Choose option");
    }

    private async Task<OutgoingWhatsAppReply> BuildMenuReplyAsync(
        Domain.Entities.Restaurant restaurant,
        CancellationToken cancellationToken)
    {
        var categories = await dbContext.MenuCategories
            .Where(x => x.RestaurantId == restaurant.Id && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToArrayAsync(cancellationToken);

        var items = await dbContext.MenuItems
            .Where(x => x.RestaurantId == restaurant.Id && x.IsActive && x.IsAvailable)
            .OrderBy(x => x.ItemCode)
            .ToArrayAsync(cancellationToken);

        var text = MenuTextFormatter.BuildMenuMessage(restaurant, categories, items);

        if (items.Length == 0)
        {
            return OutgoingWhatsAppReply.TextOnly(text);
        }

        var sections = categories
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(category => new WhatsAppInteractiveListSection(
                category.Name,
                items
                    .Where(item => item.CategoryId == category.Id)
                    .OrderBy(item => item.ItemCode)
                    .Select(item => new WhatsAppInteractiveListRow(
                        $"menu.item.{item.ItemCode}",
                        $"{item.ItemCode}. {item.Name}".Length <= 24
                            ? $"{item.ItemCode}. {item.Name}"
                            : $"{item.ItemCode}. {item.Name}"[..24],
                        $"Rs {item.Price:0.##}"))
                    .ToArray()))
            .Where(section => section.Rows.Count > 0)
            .ToArray();

        return new OutgoingWhatsAppReply(
            text,
            sections,
            "Select item",
            "Tap an item to order 1 quantity, or type Order: 1 x 2, 4 x 1");
    }

    private async Task<OutgoingWhatsAppReply> BuildQuantityReplyAsync(
        Domain.Entities.Restaurant restaurant,
        int itemCode,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.MenuItems
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.RestaurantId == restaurant.Id &&
                     x.ItemCode == itemCode &&
                     x.IsActive &&
                     x.IsAvailable,
                cancellationToken);

        if (item is null)
        {
            return OutgoingWhatsAppReply.TextOnly("""
                Some items in your order are not valid.
                Please check the menu and try again.
                Example: Order: 1 x 2, 4 x 1
                """);
        }

        var sections = new[]
        {
            new WhatsAppInteractiveListSection(
                "Quantity",
                Enumerable.Range(1, 5)
                    .Select(quantity => new WhatsAppInteractiveListRow(
                        $"menu.qty.{itemCode}.{quantity}",
                        quantity == 1 ? "1 item" : $"{quantity} items",
                        $"{item.Name} x {quantity}"))
                    .ToArray())
        };

        return new OutgoingWhatsAppReply(
            $"Selected: {item.Name} - Rs {item.Price:0.##}\n\nChoose quantity:",
            sections,
            "Quantity",
            "For multiple items, type Order: 1 x 2, 4 x 1");
    }

    private static OutgoingWhatsAppReply BuildCartReviewReply(PendingOrderDraft draft)
    {
        var sections = new[]
        {
            new WhatsAppInteractiveListSection(
                "Cart",
                new[]
                {
                    new WhatsAppInteractiveListRow(CartAddMoreId, "Add more items", "Return to menu"),
                    new WhatsAppInteractiveListRow(CartCheckoutId, "Checkout", "Confirm customer details"),
                    new WhatsAppInteractiveListRow(CartCancelId, "Cancel order", "Clear this cart")
                })
        };

        return new OutgoingWhatsAppReply(
            MenuTextFormatter.BuildCartSummary(draft),
            sections,
            "Cart options",
            "You can also type Add More, Checkout, or Cancel.");
    }

    private static OutgoingWhatsAppReply ContinueCheckout(Customer customer, Conversation conversation)
    {
        var draft = ReadDraft(conversation);

        if (draft.Items.Count == 0)
        {
            conversation.CurrentState = ConversationStates.OrderInputPending;
            return OutgoingWhatsAppReply.TextOnly("Your cart is empty. Please choose an item from the menu.");
        }

        if (string.IsNullOrWhiteSpace(draft.CustomerName) &&
            !string.IsNullOrWhiteSpace(customer.Name))
        {
            draft.CustomerName = customer.Name;
        }

        if (string.IsNullOrWhiteSpace(draft.Address) &&
            !string.IsNullOrWhiteSpace(customer.LastAddress))
        {
            draft.Address = customer.LastAddress;
        }

        conversation.TempOrderJson = JsonSerializer.Serialize(draft, JsonOptions);

        if (string.IsNullOrWhiteSpace(draft.CustomerName))
        {
            conversation.CurrentState = ConversationStates.CustomerNamePending;
            return OutgoingWhatsAppReply.TextOnly(MenuTextFormatter.BuildOrderSummary(draft));
        }

        if (string.IsNullOrWhiteSpace(draft.Address))
        {
            conversation.CurrentState = ConversationStates.AddressPending;
            return OutgoingWhatsAppReply.TextOnly(MenuTextFormatter.BuildAddressRequest(draft.CustomerName));
        }

        conversation.CurrentState = ConversationStates.ConfirmationPending;
        return OutgoingWhatsAppReply.TextOnly(MenuTextFormatter.BuildFinalConfirmation(draft));
    }

    private async Task<OutgoingWhatsAppReply> CaptureOrderAsync(
        Domain.Entities.Restaurant restaurant,
        Conversation conversation,
        string incomingText,
        CancellationToken cancellationToken)
    {
        var parsedItems = ParseOrderItems(incomingText);

        if (parsedItems.Count == 0)
        {
            conversation.CurrentState = ConversationStates.OrderInputPending;
            return OutgoingWhatsAppReply.TextOnly("""
                Please enter valid quantity.
                Example: Order: 1 x 2, 4 x 1
                """);
        }

        var requestedCodes = parsedItems.Select(x => x.ItemCode).Distinct().ToArray();
        var menuItems = await dbContext.MenuItems
            .Where(x => x.RestaurantId == restaurant.Id &&
                        x.IsActive &&
                        x.IsAvailable &&
                        requestedCodes.Contains(x.ItemCode))
            .ToDictionaryAsync(x => x.ItemCode, cancellationToken);

        if (menuItems.Count != requestedCodes.Length)
        {
            conversation.CurrentState = ConversationStates.OrderInputPending;
            return OutgoingWhatsAppReply.TextOnly("""
                Some items in your order are not valid.
                Please check the menu and try again.
                Example: Order: 1 x 2, 4 x 1
                """);
        }

        var draft = ReadDraft(conversation);

        foreach (var parsedItem in parsedItems)
        {
            if (parsedItem.Quantity <= 0)
            {
                conversation.CurrentState = ConversationStates.OrderInputPending;
                return OutgoingWhatsAppReply.TextOnly("""
                    Please enter valid quantity.
                    Example: Order: 1 x 2, 4 x 1
                    """);
            }

            var menuItem = menuItems[parsedItem.ItemCode];
            AddOrUpdateDraftItem(draft, menuItem, parsedItem.Quantity);
        }

        conversation.TempOrderJson = JsonSerializer.Serialize(draft, JsonOptions);
        conversation.CurrentState = ConversationStates.CartReview;

        return BuildCartReviewReply(draft);
    }

    private static void AddOrUpdateDraftItem(PendingOrderDraft draft, MenuItem menuItem, int quantity)
    {
        var existingItem = draft.Items.FirstOrDefault(x => x.MenuItemId == menuItem.Id);

        if (existingItem is null)
        {
            draft.Items.Add(new PendingOrderItemDraft
            {
                MenuItemId = menuItem.Id,
                ItemCode = menuItem.ItemCode,
                ItemName = menuItem.Name,
                UnitPrice = menuItem.Price,
                Quantity = quantity,
                LineTotal = menuItem.Price * quantity
            });
        }
        else
        {
            existingItem.Quantity += quantity;
            existingItem.LineTotal = existingItem.UnitPrice * existingItem.Quantity;
        }

        draft.TotalAmount = draft.Items.Sum(x => x.LineTotal);
    }

    private static string CaptureCustomerName(Customer customer, Conversation conversation, string name)
    {
        var draft = ReadDraft(conversation);
        draft.CustomerName = name.Trim();
        customer.Name = draft.CustomerName;

        if (!string.IsNullOrWhiteSpace(customer.LastAddress))
        {
            draft.Address = customer.LastAddress;
            conversation.TempOrderJson = JsonSerializer.Serialize(draft, JsonOptions);
            conversation.CurrentState = ConversationStates.ConfirmationPending;

            return MenuTextFormatter.BuildFinalConfirmation(draft);
        }

        conversation.TempOrderJson = JsonSerializer.Serialize(draft, JsonOptions);
        conversation.CurrentState = ConversationStates.AddressPending;

        return MenuTextFormatter.BuildAddressRequest(draft.CustomerName);
    }

    private static string CaptureAddress(Customer customer, Conversation conversation, string address)
    {
        var draft = ReadDraft(conversation);
        draft.Address = address.Trim();
        customer.LastAddress = draft.Address;

        conversation.TempOrderJson = JsonSerializer.Serialize(draft, JsonOptions);
        conversation.CurrentState = ConversationStates.ConfirmationPending;

        return MenuTextFormatter.BuildFinalConfirmation(draft);
    }

    private async Task<string> ConfirmOrCancelOrderAsync(
        Domain.Entities.Restaurant restaurant,
        Customer customer,
        Conversation conversation,
        string normalized,
        CancellationToken cancellationToken)
    {
        if (normalized is "no" or "n" or "cancel")
        {
            conversation.CurrentState = ConversationStates.Cancelled;
            conversation.IsActive = false;
            conversation.TempOrderJson = null;
            return "Your order has been cancelled. Reply Hi anytime to start again.";
        }

        if (normalized is not ("yes" or "y"))
        {
            return "Please reply YES to confirm or NO to cancel.";
        }

        var draft = ReadDraft(conversation);
        var orderNumber = await GenerateOrderNumberAsync(cancellationToken);
        var order = new Order
        {
            OrderNumber = orderNumber,
            RestaurantId = restaurant.Id,
            CustomerId = customer.Id,
            CustomerName = draft.CustomerName ?? customer.Name ?? "WhatsApp Customer",
            CustomerPhone = customer.PhoneNumber,
            Address = draft.Address ?? customer.LastAddress ?? "Pickup",
            OrderStatus = OrderStatuses.Confirmed,
            Source = OrderSources.WhatsApp,
            TotalAmount = draft.TotalAmount
        };

        foreach (var item in draft.Items)
        {
            order.Items.Add(new OrderItem
            {
                MenuItemId = item.MenuItemId,
                ItemName = item.ItemName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                LineTotal = item.LineTotal
            });
        }

        dbContext.Orders.Add(order);
        conversation.CurrentState = ConversationStates.Confirmed;
        conversation.IsActive = false;
        conversation.TempOrderJson = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await notificationService.NotifyOrderConfirmedAsync(restaurant, customer, order, order.Items.ToArray(), cancellationToken);
            order.OrderStatus = OrderStatuses.Notified;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify restaurant for order {OrderNumber}", order.OrderNumber);
            order.OrderStatus = OrderStatuses.Failed;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return MenuTextFormatter.BuildOrderReceived(order.OrderNumber);
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken cancellationToken)
    {
        var orderCount = await dbContext.Orders.CountAsync(cancellationToken);
        return $"VRC-{1001 + orderCount}";
    }

    private static PendingOrderDraft ReadDraft(Conversation conversation)
    {
        if (string.IsNullOrWhiteSpace(conversation.TempOrderJson))
        {
            return new PendingOrderDraft();
        }

        return JsonSerializer.Deserialize<PendingOrderDraft>(conversation.TempOrderJson, JsonOptions)
            ?? new PendingOrderDraft();
    }

    private static bool IsOrderInput(string text)
    {
        var normalized = text.Trim();
        return normalized.StartsWith("Order:", StringComparison.OrdinalIgnoreCase) ||
               OrderLineRegex().IsMatch(normalized);
    }

    private static bool IsGreetingInput(string normalized) =>
        normalized is "hi" or "hello" or "hey" or "start" or "restart";

    private static bool IsMenuInput(string normalized) =>
        normalized is "menu" or "main menu" or "view menu";

    private static bool IsAddMoreInput(string normalized) =>
        normalized is "add more" or "more" or "add" or "continue";

    private static bool IsCheckoutInput(string normalized) =>
        normalized is "checkout" or "done" or "finish" or "place order";

    private static bool IsCancelInput(string normalized) =>
        normalized is "cancel" or "no" or "stop";

    private static IReadOnlyCollection<(int ItemCode, int Quantity)> ParseOrderItems(string text)
    {
        var orderText = text.Trim();

        if (orderText.StartsWith("Order:", StringComparison.OrdinalIgnoreCase))
        {
            orderText = orderText["Order:".Length..].Trim();
        }

        var result = new List<(int ItemCode, int Quantity)>();

        foreach (var part in orderText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var match = OrderLineRegex().Match(part);

            if (!match.Success ||
                !int.TryParse(match.Groups["code"].Value, out var code) ||
                !int.TryParse(match.Groups["qty"].Value, out var quantity))
            {
                return Array.Empty<(int ItemCode, int Quantity)>();
            }

            result.Add((code, quantity));
        }

        return result;
    }

    [GeneratedRegex(@"^(?<code>\d+)\s*(?:x|-)\s*(?<qty>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex OrderLineRegex();

    private sealed record OutgoingWhatsAppReply(
        string Text,
        IReadOnlyCollection<WhatsAppInteractiveListSection> InteractiveSections,
        string? ButtonText = null,
        string? FooterText = null)
    {
        public static OutgoingWhatsAppReply TextOnly(string text) =>
            new(text, Array.Empty<WhatsAppInteractiveListSection>());
    }
}
