# Interactive WhatsApp Ordering Experience

## Current problem

The previous WhatsApp flow rendered the restaurant's complete menu as a long text message and then attached a selection list. Large menus produced noisy conversations, could exceed WhatsApp interactive-message limits, and forced customers to scan unrelated categories before ordering.

## Proposed customer flow

1. Welcome message with Browse Menu, View Cart, and Help reply buttons.
2. Paginated category list containing only categories with active, available items.
3. Paginated item list for the selected category.
4. Quantity list with quick choices 1-5, a custom quantity option, and back navigation.
5. Item-added navigation that keeps the current category and page expanded, allowing the customer to continue in that category, change category, view the cart, or checkout.
6. Cart summary with Add More, Checkout, and Cancel reply buttons. Add More resumes the current category when context exists.
7. Customer name collection when the WhatsApp profile does not provide one.
8. Delivery or Pickup selection, followed by address collection for delivery.
9. Final summary requiring an explicit YES or NO.
10. Order creation and existing restaurant staff notification after YES.

Customers may also type an item name at any point. Exact or strong single matches proceed to quantity selection; multiple matches are returned as a selectable list. Natural messages such as `2 Paneer Pizza and 1 Veg Burger` populate the cart when every item is matched confidently.

The full text menu is sent only when the customer explicitly types `Full Menu`.

## Technical design

- `ConversationService` remains the webhook-facing orchestration service. Webhook verification and signature validation are unchanged.
- `ICategoryMessageBuilder`, `IMenuMessageBuilder`, `IQuantityMessageBuilder`, and `ICartNavigationMessageBuilder` separate channel message construction from conversation orchestration.
- The WhatsApp implementations create bounded lists and reply buttons while respecting WhatsApp's ten-row list and three-button limits.
- `MenuSelection` is a channel-neutral selection context containing the current category, menu page, last selected item, cart identity, and selected item collection. It can later back WhatsApp Flows, checkbox-capable web ordering, or expand/collapse interfaces.
- `WhatsAppCloudMessageSender` supports text, interactive list, and interactive reply-button payloads.
- `MenuSearchService` normalizes item names and ranks active, available matches.
- `FreeTextOrderParser` extracts quantities and item phrases, automatically accepting only high-confidence matches.
- `OrderingCartService` owns cart merging and total calculation.
- `PendingOrderDraft` stores cart, category, item, pagination, fulfilment, and checkout state in the existing encrypted `Conversation.TempOrderJson` column.
- Every menu query is scoped by `RestaurantId`, and inactive or unavailable items are excluded.
- Existing numeric order syntax (`Order: 1 x 2, 4 x 1`) and legacy interactive IDs remain supported.
- No database migration or breaking Azure configuration change is required.

## Interactive identifiers

- `category.list`, `category.page:{page}`, `category.select:{categoryId}`
- `item.page:{categoryId}:{page}`, `item.select:{itemId}`
- `quantity.select:{itemId}:{quantity}`, `quantity.custom:{itemId}`
- `menu.continue`, `menu.back`
- `cart.view`, `cart.add_more`, `cart.checkout`, `cart.cancel`
- `checkout.delivery`, `checkout.pickup`

The webhook passes these identifiers to the conversation service as command text. Legacy identifiers are still translated as before.

## Error handling and observability

- Unsupported or missing webhook messages continue to be ignored safely.
- Duplicate WhatsApp message IDs remain idempotently ignored.
- Processing exceptions are logged with restaurant and conversation identifiers and return a plain-text recovery prompt.
- Items that become unavailable between browsing and cart insertion are rejected.
- Staff notification failures are logged and reflected in the existing order status behavior.

## Testing checklist

- [x] Category list generation and pagination stay within ten rows.
- [x] Item pagination includes previous/next/category/cart actions.
- [x] Quantity selection includes 1-5 and custom quantity.
- [x] Quantity selection includes Back to Menu and Back to Categories without clearing the cart.
- [x] Item-added navigation exposes continue category, other categories, cart, and checkout.
- [x] Current category, page, last selected item, and cart identity are retained in selection state.
- [x] Cart merges duplicate items and recalculates line and grand totals.
- [x] Search filters inactive/unavailable items and ranks exact matches.
- [x] Free-text parsing handles multi-item natural order messages.
- [x] Checkout confirmation includes customer, items, quantities, line totals, grand total, and delivery/pickup.
- [ ] Exercise the Meta test number with real list and reply-button payloads.
- [ ] Confirm staff WhatsApp/email notification using production-like credentials.
- [ ] Verify concurrent conversations for two restaurants use isolated menus and carts.
