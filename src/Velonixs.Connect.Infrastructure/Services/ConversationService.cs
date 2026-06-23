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
    MenuSearchService menuSearchService,
    FreeTextOrderParser freeTextOrderParser,
    OrderingCartService cartService,
    ILogger<ConversationService> logger) : IConversationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

        if (!string.IsNullOrWhiteSpace(message.WhatsAppMessageId) &&
            await dbContext.MessageLogs.AnyAsync(
                x => x.WhatsAppMessageId == message.WhatsAppMessageId,
                cancellationToken))
        {
            return WhatsAppWebhookProcessResult.Ignored("Duplicate WhatsApp message.");
        }

        var restaurant = await dbContext.Restaurants.FirstOrDefaultAsync(
            x => x.WhatsAppPhoneNumberId == message.PhoneNumberId && x.IsActive,
            cancellationToken);
        if (restaurant is null)
        {
            return WhatsAppWebhookProcessResult.Ignored(
                $"No active restaurant found for phone number id '{message.PhoneNumberId}'.");
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

        OutgoingReply reply;
        try
        {
            reply = await BuildReplyAsync(
                restaurant,
                customer,
                conversation,
                message.MessageText.Trim(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to process WhatsApp ordering message for restaurant {RestaurantId} and conversation {ConversationId}",
                restaurant.Id,
                conversation.Id);
            reply = OutgoingReply.TextOnly(
                "Sorry, something went wrong while processing your request. Please type Menu to try again.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var sendResult = await SendReplyAsync(restaurant, customer, reply, cancellationToken);

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

        return new WhatsAppWebhookProcessResult(
            true,
            reply.Text,
            restaurant.Id,
            customer.Id,
            conversation.Id,
            conversation.CurrentState);
    }

    private async Task<OutgoingReply> BuildReplyAsync(
        Domain.Entities.Restaurant restaurant,
        Customer customer,
        Conversation conversation,
        string text,
        CancellationToken cancellationToken)
    {
        var normalized = text.ToLowerInvariant();
        var draft = ReadDraft(conversation);

        if (IsGreeting(normalized) || normalized == "main menu")
        {
            ResetDraft(conversation);
            return BuildWelcomeReply(restaurant);
        }

        if (normalized is "full menu" or "show full menu")
        {
            return await BuildFullMenuReplyAsync(restaurant, cancellationToken);
        }

        if (normalized is "menu" or "view menu" or "1" or "2" or "order" or "place order" or "category.list" or "main.view_menu")
        {
            return await BuildCategoryReplyAsync(restaurant, conversation, draft, 0, cancellationToken);
        }

        if (TryReadPageCommand(normalized, "category.page:", out var categoryPage))
        {
            return await BuildCategoryReplyAsync(
                restaurant,
                conversation,
                draft,
                categoryPage,
                cancellationToken);
        }

        if (TryReadGuidCommand(normalized, "category.select:", out var categoryId))
        {
            return await BuildItemsReplyAsync(
                restaurant,
                conversation,
                draft,
                categoryId,
                0,
                cancellationToken);
        }

        if (TryReadItemPageCommand(normalized, out categoryId, out var itemPage))
        {
            return await BuildItemsReplyAsync(
                restaurant,
                conversation,
                draft,
                categoryId,
                itemPage,
                cancellationToken);
        }

        if (TryReadGuidCommand(normalized, "item.select:", out var menuItemId))
        {
            return await BuildQuantityReplyAsync(
                restaurant,
                conversation,
                draft,
                menuItemId,
                cancellationToken);
        }

        if (normalized is "menu.continue")
        {
            return await BuildCurrentCategoryReplyAsync(
                restaurant,
                conversation,
                draft,
                cancellationToken);
        }

        if (normalized.StartsWith("select_item:", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(normalized["select_item:".Length..], out var legacyItemCode))
        {
            var legacyItem = await dbContext.MenuItems.AsNoTracking().FirstOrDefaultAsync(
                x => x.RestaurantId == restaurant.Id &&
                     x.ItemCode == legacyItemCode &&
                     x.IsActive &&
                     x.IsAvailable,
                cancellationToken);
            if (legacyItem is not null)
            {
                return await BuildQuantityReplyAsync(
                    restaurant,
                    conversation,
                    draft,
                    legacyItem.Id,
                    cancellationToken);
            }
        }

        if (TryReadQuantityCommand(normalized, out menuItemId, out var quantity))
        {
            return await AddItemAsync(
                restaurant,
                conversation,
                draft,
                menuItemId,
                quantity,
                cancellationToken);
        }

        if (TryReadGuidCommand(normalized, "quantity.custom:", out menuItemId))
        {
            draft.SelectedMenuItemId = menuItemId;
            SaveDraft(conversation, draft);
            conversation.CurrentState = ConversationStates.QuantitySelection;
            return OutgoingReply.TextOnly("Please type the quantity you need (for example: 6).");
        }

        if (conversation.CurrentState == ConversationStates.QuantitySelection &&
            draft.SelectedMenuItemId is Guid selectedItemId)
        {
            if (int.TryParse(normalized, out quantity) && quantity > 0 && quantity <= 100)
            {
                return await AddItemAsync(
                    restaurant,
                    conversation,
                    draft,
                    selectedItemId,
                    quantity,
                    cancellationToken);
            }

            return OutgoingReply.TextOnly("Please enter a quantity between 1 and 100.");
        }

        if (normalized is "cart.view" or "view cart" or "cart")
        {
            return BuildCartReply(conversation, draft);
        }

        if (normalized is "cart.add_more" or "add more" or "more")
        {
            return await BuildCategoryReplyAsync(restaurant, conversation, draft, 0, cancellationToken);
        }

        if (normalized is "cart.cancel" or "cancel" or "stop")
        {
            CancelConversation(conversation);
            return OutgoingReply.TextOnly("Your order has been cancelled. Reply Hi anytime to start again.");
        }

        if (normalized is "cart.checkout" or "checkout" or "done" or "finish")
        {
            return ContinueCheckout(customer, conversation, draft);
        }

        if (conversation.CurrentState == ConversationStates.CustomerNamePending)
        {
            draft.CustomerName = text;
            customer.Name = text;
            SaveDraft(conversation, draft);
            conversation.CurrentState = ConversationStates.FulfilmentPending;
            return BuildFulfilmentReply();
        }

        if (normalized == "checkout.delivery")
        {
            draft.IsPickup = false;
            draft.CheckoutState = "ADDRESS_PENDING";
            SaveDraft(conversation, draft);
            conversation.CurrentState = ConversationStates.AddressPending;
            return OutgoingReply.TextOnly("Please share your delivery address.");
        }

        if (normalized is "checkout.pickup" or "pickup")
        {
            draft.IsPickup = true;
            draft.Address = "Pickup";
            draft.CheckoutState = "CONFIRMATION_PENDING";
            SaveDraft(conversation, draft);
            conversation.CurrentState = ConversationStates.ConfirmationPending;
            return OutgoingReply.TextOnly(MenuTextFormatter.BuildFinalConfirmation(draft));
        }

        if (conversation.CurrentState == ConversationStates.AddressPending)
        {
            draft.IsPickup = false;
            draft.Address = text;
            draft.CheckoutState = "CONFIRMATION_PENDING";
            customer.LastAddress = text;
            SaveDraft(conversation, draft);
            conversation.CurrentState = ConversationStates.ConfirmationPending;
            return OutgoingReply.TextOnly(MenuTextFormatter.BuildFinalConfirmation(draft));
        }

        if (conversation.CurrentState == ConversationStates.ConfirmationPending)
        {
            return OutgoingReply.TextOnly(
                await ConfirmOrCancelAsync(
                    restaurant,
                    customer,
                    conversation,
                    draft,
                    normalized,
                    cancellationToken));
        }

        var legacyOrderLines = ParseLegacyOrder(text);
        if (legacyOrderLines.Count > 0)
        {
            var requestedCodes = legacyOrderLines.Select(x => x.ItemCode).Distinct().ToArray();
            var legacyItems = await dbContext.MenuItems.AsNoTracking()
                .Where(x => x.RestaurantId == restaurant.Id &&
                            x.IsActive &&
                            x.IsAvailable &&
                            requestedCodes.Contains(x.ItemCode))
                .ToDictionaryAsync(x => x.ItemCode, cancellationToken);
            if (legacyItems.Count == requestedCodes.Length)
            {
                foreach (var line in legacyOrderLines)
                {
                    cartService.AddOrUpdate(draft, legacyItems[line.ItemCode], line.Quantity);
                }

                SaveDraft(conversation, draft);
                return BuildCartReply(conversation, draft);
            }
        }

        if (normalized is "main.location" or "3" or "location" or "address")
        {
            return OutgoingReply.TextOnly(string.IsNullOrWhiteSpace(restaurant.Address)
                ? "Restaurant location is not configured yet. Please talk to staff."
                : restaurant.Address);
        }

        if (normalized is "main.staff" or "4" or "staff" or "help" or "talk to staff")
        {
            conversation.CurrentState = ConversationStates.StaffHandover;
            await notificationService.NotifyStaffHandoverAsync(
                restaurant,
                customer,
                text,
                cancellationToken);
            return OutgoingReply.TextOnly("Our staff has been notified and will contact you shortly.");
        }

        var activeItems = await LoadActiveItemsAsync(restaurant.Id, cancellationToken);
        var naturalOrder = freeTextOrderParser.Parse(text, activeItems);
        if (naturalOrder.IsHighConfidence)
        {
            foreach (var parsedItem in naturalOrder.Items)
            {
                cartService.AddOrUpdate(draft, parsedItem.Item, parsedItem.Quantity);
            }

            SaveDraft(conversation, draft);
            return BuildCartReply(conversation, draft);
        }

        if (naturalOrder.SuggestedItems.Count > 0)
        {
            conversation.CurrentState = ConversationStates.SearchSelection;
            return OutgoingReply.List(
                "I found a few possible matches. Please select the item you meant.",
                "Select item",
                WhatsAppOrderingMessageBuilder.BuildSearchSections(
                    naturalOrder.SuggestedItems.ToArray()));
        }

        var matches = menuSearchService.Search(text, activeItems);
        if (matches.Count == 1 && matches[0].Score >= 0.75)
        {
            return await BuildQuantityReplyAsync(
                restaurant,
                conversation,
                draft,
                matches[0].Item.Id,
                cancellationToken);
        }

        if (matches.Count > 0)
        {
            conversation.CurrentState = ConversationStates.SearchSelection;
            return OutgoingReply.List(
                "Select a matching menu item:",
                "View matches",
                WhatsAppOrderingMessageBuilder.BuildSearchSections(
                    matches.Select(x => x.Item).ToArray()));
        }

        var categoryReply = await BuildCategoryReplyAsync(
            restaurant,
            conversation,
            draft,
            0,
            cancellationToken);
        return categoryReply with
        {
            Text = $"I couldn't find \"{text}\" on the available menu. Please choose a category instead."
        };
    }

    private static OutgoingReply BuildWelcomeReply(Domain.Entities.Restaurant restaurant) =>
        OutgoingReply.ButtonReply(
            $"Welcome to {restaurant.Name}. Browse the menu or type an item name to search.",
            WhatsAppOrderingMessageBuilder.BuildMainMenuButtons(),
            "You can also type: 2 Paneer Pizza and 1 Veg Burger");

    private async Task<OutgoingReply> BuildCategoryReplyAsync(
        Domain.Entities.Restaurant restaurant,
        Conversation conversation,
        PendingOrderDraft draft,
        int page,
        CancellationToken cancellationToken)
    {
        var categories = await dbContext.MenuCategories
            .AsNoTracking()
            .Where(category => category.RestaurantId == restaurant.Id &&
                               category.IsActive &&
                               category.MenuItems.Any(item => item.IsActive && item.IsAvailable))
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .ToArrayAsync(cancellationToken);

        if (categories.Length == 0)
        {
            return OutgoingReply.TextOnly(
                "The menu is currently unavailable. Please type Help to contact restaurant staff.");
        }

        var maxPage = (categories.Length - 1) / WhatsAppOrderingMessageBuilder.CategoryPageSize;
        draft.CategoryPage = Math.Clamp(page, 0, maxPage);
        draft.SelectedCategoryId = null;
        draft.SelectedCategoryName = null;
        SaveDraft(conversation, draft);
        conversation.CurrentState = ConversationStates.CategorySelection;

        return OutgoingReply.List(
            $"Welcome to {restaurant.Name}. Please choose a category.",
            "Categories",
            WhatsAppOrderingMessageBuilder.BuildCategorySections(
                categories,
                draft.CategoryPage,
                draft.Items.Count > 0),
            "Or type an item name to search.");
    }

    private async Task<OutgoingReply> BuildItemsReplyAsync(
        Domain.Entities.Restaurant restaurant,
        Conversation conversation,
        PendingOrderDraft draft,
        Guid categoryId,
        int page,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.MenuCategories.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == categoryId && x.RestaurantId == restaurant.Id && x.IsActive,
            cancellationToken);
        if (category is null)
        {
            return await BuildCategoryReplyAsync(
                restaurant,
                conversation,
                draft,
                0,
                cancellationToken);
        }

        var items = await dbContext.MenuItems.AsNoTracking()
            .Where(item => item.RestaurantId == restaurant.Id &&
                           item.CategoryId == category.Id &&
                           item.IsActive &&
                           item.IsAvailable)
            .OrderBy(item => item.ItemCode)
            .ThenBy(item => item.Name)
            .ToArrayAsync(cancellationToken);
        if (items.Length == 0)
        {
            return await BuildCategoryReplyAsync(
                restaurant,
                conversation,
                draft,
                0,
                cancellationToken);
        }

        var maxPage = (items.Length - 1) / WhatsAppOrderingMessageBuilder.ItemPageSize;
        draft.SelectedCategoryId = category.Id;
        draft.SelectedCategoryName = category.Name;
        draft.ItemPage = Math.Clamp(page, 0, maxPage);
        SaveDraft(conversation, draft);
        conversation.CurrentState = ConversationStates.ItemSelection;

        return OutgoingReply.List(
            $"{category.Name} items",
            "Select item",
            WhatsAppOrderingMessageBuilder.BuildItemSections(
                category.Id,
                items,
                draft.ItemPage,
                draft.Items.Count > 0),
            "Only currently available items are shown.");
    }

    private async Task<OutgoingReply> BuildQuantityReplyAsync(
        Domain.Entities.Restaurant restaurant,
        Conversation conversation,
        PendingOrderDraft draft,
        Guid menuItemId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.MenuItems.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == menuItemId &&
                 x.RestaurantId == restaurant.Id &&
                 x.IsActive &&
                 x.IsAvailable,
            cancellationToken);
        if (item is null)
        {
            return await BuildCategoryReplyAsync(
                restaurant,
                conversation,
                draft,
                0,
                cancellationToken);
        }

        if (draft.SelectedCategoryId != item.CategoryId)
        {
            var category = await dbContext.MenuCategories.AsNoTracking().FirstOrDefaultAsync(
                x => x.Id == item.CategoryId &&
                     x.RestaurantId == restaurant.Id &&
                     x.IsActive,
                cancellationToken);
            draft.SelectedCategoryId = item.CategoryId;
            draft.SelectedCategoryName = category?.Name;
            draft.ItemPage = 0;
        }

        draft.SelectedMenuItemId = item.Id;
        SaveDraft(conversation, draft);
        conversation.CurrentState = ConversationStates.QuantitySelection;
        return OutgoingReply.List(
            $"{item.Name} - Rs {item.Price:0.##}. Choose quantity.",
            "Quantity",
            WhatsAppOrderingMessageBuilder.BuildQuantitySections(item),
            "Choose Custom quantity to type another amount.");
    }

    private async Task<OutgoingReply> AddItemAsync(
        Domain.Entities.Restaurant restaurant,
        Conversation conversation,
        PendingOrderDraft draft,
        Guid menuItemId,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0 || quantity > 100)
        {
            return OutgoingReply.TextOnly("Please choose a quantity between 1 and 100.");
        }

        var item = await dbContext.MenuItems.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == menuItemId &&
                 x.RestaurantId == restaurant.Id &&
                 x.IsActive &&
                 x.IsAvailable,
            cancellationToken);
        if (item is null)
        {
            return OutgoingReply.TextOnly(
                "That item is no longer available. Type Menu to choose another item.");
        }

        cartService.AddOrUpdate(draft, item, quantity);
        draft.SelectedMenuItemId = null;
        SaveDraft(conversation, draft);
        conversation.CurrentState = ConversationStates.ItemSelection;
        return OutgoingReply.List(
            $"✓ {item.Name} x{quantity} added.\n\nWhat would you like to do?",
            "Continue",
            WhatsAppOrderingMessageBuilder.BuildItemAddedSections(draft.SelectedCategoryName),
            $"Cart total: Rs {draft.TotalAmount:0.##}");
    }

    private Task<OutgoingReply> BuildCurrentCategoryReplyAsync(
        Domain.Entities.Restaurant restaurant,
        Conversation conversation,
        PendingOrderDraft draft,
        CancellationToken cancellationToken) =>
        draft.SelectedCategoryId is Guid categoryId
            ? BuildItemsReplyAsync(
                restaurant,
                conversation,
                draft,
                categoryId,
                draft.ItemPage,
                cancellationToken)
            : BuildCategoryReplyAsync(
                restaurant,
                conversation,
                draft,
                draft.CategoryPage,
                cancellationToken);

    private static OutgoingReply BuildCartReply(
        Conversation conversation,
        PendingOrderDraft draft)
    {
        if (draft.Items.Count == 0)
        {
            conversation.CurrentState = ConversationStates.CategorySelection;
            return OutgoingReply.ButtonReply(
                "Your cart is empty. Browse the menu to add an item.",
                new[] { new WhatsAppReplyButton("category.list", "Browse Menu") });
        }

        conversation.CurrentState = ConversationStates.CartReview;
        return OutgoingReply.ButtonReply(
            MenuTextFormatter.BuildCartSummary(draft),
            WhatsAppOrderingMessageBuilder.BuildCartButtons());
    }

    private static OutgoingReply ContinueCheckout(
        Customer customer,
        Conversation conversation,
        PendingOrderDraft draft)
    {
        if (draft.Items.Count == 0)
        {
            return BuildCartReply(conversation, draft);
        }

        draft.CustomerName ??= customer.Name;
        draft.CheckoutState = string.IsNullOrWhiteSpace(draft.CustomerName)
            ? "CUSTOMER_NAME_PENDING"
            : "FULFILMENT_PENDING";
        SaveDraft(conversation, draft);

        if (string.IsNullOrWhiteSpace(draft.CustomerName))
        {
            conversation.CurrentState = ConversationStates.CustomerNamePending;
            return OutgoingReply.TextOnly(
                $"{MenuTextFormatter.BuildCartSummary(draft)}\n\nPlease reply with your name.");
        }

        conversation.CurrentState = ConversationStates.FulfilmentPending;
        return BuildFulfilmentReply();
    }

    private static OutgoingReply BuildFulfilmentReply() =>
        OutgoingReply.ButtonReply(
            "Is this order for delivery or pickup?",
            WhatsAppOrderingMessageBuilder.BuildFulfilmentButtons());

    private async Task<string> ConfirmOrCancelAsync(
        Domain.Entities.Restaurant restaurant,
        Customer customer,
        Conversation conversation,
        PendingOrderDraft draft,
        string normalized,
        CancellationToken cancellationToken)
    {
        if (normalized is "no" or "n" or "cancel")
        {
            CancelConversation(conversation);
            return "Your order has been cancelled. Reply Hi anytime to start again.";
        }

        if (normalized is not ("yes" or "y"))
        {
            return "Please reply YES to confirm or NO to cancel.";
        }

        if (draft.Items.Count == 0)
        {
            conversation.CurrentState = ConversationStates.CategorySelection;
            return "Your cart is empty. Type Menu to start again.";
        }

        var order = new Order
        {
            OrderNumber = await GenerateOrderNumberAsync(cancellationToken),
            RestaurantId = restaurant.Id,
            CustomerId = customer.Id,
            CustomerName = draft.CustomerName ?? customer.Name ?? "WhatsApp Customer",
            CustomerPhone = customer.PhoneNumber,
            Address = draft.IsPickup ? "Pickup" : draft.Address ?? "Pickup",
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
            await notificationService.NotifyOrderConfirmedAsync(
                restaurant,
                customer,
                order,
                order.Items.ToArray(),
                cancellationToken);
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

    private async Task<OutgoingReply> BuildFullMenuReplyAsync(
        Domain.Entities.Restaurant restaurant,
        CancellationToken cancellationToken)
    {
        var categories = await dbContext.MenuCategories.AsNoTracking()
            .Where(x => x.RestaurantId == restaurant.Id && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
        var items = await LoadActiveItemsAsync(restaurant.Id, cancellationToken);
        return OutgoingReply.TextOnly(
            MenuTextFormatter.BuildMenuMessage(restaurant, categories, items));
    }

    private Task<MenuItem[]> LoadActiveItemsAsync(
        Guid restaurantId,
        CancellationToken cancellationToken) =>
        dbContext.MenuItems.AsNoTracking()
            .Where(x => x.RestaurantId == restaurantId && x.IsActive && x.IsAvailable)
            .OrderBy(x => x.ItemCode)
            .ToArrayAsync(cancellationToken);

    private async Task<Customer> GetOrCreateCustomerAsync(
        Guid restaurantId,
        IncomingWhatsAppMessage message,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(
            x => x.RestaurantId == restaurantId && x.PhoneNumber == message.FromPhoneNumber,
            cancellationToken);
        if (customer is null)
        {
            customer = new Customer
            {
                RestaurantId = restaurantId,
                PhoneNumber = message.FromPhoneNumber,
                Name = message.ProfileName
            };
            dbContext.Customers.Add(customer);
        }
        else
        {
            customer.Name ??= message.ProfileName;
            customer.LastInteractionAt = DateTimeOffset.UtcNow;
        }

        return customer;
    }

    private async Task<Conversation> GetOrCreateConversationAsync(
        Guid restaurantId,
        Customer customer,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations.FirstOrDefaultAsync(
            x => x.RestaurantId == restaurantId && x.CustomerId == customer.Id && x.IsActive,
            cancellationToken);
        if (conversation is null)
        {
            conversation = new Conversation
            {
                RestaurantId = restaurantId,
                CustomerId = customer.Id,
                WhatsAppNumber = customer.PhoneNumber
            };
            dbContext.Conversations.Add(conversation);
        }

        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        return conversation;
    }

    private async Task<WhatsAppSendResult> SendReplyAsync(
        Domain.Entities.Restaurant restaurant,
        Customer customer,
        OutgoingReply reply,
        CancellationToken cancellationToken)
    {
        if (reply.Sections.Count > 0)
        {
            return await whatsAppMessageSender.SendInteractiveListMessageAsync(
                restaurant.WhatsAppPhoneNumberId,
                customer.PhoneNumber,
                reply.Text,
                reply.ButtonText ?? "Select",
                reply.Sections,
                reply.FooterText,
                cancellationToken);
        }

        if (reply.Buttons.Count > 0)
        {
            return await whatsAppMessageSender.SendReplyButtonMessageAsync(
                restaurant.WhatsAppPhoneNumberId,
                customer.PhoneNumber,
                reply.Text,
                reply.Buttons,
                reply.FooterText,
                cancellationToken);
        }

        return await whatsAppMessageSender.SendTextMessageAsync(
            restaurant.WhatsAppPhoneNumberId,
            customer.PhoneNumber,
            reply.Text,
            cancellationToken);
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken cancellationToken)
    {
        var count = await dbContext.Orders.CountAsync(cancellationToken);
        return $"VRC-{1001 + count}";
    }

    private static PendingOrderDraft ReadDraft(Conversation conversation)
    {
        if (string.IsNullOrWhiteSpace(conversation.TempOrderJson))
        {
            return new PendingOrderDraft();
        }

        return JsonSerializer.Deserialize<PendingOrderDraft>(
                   conversation.TempOrderJson,
                   JsonOptions)
               ?? new PendingOrderDraft();
    }

    private static void SaveDraft(Conversation conversation, PendingOrderDraft draft) =>
        conversation.TempOrderJson = JsonSerializer.Serialize(draft, JsonOptions);

    private static void ResetDraft(Conversation conversation)
    {
        conversation.CurrentState = ConversationStates.New;
        conversation.IsActive = true;
        conversation.TempOrderJson = null;
    }

    private static void CancelConversation(Conversation conversation)
    {
        conversation.CurrentState = ConversationStates.Cancelled;
        conversation.IsActive = false;
        conversation.TempOrderJson = null;
    }

    private static bool IsGreeting(string normalized) =>
        normalized is "hi" or "hello" or "hey" or "start" or "restart";

    private static IReadOnlyCollection<(int ItemCode, int Quantity)> ParseLegacyOrder(string text)
    {
        var value = text.Trim();
        if (value.StartsWith("Order:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["Order:".Length..].Trim();
        }
        else if (!LegacyOrderLineRegex().IsMatch(value))
        {
            return Array.Empty<(int, int)>();
        }

        var result = new List<(int, int)>();
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var match = LegacyOrderLineRegex().Match(part);
            if (!match.Success ||
                !int.TryParse(match.Groups["code"].Value, out var code) ||
                !int.TryParse(match.Groups["quantity"].Value, out var quantity) ||
                quantity <= 0)
            {
                return Array.Empty<(int, int)>();
            }

            result.Add((code, quantity));
        }

        return result;
    }

    private static bool TryReadGuidCommand(
        string text,
        string prefix,
        out Guid id)
    {
        id = Guid.Empty;
        return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParse(text[prefix.Length..], out id);
    }

    private static bool TryReadPageCommand(
        string text,
        string prefix,
        out int page)
    {
        page = 0;
        return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(text[prefix.Length..], out page);
    }

    private static bool TryReadItemPageCommand(
        string text,
        out Guid categoryId,
        out int page)
    {
        categoryId = Guid.Empty;
        page = 0;
        var match = ItemPageRegex().Match(text);
        return Guid.TryParse(match.Groups["category"].Value, out categoryId) &&
               int.TryParse(match.Groups["page"].Value, out page);
    }

    private static bool TryReadQuantityCommand(
        string text,
        out Guid itemId,
        out int quantity)
    {
        itemId = Guid.Empty;
        quantity = 0;
        var match = QuantityRegex().Match(text);
        return Guid.TryParse(match.Groups["item"].Value, out itemId) &&
               int.TryParse(match.Groups["quantity"].Value, out quantity);
    }

    [GeneratedRegex(@"^item\.page:(?<category>[0-9a-f-]+):(?<page>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ItemPageRegex();

    [GeneratedRegex(@"^quantity\.select:(?<item>[0-9a-f-]+):(?<quantity>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex QuantityRegex();

    [GeneratedRegex(@"^(?<code>\d+)\s*(?:x|-)\s*(?<quantity>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LegacyOrderLineRegex();

    private sealed record OutgoingReply(
        string Text,
        IReadOnlyCollection<WhatsAppInteractiveListSection> Sections,
        IReadOnlyCollection<WhatsAppReplyButton> Buttons,
        string? ButtonText = null,
        string? FooterText = null)
    {
        public static OutgoingReply TextOnly(string text) =>
            new(
                text,
                Array.Empty<WhatsAppInteractiveListSection>(),
                Array.Empty<WhatsAppReplyButton>());

        public static OutgoingReply List(
            string text,
            string buttonText,
            IReadOnlyCollection<WhatsAppInteractiveListSection> sections,
            string? footerText = null) =>
            new(
                text,
                sections,
                Array.Empty<WhatsAppReplyButton>(),
                buttonText,
                footerText);

        public static OutgoingReply ButtonReply(
            string text,
            IReadOnlyCollection<WhatsAppReplyButton> buttons,
            string? footerText = null) =>
            new(
                text,
                Array.Empty<WhatsAppInteractiveListSection>(),
                buttons,
                FooterText: footerText);
    }
}
