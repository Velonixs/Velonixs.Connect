using System.Diagnostics;
using System.Text;
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
        var stopwatch = Stopwatch.StartNew();

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

        if (string.IsNullOrWhiteSpace(message.WhatsAppMessageId) &&
            await IsRecentDuplicateIncomingMessageAsync(restaurant.Id, customer.Id, message, cancellationToken))
        {
            return WhatsAppWebhookProcessResult.Ignored("Duplicate WhatsApp message.");
        }

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
        await dbContext.SaveChangesAsync(cancellationToken);

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
        var sendStopwatch = Stopwatch.StartNew();
        WhatsAppSendResult sendResult;
        try
        {
            sendResult = await SendReplyAsync(restaurant, customer, reply, cancellationToken);
        }
        catch (Exception ex)
        {
            sendStopwatch.Stop();
            logger.LogError(
                ex,
                "Failed to send WhatsApp reply after {ElapsedMilliseconds} ms for restaurant {RestaurantId} and conversation {ConversationId}",
                sendStopwatch.ElapsedMilliseconds,
                restaurant.Id,
                conversation.Id);
            sendResult = new WhatsAppSendResult(false, false, Error: ex.Message);
        }
        sendStopwatch.Stop();

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
        stopwatch.Stop();

        logger.LogInformation(
            "Processed WhatsApp message in {ElapsedMilliseconds} ms. SendElapsedMilliseconds={SendElapsedMilliseconds}, RestaurantId={RestaurantId}, ConversationId={ConversationId}",
            stopwatch.ElapsedMilliseconds,
            sendStopwatch.ElapsedMilliseconds,
            restaurant.Id,
            conversation.Id);

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
            return await BuildNumberedMenuReplyAsync(
                restaurant,
                conversation,
                ReadDraft(conversation),
                cancellationToken);
        }

        if (normalized is "full menu" or "show full menu")
        {
            return await BuildFullMenuReplyAsync(restaurant, cancellationToken);
        }

        if ((conversation.CurrentState == ConversationStates.ItemSelection ||
             draft.CurrentStep == ConversationStates.ItemSelection) &&
            int.TryParse(normalized, out var selectedItemNumber))
        {
            return await SelectNumberedMenuItemAsync(
                restaurant,
                conversation,
                draft,
                selectedItemNumber,
                cancellationToken);
        }

        if (normalized is "menu" or "view menu" or "order" or "place order" or "category.list" or "main.view_menu")
        {
            return await BuildNumberedMenuReplyAsync(restaurant, conversation, draft, cancellationToken, page: 0);
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
                conversation.CurrentState == ConversationStates.SearchSelection,
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

        if (normalized is "menu.next" or "next" or "menu.more" or "view more items" or "more items")
        {
            return await BuildNumberedMenuReplyAsync(
                restaurant,
                conversation,
                draft,
                cancellationToken,
                page: draft.CurrentMenuPage + 1);
        }

        if (normalized is "menu.previous" or "previous" or "prev")
        {
            return await BuildNumberedMenuReplyAsync(
                restaurant,
                conversation,
                draft,
                cancellationToken,
                page: draft.CurrentMenuPage - 1);
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
                    false,
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
            if (draft.Items.Count == 0)
            {
                return await BuildNumberedMenuReplyAsync(
                    restaurant,
                    conversation,
                    draft,
                    cancellationToken,
                    "Your cart is currently empty.\n\nSelect an item from the menu to begin your order.");
            }

            return BuildCartReply(conversation, draft);
        }

        if (normalized is "cart.add_more" or "add more" or "more")
        {
            return await BuildNumberedMenuReplyAsync(restaurant, conversation, draft, cancellationToken, page: 0);
        }

        if (normalized is "cart.cancel" or "cancel" or "stop")
        {
            CancelConversation(conversation);
            return OutgoingReply.TextOnly("Your order has been cancelled. Reply Hi anytime to start again.");
        }

        if (normalized is "cart.checkout" or "checkout" or "done" or "finish")
        {
            if (draft.Items.Count == 0)
            {
                return await BuildNumberedMenuReplyAsync(
                    restaurant,
                    conversation,
                    draft,
                    cancellationToken,
                    "Your cart is currently empty.\n\nSelect an item from the menu to begin your order.",
                    page: draft.CurrentMenuPage);
            }

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

            var savedAddress = ResolveSavedDeliveryAddress(customer, draft);
            if (!string.IsNullOrWhiteSpace(savedAddress))
            {
                draft.Address = savedAddress;
                draft.CheckoutState = "CONFIRMATION_PENDING";
                SaveDraft(conversation, draft);
                conversation.CurrentState = ConversationStates.ConfirmationPending;
                return BuildConfirmationReply(draft);
            }

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
            return BuildConfirmationReply(draft);
        }

        if (conversation.CurrentState == ConversationStates.AddressPending)
        {
            draft.IsPickup = false;
            draft.Address = text;
            draft.CheckoutState = "CONFIRMATION_PENDING";
            customer.LastAddress = text;
            SaveDraft(conversation, draft);
            conversation.CurrentState = ConversationStates.ConfirmationPending;
            return BuildConfirmationReply(draft);
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
                    cartService.AddOrUpdate(
                        draft,
                        legacyItems[line.ItemCode],
                        line.Quantity,
                        restaurant.CgstPercent,
                        restaurant.SgstPercent);
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

        if (normalized is "main.staff" or "4" or "staff" or "help" or "talk to staff" or
            "connect restaurant" or "connect to restaurant" or "connect to restaurent")
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
                cartService.AddOrUpdate(
                    draft,
                    parsedItem.Item,
                    parsedItem.Quantity,
                    restaurant.CgstPercent,
                    restaurant.SgstPercent);
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
                false,
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

        var categoryReply = await BuildNumberedMenuReplyAsync(
            restaurant,
            conversation,
            draft,
            cancellationToken,
            $"I couldn't find \"{text}\" on the available menu. Please choose an item below.",
            page: 0);
        return categoryReply with
        {
            Text = categoryReply.Text
        };
    }

    private async Task<OutgoingReply> BuildNumberedMenuReplyAsync(
        Domain.Entities.Restaurant restaurant,
        Conversation conversation,
        PendingOrderDraft draft,
        CancellationToken cancellationToken,
        string? prefix = null,
        Guid? preferredCategoryId = null,
        int? page = null)
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
            conversation.CurrentState = ConversationStates.CategorySelection;
            return OutgoingReply.TextOnly(
                "The menu is currently unavailable. Please connect with the restaurant.");
        }

        var categoryOrder = categories
            .Select((category, index) => new { category.Id, Index = index })
            .ToDictionary(x => x.Id, x => x.Index);
        var categoryIds = categories.Select(category => category.Id).ToArray();
        var availableItems = await dbContext.MenuItems.AsNoTracking()
            .Where(item => item.RestaurantId == restaurant.Id &&
                           categoryIds.Contains(item.CategoryId) &&
                           item.IsActive &&
                           item.IsAvailable)
            .ToArrayAsync(cancellationToken);

        if (availableItems.Length == 0)
        {
            return await BuildCategoryReplyAsync(
                restaurant,
                conversation,
                draft,
                0,
                cancellationToken);
        }

        var categoryById = categories.ToDictionary(category => category.Id);
        var allEntries = availableItems
            .Where(item => categoryById.ContainsKey(item.CategoryId))
            .OrderBy(item => categoryOrder[item.CategoryId])
            .ThenBy(item => item.ItemCode)
            .ThenBy(item => item.Name)
            .Select(item => (Category: categoryById[item.CategoryId], Item: item))
            .ToArray();
        var totalPages = Math.Max(
            1,
            (int)Math.Ceiling(allEntries.Length / (double)WhatsAppOrderingMessageBuilder.NumberedMenuPageSize));
        var requestedPage = page ?? draft.CurrentMenuPage;

        if (preferredCategoryId.HasValue && !page.HasValue)
        {
            var preferredIndex = Array.FindIndex(allEntries, entry => entry.Item.CategoryId == preferredCategoryId.Value);
            if (preferredIndex >= 0)
            {
                requestedPage = preferredIndex / WhatsAppOrderingMessageBuilder.NumberedMenuPageSize;
            }
        }

        var safePage = Math.Clamp(requestedPage, 0, totalPages - 1);
        var pageEntries = allEntries
            .Skip(safePage * WhatsAppOrderingMessageBuilder.NumberedMenuPageSize)
            .Take(WhatsAppOrderingMessageBuilder.NumberedMenuPageSize)
            .ToArray();
        var category = pageEntries[0].Category;

        draft.CurrentRestaurantId = restaurant.Id;
        draft.CurrentCategoryId = category.Id;
        draft.CurrentMenuPage = safePage;
        draft.CurrentStep = ConversationStates.ItemSelection;
        draft.CurrentMenuItemIds = pageEntries.Select(entry => entry.Item.Id).ToList();
        draft.SelectedCategoryId = category.Id;
        draft.SelectedCategoryName = category.Name;
        draft.ItemPage = safePage;
        SaveDraft(conversation, draft);
        conversation.CurrentState = ConversationStates.ItemSelection;

        logger.LogInformation(
            "Loaded numbered WhatsApp menu for restaurant {RestaurantId}, page {Page}, rows {ItemCount}",
            restaurant.Id,
            safePage,
            pageEntries.Length);

        var itemAddedPrefix = prefix?.Contains("Added to cart", StringComparison.OrdinalIgnoreCase) == true;
        var startNumber = safePage * WhatsAppOrderingMessageBuilder.NumberedMenuPageSize + 1;
        return OutgoingReply.ButtonReply(
            WhatsAppOrderingMessageBuilder.BuildNumberedMenuText(
                restaurant.Name,
                pageEntries,
                safePage,
                totalPages,
                prefix,
                startNumber,
                showWelcome: !itemAddedPrefix,
                instruction: itemAddedPrefix
                    ? "Want to add more item? Select an item by replying with its number."
                    : null),
            WhatsAppOrderingMessageBuilder.BuildMenuNavigationButtons(safePage, totalPages),
            null);
    }

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
        draft.CurrentRestaurantId = restaurant.Id;
        draft.CurrentCategoryId = null;
        draft.CurrentMenuPage = 0;
        draft.CurrentStep = ConversationStates.CategorySelection;
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
        draft.CurrentRestaurantId = restaurant.Id;
        draft.CurrentCategoryId = category.Id;
        draft.CurrentMenuPage = Math.Clamp(page, 0, maxPage);
        draft.CurrentStep = ConversationStates.ItemSelection;
        draft.SelectedCategoryId = category.Id;
        draft.SelectedCategoryName = category.Name;
        draft.ItemPage = draft.CurrentMenuPage;
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
        bool selectedFromSearch,
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

        var selectedFromNumberedMenu = draft.CurrentMenuItemIds.Contains(item.Id);
        var returnMenuPage = selectedFromNumberedMenu ? draft.CurrentMenuPage : draft.ItemPage;

        if (draft.SelectedCategoryId != item.CategoryId)
        {
            var category = await dbContext.MenuCategories.AsNoTracking().FirstOrDefaultAsync(
                x => x.Id == item.CategoryId &&
                     x.RestaurantId == restaurant.Id &&
                     x.IsActive,
                cancellationToken);
            draft.SelectedCategoryId = item.CategoryId;
            draft.SelectedCategoryName = category?.Name;
        }

        draft.CurrentRestaurantId = restaurant.Id;
        draft.CurrentCategoryId = item.CategoryId;
        draft.CurrentMenuPage = selectedFromSearch ? 0 : returnMenuPage;
        draft.CurrentStep = ConversationStates.QuantitySelection;
        draft.SelectedMenuItemId = item.Id;
        draft.ReturnToDefaultMenuAfterQuantity = selectedFromSearch;
        SaveDraft(conversation, draft);
        conversation.CurrentState = ConversationStates.QuantitySelection;
        logger.LogInformation(
            "Selected WhatsApp menu item {MenuItemId} for restaurant {RestaurantId}",
            item.Id,
            restaurant.Id);

        return OutgoingReply.ButtonReply(
            $"{item.Name} — Rs {item.Price:0.##}\n\nSelect a quantity below or type any quantity, such as 4 or 10.",
            WhatsAppOrderingMessageBuilder.BuildQuantityButtons(item.Id));
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

        cartService.AddOrUpdate(draft, item, quantity, restaurant.CgstPercent, restaurant.SgstPercent);
        draft.CurrentStep = ConversationStates.ItemSelection;
        var shouldReturnToDefaultMenu = draft.ReturnToDefaultMenuAfterQuantity;
        draft.ReturnToDefaultMenuAfterQuantity = false;
        draft.SelectedMenuItemId = null;
        SaveDraft(conversation, draft);
        conversation.CurrentState = ConversationStates.ItemSelection;
        return await BuildNumberedMenuReplyAsync(
            restaurant,
            conversation,
            draft,
            cancellationToken,
            $"✅ Added to cart\n\n{quantity} × {item.Name}\nCart total: Rs {draft.TotalAmount:0.##}",
            shouldReturnToDefaultMenu ? null : item.CategoryId,
            shouldReturnToDefaultMenu ? 0 : draft.CurrentMenuPage);
    }

    private async Task<OutgoingReply> SelectNumberedMenuItemAsync(
        Domain.Entities.Restaurant restaurant,
        Conversation conversation,
        PendingOrderDraft draft,
        int itemNumber,
        CancellationToken cancellationToken)
    {
        var globalStartNumber = draft.CurrentMenuPage * WhatsAppOrderingMessageBuilder.NumberedMenuPageSize + 1;
        var itemIndex = itemNumber >= globalStartNumber
            ? itemNumber - globalStartNumber
            : itemNumber - 1;

        if (itemIndex < 0 || itemIndex >= draft.CurrentMenuItemIds.Count)
        {
            return await BuildNumberedMenuReplyAsync(
                restaurant,
                conversation,
                draft,
                cancellationToken,
                "Please choose a valid item number from this menu page.",
                page: draft.CurrentMenuPage);
        }

        var menuItemId = draft.CurrentMenuItemIds[itemIndex];
        return await BuildQuantityReplyAsync(
            restaurant,
            conversation,
            draft,
            menuItemId,
            false,
            cancellationToken);
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

    private async Task<OutgoingReply> BuildMoreItemsReplyAsync(
        Domain.Entities.Restaurant restaurant,
        Conversation conversation,
        PendingOrderDraft draft,
        CancellationToken cancellationToken)
    {
        if (draft.SelectedCategoryId is Guid categoryId)
        {
            var categoryItemCount = await dbContext.MenuItems.AsNoTracking()
                .CountAsync(
                    item => item.RestaurantId == restaurant.Id &&
                            item.CategoryId == categoryId &&
                            item.IsActive &&
                            item.IsAvailable,
                    cancellationToken);

            if (categoryItemCount > WhatsAppOrderingMessageBuilder.DirectMenuItemCount)
            {
                return await BuildItemsReplyAsync(
                    restaurant,
                    conversation,
                    draft,
                    categoryId,
                    1,
                    cancellationToken);
            }
        }

        return await BuildCategoryReplyAsync(
            restaurant,
            conversation,
            draft,
            0,
            cancellationToken);
    }

    private static OutgoingReply BuildCartReply(
        Conversation conversation,
        PendingOrderDraft draft)
    {
        if (draft.Items.Count == 0)
        {
            conversation.CurrentState = ConversationStates.CategorySelection;
            return OutgoingReply.ButtonReply(
                "Your cart is currently empty.\n\nSelect an item from the menu to begin your order.",
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
            "How would you like to receive your order?",
            WhatsAppOrderingMessageBuilder.BuildFulfilmentButtons());

    private static OutgoingReply BuildConfirmationReply(PendingOrderDraft draft) =>
        OutgoingReply.ButtonReply(
            MenuTextFormatter.BuildFinalConfirmation(draft),
            WhatsAppOrderingMessageBuilder.BuildConfirmationButtons());

    private static string? ResolveSavedDeliveryAddress(
        Customer customer,
        PendingOrderDraft draft)
    {
        if (!string.IsNullOrWhiteSpace(draft.Address) &&
            !string.Equals(draft.Address, "Pickup", StringComparison.OrdinalIgnoreCase))
        {
            return draft.Address;
        }

        return string.IsNullOrWhiteSpace(customer.LastAddress)
            ? null
            : customer.LastAddress;
    }

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
            return "Your cart is currently empty.\n\nSelect an item from the menu to begin your order.";
        }

        var order = new Order
        {
            OrderNumber = await GenerateOrderNumberAsync(cancellationToken),
            RestaurantId = restaurant.Id,
            CustomerId = customer.Id,
            CustomerName = draft.CustomerName ?? customer.Name ?? "WhatsApp Customer",
            CustomerPhone = customer.PhoneNumber,
            Address = draft.IsPickup ? "Pickup" : draft.Address ?? "Pickup",
            OrderStatus = OrderStatuses.PendingConfirmation,
            Source = OrderSources.WhatsApp,
            SubTotalAmount = draft.SubTotalAmount,
            CgstPercent = draft.CgstPercent,
            CgstAmount = draft.CgstAmount,
            SgstPercent = draft.SgstPercent,
            SgstAmount = draft.SgstAmount,
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

        order.StatusHistory.Add(new OrderStatusHistory
        {
            PreviousStatus = OrderStatuses.Draft,
            NewStatus = OrderStatuses.PendingConfirmation,
            Comment = "Customer confirmed order.",
            UpdatedBy = customer.Name ?? customer.PhoneNumber,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify restaurant for order {OrderNumber}", order.OrderNumber);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MenuTextFormatter.BuildOrderReceived(
            order.OrderNumber,
            order.CustomerName,
            restaurant.Name);
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

    private Task<bool> IsRecentDuplicateIncomingMessageAsync(
        Guid restaurantId,
        Guid customerId,
        IncomingWhatsAppMessage message,
        CancellationToken cancellationToken)
    {
        var receivedAt = message.ReceivedAt ?? DateTimeOffset.UtcNow;
        var duplicateWindowStart = receivedAt.AddSeconds(-10);

        return dbContext.MessageLogs.AnyAsync(
            x => x.RestaurantId == restaurantId &&
                 x.CustomerId == customerId &&
                 x.Direction == MessageDirections.Incoming &&
                 x.MessageText == message.MessageText &&
                 x.CreatedAt >= duplicateWindowStart,
            cancellationToken);
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
            var listResult = await whatsAppMessageSender.SendInteractiveListMessageAsync(
                restaurant.WhatsAppPhoneNumberId,
                customer.PhoneNumber,
                reply.Text,
                reply.ButtonText ?? "Select",
                reply.Sections,
                reply.FooterText,
                cancellationToken);

            if (listResult.IsSuccess)
            {
                return listResult;
            }

            logger.LogWarning(
                "WhatsApp interactive list send failed for customer {CustomerId}. Falling back to text. Error={Error}",
                customer.Id,
                listResult.Error);

            return await whatsAppMessageSender.SendTextMessageAsync(
                restaurant.WhatsAppPhoneNumberId,
                customer.PhoneNumber,
                BuildFallbackText(reply),
                cancellationToken);
        }

        if (reply.Buttons.Count > 0)
        {
            var buttonResult = await whatsAppMessageSender.SendReplyButtonMessageAsync(
                restaurant.WhatsAppPhoneNumberId,
                customer.PhoneNumber,
                reply.Text,
                reply.Buttons,
                reply.FooterText,
                cancellationToken);

            if (buttonResult.IsSuccess)
            {
                return buttonResult;
            }

            logger.LogWarning(
                "WhatsApp reply-button send failed for customer {CustomerId}. Falling back to text. Error={Error}",
                customer.Id,
                buttonResult.Error);

            return await whatsAppMessageSender.SendTextMessageAsync(
                restaurant.WhatsAppPhoneNumberId,
                customer.PhoneNumber,
                BuildFallbackText(reply),
                cancellationToken);
        }

        return await whatsAppMessageSender.SendTextMessageAsync(
            restaurant.WhatsAppPhoneNumberId,
            customer.PhoneNumber,
            reply.Text,
            cancellationToken);
    }

    private static string BuildFallbackText(OutgoingReply reply)
    {
        var builder = new StringBuilder(reply.Text.Trim());

        if (!string.IsNullOrWhiteSpace(reply.FooterText))
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine(reply.FooterText);
        }

        if (reply.Sections.Count > 0)
        {
            foreach (var section in reply.Sections)
            {
                builder.AppendLine();
                builder.AppendLine();
                builder.AppendLine($"*{section.Title}*");

                foreach (var row in section.Rows)
                {
                    builder.Append("- ");
                    builder.Append(row.Title);

                    if (!string.IsNullOrWhiteSpace(row.Description))
                    {
                        builder.Append(" - ");
                        builder.Append(row.Description);
                    }

                    builder.AppendLine();
                }
            }

            builder.AppendLine();
            builder.AppendLine("You can type an item name, View Cart, Checkout, Cancel, or Connect Restaurant.");
        }

        if (reply.Buttons.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("*Options*");

            foreach (var button in reply.Buttons)
            {
                builder.AppendLine($"- {button.Title}");
            }
        }

        return builder.ToString().Trim();
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
