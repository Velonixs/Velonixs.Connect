# Velonixs Restaurant Connect
## Native WhatsApp Catalog + Multi-Item Cart + MCP Server Implementation Prompt

> Use this file directly with Codex in Visual Studio.
>
> Objective: Implement the native WhatsApp Catalog shopping experience for Velonixs Restaurant Connect, including:
>
> - Native **View Catalog** button in WhatsApp
> - Native WhatsApp catalog screen
> - Multiple product selection
> - Multiple quantities per product
> - Native **View Cart**
> - Complete cart submission in one order
> - Webhook processing for `type = "order"`
> - Velonixs order creation
> - Restaurant dashboard notification
> - Meta Catalog synchronization
> - MCP server and MCP tools
> - Tenant isolation, security, retries, idempotency, and tests

---

# 1. Existing Application Context

You are working on an existing SaaS application called **Velonixs Restaurant Connect**.

Current technology stack:

- ASP.NET Core Web API
- Blazor Admin / Restaurant Portal
- SQL Server / Azure SQL
- Entity Framework Core
- Azure App Service
- Meta WhatsApp Cloud API
- Meta Graph API
- Existing WhatsApp webhook handling
- Existing WhatsApp outbound message handling
- Multi-restaurant / multi-tenant architecture

The application already has working functionality.

## Critical Rule

**Do not rewrite or replace existing working functionality unnecessarily.**

Before implementation:

1. Inspect the complete solution.
2. Identify the current architecture.
3. Identify existing abstractions and services.
4. Reuse current models/services wherever practical.
5. Follow existing naming conventions and coding standards.
6. Preserve backwards compatibility with existing WhatsApp order flows.

---

# 2. Primary Customer Experience

The required customer experience must use the **native WhatsApp Catalog / Commerce experience**.

Do **not** build:

- A custom catalog webpage
- A Blazor customer catalog
- An HTML menu page
- A WebView catalog
- A custom JavaScript cart
- Custom plus/minus buttons inside chat

WhatsApp must render the native product catalog, quantity controls, cart, and order submission UI.

---

# 3. Required WhatsApp Flow

## Step 1 — Customer starts conversation

Example:

```text
Customer:
Hi
```

Velonixs responds:

```text
👋 Welcome to Spice Garden!

Browse our restaurant menu and order directly from WhatsApp.

[ VIEW CATALOG ]
```

The button must open the restaurant's **native WhatsApp Catalog**.

---

# 4. Native Catalog Requirements

When the customer taps **VIEW CATALOG**, the experience should be equivalent to:

```text
Chicken Momo
Steamed chicken momo
₹120
[ + ]

Chicken Biryani
₹180
[ - ] 2 [ + ]

Veg Chowmein
₹110
[ + ]

Coke
₹40
[ - ] 3 [ + ]

-------------------------
VIEW CART (5)
```

## Mandatory behavior

The customer must be able to:

- View product image
- View product name
- View description
- View price
- Add a product
- Remove a product
- Increase quantity
- Decrease quantity
- Select multiple different products
- Select quantity greater than one
- Open the cart
- Review all selected items
- Submit all selected products together

The exact UI can vary by WhatsApp version/client.

Do not implement custom quantity controls. Use the native WhatsApp catalog/cart UI.

---

# 5. Example Multi-Item Cart

The following must work as one customer order:

```text
Chicken Momo × 2
Chicken Biryani × 1
Coke × 3
```

The customer should submit these products together from the native WhatsApp cart.

Velonixs must receive the complete submitted cart and create:

- ONE Order
- MULTIPLE OrderItems

Do not create one order per product.

---

# 6. Target Architecture

```text
Customer WhatsApp
        |
        v
WhatsApp Cloud API
        |
        v
Velonixs Webhook
        |
        v
WhatsApp Message Router
        |
        v
Incoming Order Handler
        |
        v
Order Service
        |
        v
SQL Server / Azure SQL
        |
        v
Restaurant Dashboard
```

Catalog synchronization:

```text
Restaurant Admin
        |
        v
Velonixs Menu Service
        |
        v
Velonixs Database
        |
        v
Catalog Sync Queue / Background Worker
        |
        v
Meta Catalog API
        |
        v
WhatsApp Native Catalog
```

MCP:

```text
AI Agent / Codex / MCP Client
        |
        v
Velonixs MCP Server
        |
        v
Velonixs Application Services
        |
        +--> Menu Service
        +--> Catalog Service
        +--> Catalog Sync Service
        +--> WhatsApp Messaging Service
        +--> Order Service
```

## Important Architecture Rule

REST controllers, Blazor pages, background workers, webhook handlers, and MCP tools must use the **same application/service layer**.

Do not duplicate business logic inside MCP handlers.

---

# 7. Repository Inspection — Mandatory First Step

Before modifying code, inspect the solution and document:

- Solution/projects
- Existing ASP.NET Core API project
- Existing Blazor project
- Existing domain/application/infrastructure projects
- Existing Restaurant entity/model
- Existing Menu entity/model
- Existing Order and OrderItem models
- Existing WhatsApp webhook controller/endpoint
- Existing WhatsApp messaging service
- Existing Meta API service/configuration
- Existing tenant resolution
- Authentication/authorization
- Database context
- Existing background workers
- Existing logging
- Existing SignalR or notification mechanism
- Existing tests

After inspection, produce a concise architecture assessment.

Then implement incrementally.

---

# 8. Meta Catalog Integration

Create or extend a dedicated abstraction:

```csharp
public interface IMetaCatalogService
```

Responsibilities should include, where supported by the current official Meta APIs:

```text
CreateCatalogAsync
GetCatalogAsync
GetCatalogProductsAsync
CreateProductAsync
UpdateProductAsync
DeleteProductAsync
UpdateProductPriceAsync
UpdateProductAvailabilityAsync
SyncProductAsync
SyncRestaurantCatalogAsync
GetCatalogSyncStatusAsync
```

Do not tightly couple Meta API calls to controllers.

Use:

- `IHttpClientFactory`
- dependency injection
- async/await
- `CancellationToken`
- strongly typed options
- structured logging

---

# 9. Official API Validation

Before implementing Meta payloads:

1. Verify current official Meta documentation.
2. Confirm currently supported WhatsApp Catalog / Commerce endpoints.
3. Confirm current Graph API fields.
4. Confirm current catalog-message payload.
5. Confirm incoming order webhook shape.
6. Confirm commerce/catalog connection requirements.
7. Confirm API version currently supported.

## Critical Rule

**Do not guess Meta request or response fields.**

Graph API version must be configurable, for example:

```json
{
  "Meta": {
    "GraphApiVersion": "CONFIGURABLE",
    "BaseUrl": "https://graph.facebook.com"
  }
}
```

Do not spread hard-coded API versions throughout the codebase.

---

# 10. Velonixs Menu as Source of Truth

The Velonixs database must remain the **primary source of truth**.

Meta Catalog is a synchronized representation of the Velonixs menu.

A restaurant should manage the menu only from Velonixs.

Flow:

```text
Velonixs Menu
    |
    v
Velonixs DB
    |
    v
Catalog Sync Service
    |
    v
Meta Catalog
    |
    v
WhatsApp Catalog
```

---

# 11. Menu Item Requirements

Reuse the existing menu model if possible.

Add only missing fields.

A menu item should support:

```text
Id
RestaurantId
CategoryId
Name
Description
SKU
Price
DiscountPrice
Currency
ImageUrl
IsVegetarian
IsAvailable
IsActive
PreparationTimeMinutes

MetaCatalogId
MetaProductId
ProductRetailerId

SyncStatus
LastSyncedAt
LastSyncError
RetryCount

CreatedAt
UpdatedAt
```

## ProductRetailerId

`ProductRetailerId` must be stable.

Examples:

```text
MOMO-CHICKEN-001
MOMO-VEG-001
BIRYANI-CHICKEN-001
DRINK-COKE-001
```

It must be unique within the correct restaurant/catalog context.

Do not rely only on raw database IDs when a stable external business identifier is more appropriate.

---

# 12. Catalog Configuration

Create or extend configuration for each restaurant:

```text
RestaurantId
MetaBusinessId
WabaId
PhoneNumberId
MetaCatalogId
CredentialReference
IsCatalogEnabled
IsCartEnabled
LastSuccessfulSyncAt
```

Access tokens must not be committed to source control.

Design credentials so they can later be moved to Azure Key Vault.

Never expose credentials in:

- API responses
- MCP responses
- logs
- exceptions
- UI source

---

# 13. Send Native WhatsApp Catalog Message

Create a reusable application/service method such as:

```csharp
Task SendCatalogMessageAsync(
    Guid restaurantId,
    string customerPhoneNumber,
    CancellationToken cancellationToken);
```

This must send the appropriate supported native WhatsApp interactive catalog message.

Expected customer-facing result:

```text
🍽️ Browse our menu and order directly from WhatsApp.

[ VIEW CATALOG ]
```

When clicked, the native WhatsApp Catalog should open.

Do not redirect to a Velonixs web URL if native catalog functionality is available.

---

# 14. Commerce Settings Validation

Create or extend:

```csharp
public interface IWhatsAppCommerceService
```

Potential responsibilities:

```text
GetCommerceSettingsAsync
ValidateCatalogConnectionAsync
ValidateCartConfigurationAsync
ValidateCatalogVisibilityAsync
```

If currently supported by Meta APIs, add enable/update operations as appropriate.

Do not invent unsupported API operations.

Provide clear configuration diagnostics for the restaurant/admin.

---

# 15. Incoming WhatsApp Order Webhook

Extend the existing WhatsApp webhook processor.

Detect native cart submissions represented by an incoming message with:

```text
type = "order"
```

Create or reuse DTOs such as:

```csharp
public sealed class WhatsAppIncomingOrder
{
    public string CustomerPhone { get; set; }
    public string CatalogId { get; set; }
    public string ExternalMessageId { get; set; }
    public List<WhatsAppIncomingOrderItem> Items { get; set; }
}
```

```csharp
public sealed class WhatsAppIncomingOrderItem
{
    public string ProductRetailerId { get; set; }
    public int Quantity { get; set; }
    public decimal ItemPrice { get; set; }
    public string Currency { get; set; }
}
```

Map from the current official Meta webhook payload.

---

# 16. Multiple Product Support

The handler must process:

```text
order.product_items[]
```

Do not assume there is only one element.

Example:

```text
product_items[0]
ProductRetailerId = MOMO-001
Quantity = 2

product_items[1]
ProductRetailerId = BIRYANI-001
Quantity = 1

product_items[2]
ProductRetailerId = COKE-001
Quantity = 3
```

The result must be:

```text
ONE Order
THREE OrderItems
```

---

# 17. Quantity Support

Quantities greater than 1 must be supported.

Example:

```text
Chicken Momo quantity = 2
Coke quantity = 3
```

Validate:

- quantity > 0
- configurable maximum quantity if the existing product/business rules require it
- integer bounds
- malformed payload handling

---

# 18. Server-Side Order Validation

Never blindly trust incoming WhatsApp prices.

For every received item:

1. Determine the restaurant from the destination WhatsApp number / tenant context.
2. Find product using:

```text
RestaurantId + ProductRetailerId
```

3. Validate tenant ownership.
4. Validate product exists.
5. Validate `IsActive`.
6. Validate `IsAvailable`.
7. Load current authoritative price from Velonixs DB.
8. Validate quantity.
9. Calculate line total server-side.

Example:

```text
MOMO-001

DB price = ₹120
Quantity = 2
Line total = ₹240
```

```text
COKE-001

DB price = ₹40
Quantity = 3
Line total = ₹120
```

Never use the WhatsApp-submitted price as the sole source for final billing.

The incoming price may be stored for diagnostics if useful, but billing must use server-side business rules.

---

# 19. Price Change Edge Case

The customer may open the catalog before the restaurant changes a price.

Example:

```text
Customer opened catalog when Chicken Momo = ₹120

Restaurant changes price to ₹140

Customer later submits cart
```

Define safe behavior.

Recommended:

- load current Velonixs price
- calculate current order total
- if incoming displayed price differs materially, notify customer that price changed before final confirmation
- do not silently charge an unexpected amount without customer-facing confirmation

Implement this consistently with existing order-confirmation UX.

---

# 20. Sold-Out Edge Case

A customer may have a product in the cart when the restaurant marks it sold out.

When the order arrives:

- detect unavailable item
- do not create an invalid final order silently
- respond with a clear customer message
- identify unavailable products
- ask customer to remove/replace them or resend the cart

Example:

```text
Sorry, Chicken Biryani is currently sold out.

Please update your cart and send the order again.
```

---

# 21. Order Creation

Reuse existing entities where possible.

Order should support:

```text
Id
RestaurantId
CustomerId
CustomerPhone
OrderNumber
OrderSource
OrderStatus
Subtotal
DeliveryCharge
Tax
Discount
GrandTotal
Currency
ExternalWhatsAppMessageId
CreatedAt
```

Order source:

```text
WhatsAppCatalog
```

OrderItem should support:

```text
Id
OrderId
MenuItemId
ProductRetailerId
ItemNameSnapshot
UnitPrice
Quantity
LineTotal
CustomerInstructions
```

Store snapshots where appropriate to preserve historical order integrity.

---

# 22. Idempotency

WhatsApp/webhook delivery may be retried.

Order processing must be idempotent.

Use the WhatsApp message ID or another reliable external unique identifier.

If the identical webhook is processed twice:

```text
Expected:
ONE order
NOT two orders
```

Add a unique constraint/index where appropriate.

Handle race conditions.

---

# 23. Customer Order Confirmation

After successfully creating the order, send:

```text
✅ Order received!

Order #VRC1052

🥟 Chicken Momo × 2       ₹240
🍗 Chicken Biryani × 1    ₹180
🥤 Coke × 3               ₹120

-------------------------------
Subtotal                   ₹540

Choose:

[ DELIVERY ]

[ PICKUP ]
```

Use currently supported native WhatsApp interactive controls when appropriate.

Continue the existing Velonixs order workflow.

---

# 24. Existing Order Workflow Integration

After catalog cart submission:

```text
Cart submitted
    |
    v
Order created
    |
    v
Delivery / Pickup
    |
    v
Address / Location
    |
    v
Payment
    |
    v
Restaurant Accept / Reject
    |
    v
Preparation Time
    |
    v
Preparing
    |
    v
Ready
    |
    v
Out for Delivery
    |
    v
Delivered
```

Reuse existing workflow/state machines where available.

Do not create a competing second order workflow unnecessarily.

---

# 25. Restaurant Dashboard

When a WhatsApp Catalog order arrives, the restaurant dashboard should receive a visible notification.

Example:

```text
🔔 NEW ORDER

Order #VRC1052

Customer:
+91XXXXXXXXXX

Chicken Momo × 2
Chicken Biryani × 1
Coke × 3

Total: ₹540

[ ACCEPT ]
[ REJECT ]
```

If SignalR or another real-time mechanism already exists, reuse it.

If no real-time mechanism exists, follow the existing application architecture and implement the least invasive production-ready approach.

---

# 26. Accept / Reject

Restaurant should be able to:

```text
ACCEPT
REJECT
```

When accepted, allow preparation time selection:

```text
10 minutes
15 minutes
20 minutes
30 minutes
Custom
```

Then notify the customer through WhatsApp.

Example:

```text
✅ Your order #VRC1052 has been accepted.

Estimated preparation time: 20 minutes.
```

---

# 27. Catalog Synchronization

Implement asynchronous synchronization.

When the restaurant:

- creates a product
- changes name
- changes description
- changes price
- changes image
- marks available
- marks sold out
- deactivates product

Velonixs should:

```text
Save DB first
    |
    v
Mark SyncStatus = Pending
    |
    v
Background synchronization
    |
    v
Meta Catalog
```

The user should not lose a local menu update just because Meta is temporarily unavailable.

---

# 28. Sync Status

Support:

```text
Pending
Syncing
Synced
Failed
```

Store:

```text
LastSyncedAt
LastSyncError
RetryCount
```

Create sync logs if they do not already exist.

Example entity:

```text
CatalogSyncLog

Id
RestaurantId
MenuItemId nullable
Action
Status
HttpStatusCode
ErrorMessage
RetryCount
CreatedAt
```

Never log access tokens or authorization headers.

---

# 29. Resilience

Handle:

- HTTP 400 validation errors
- 401 authentication failures
- 403 permission failures
- 404 resource issues
- 429 rate limiting
- Meta 5xx failures
- network failures
- timeouts
- cancellation
- duplicate requests

Use retry only for appropriate transient errors.

Use exponential backoff where suitable.

Do not retry permanent 4xx validation failures indefinitely.

---

# 30. MCP Server

Add an MCP server to Velonixs Restaurant Connect.

Use the current recommended/supported .NET MCP implementation after verifying the official/current SDK/package situation.

## Critical MCP Rule

MCP is a control/integration layer.

MCP is **not** responsible for rendering the WhatsApp catalog.

WhatsApp renders:

- catalog
- product selection
- quantity controls
- cart
- order submission

MCP should call Velonixs application services.

---

# 31. MCP Architecture

```text
MCP Client / AI Agent
        |
        v
Velonixs MCP Server
        |
        v
Application Services
        |
        +--> MenuService
        +--> CatalogService
        +--> CatalogSyncService
        +--> WhatsAppMessagingService
        +--> OrderService
```

Do not perform direct database mutations from MCP handlers if existing application services already contain the relevant business logic.

---

# 32. MCP Catalog Tools

Expose tools similar to:

```text
restaurant_get_catalog

restaurant_create_catalog

restaurant_sync_catalog

restaurant_get_catalog_status

restaurant_get_menu

restaurant_get_menu_item

restaurant_add_menu_item

restaurant_update_menu_item

restaurant_update_price

restaurant_set_item_available

restaurant_set_item_sold_out

restaurant_remove_menu_item

restaurant_send_catalog
```

Tool names may follow the project's established naming conventions.

---

# 33. MCP Order Tools

Expose:

```text
restaurant_get_orders

restaurant_get_order

restaurant_accept_order

restaurant_reject_order

restaurant_set_preparation_time
```

Optionally expose safe read-only tools such as:

```text
restaurant_get_pending_orders
restaurant_get_today_orders
```

only if they align with the existing domain model.

---

# 34. MCP Example — Send Catalog

User/agent request:

```text
Send catalog to +919876543210
```

MCP:

```text
restaurant_send_catalog
```

Flow:

```text
MCP
    |
    v
Catalog Messaging Application Service
    |
    v
WhatsApp Cloud API
    |
    v
Customer receives:
[ VIEW CATALOG ]
```

---

# 35. MCP Example — Sold Out

User/agent:

```text
Chicken Momo is sold out
```

MCP:

```text
restaurant_set_item_sold_out
```

Flow:

```text
MCP
    |
    v
MenuService
    |
    v
Velonixs DB
    |
    v
SyncStatus = Pending
    |
    v
Catalog Sync
    |
    v
Meta Catalog
```

---

# 36. MCP Example — Price Change

User/agent:

```text
Change Chicken Momo price to ₹140
```

MCP:

```text
restaurant_update_price
```

Flow:

```text
MCP
    |
    v
MenuService
    |
    v
Velonixs DB = ₹140
    |
    v
Catalog Sync Pending
    |
    v
Meta Catalog
    |
    v
WhatsApp Catalog eventually reflects ₹140
```

---

# 37. MCP Tenant Isolation

This is mandatory.

Restaurant A must never be able to:

- view Restaurant B catalog
- read Restaurant B menu
- update Restaurant B menu
- send Restaurant B catalog
- read Restaurant B orders
- accept Restaurant B orders
- access Restaurant B Meta credentials

Do not trust a caller-supplied `RestaurantId` by itself.

Resolve/enforce tenant context through authentication/authorization.

Every MCP tool must run within an authorized tenant context.

---

# 38. MCP Secrets

Never expose:

- Meta access tokens
- app secrets
- system-user tokens
- authorization headers
- connection strings
- private credentials

through:

- MCP output
- logs
- API output
- exception messages

---

# 39. Blazor Admin UI

Create or extend these areas:

```text
Menu Management
Catalog Configuration
Catalog Sync Status
Orders
```

Menu Management should display:

```text
Product
Category
Price
Availability
Meta Sync Status
Last Sync
Actions
```

Actions:

```text
Edit
Available
Sold Out
Delete/Deactivate
Sync
```

Also provide:

```text
[ Sync Entire Catalog ]
[ Test Catalog ]
[ Send Catalog to Test Number ]
```

where appropriate.

---

# 40. Test Catalog Feature

Add an onboarding/admin test feature.

Restaurant/admin enters a WhatsApp number and clicks:

```text
Send Test Catalog
```

Velonixs sends:

```text
🍽️ Test your restaurant menu.

[ VIEW CATALOG ]
```

This helps validate:

- catalog connection
- catalog visibility
- WhatsApp business phone configuration
- cart support
- outbound catalog message
- product synchronization

before production launch.

---

# 41. API Endpoints

Follow the application's existing API style.

Potential endpoints:

```text
GET    /api/menu
POST   /api/menu/items
PUT    /api/menu/items/{id}
DELETE /api/menu/items/{id}

POST   /api/catalog/sync
GET    /api/catalog/sync-status
POST   /api/catalog/items/{id}/sync

POST   /api/whatsapp/catalog/send
POST   /api/whatsapp/catalog/test
```

Do not add duplicate endpoints if equivalent APIs already exist.

Controllers should remain thin.

Business logic belongs in application/domain services.

---

# 42. Database Migration Rules

Use EF Core migrations.

Do not:

- delete production data
- recreate existing tables unnecessarily
- rename existing production columns without migration analysis
- introduce destructive migrations without explicit justification

Create indexes/constraints where useful.

Important candidates:

- unique ProductRetailerId per restaurant
- unique external WhatsApp order/message ID
- RestaurantId indexes
- CatalogId indexes
- SyncStatus indexes where background processing benefits

---

# 43. Unit Tests

Add unit tests for:

1. Menu item creation
2. Duplicate ProductRetailerId
3. Duplicate SKU
4. Tenant isolation
5. Price validation
6. Availability changes
7. Sync status transition
8. Meta sync failure
9. Successful catalog-message request mapping
10. Multi-product incoming order parsing
11. Quantity greater than one
12. Sold-out product validation
13. Price-change validation
14. Duplicate webhook/idempotency
15. MCP tenant enforcement
16. MCP sold-out operation
17. MCP update-price operation
18. MCP send-catalog operation

---

# 44. Integration Tests

Where practical, add integration tests for:

```text
WhatsApp order webhook
    ->
Order handler
    ->
DB Order
    ->
Multiple OrderItems
```

Test this exact payload scenario conceptually:

```text
Chicken Momo × 2
Chicken Biryani × 1
Coke × 3
```

Expected:

```text
Orders created: 1
OrderItems created: 3
```

---

# 45. Duplicate Webhook Test

Replay the same valid incoming order webhook twice.

Expected:

```text
First processing:
Order created

Second processing:
No duplicate order
Safe idempotent result
```

The application must not generate two restaurant notifications/orders.

---

# 46. Critical End-to-End Acceptance Test

The implementation is complete only when this full flow works:

## Step 1

Customer sends:

```text
Hi
```

## Step 2

Velonixs responds:

```text
[ VIEW CATALOG ]
```

## Step 3

Customer clicks **VIEW CATALOG**.

## Step 4

The **native WhatsApp Catalog** opens.

## Step 5

Customer sees restaurant products with:

- image
- name
- description
- price
- add control

## Step 6

Customer selects:

```text
Chicken Momo × 2
Chicken Biryani × 1
Coke × 3
```

## Step 7

WhatsApp shows:

```text
VIEW CART
```

## Step 8

Customer reviews the cart.

## Step 9

Customer submits the complete cart.

## Step 10

Velonixs receives:

```text
ONE incoming WhatsApp order
THREE product items
```

## Step 11

Velonixs validates each product against its own database.

## Step 12

Velonixs creates:

```text
ONE Order
THREE OrderItems
```

## Step 13

Restaurant dashboard shows:

```text
🔔 NEW ORDER
```

## Step 14

Restaurant accepts/rejects.

## Step 15

Restaurant sets preparation time.

## Step 16

Customer receives the order status.

## Step 17

MCP can independently:

- send catalog
- read catalog
- synchronize catalog
- add menu item
- update menu item
- update price
- mark sold out
- mark available
- read authorized orders
- accept/reject authorized orders
- set preparation time

---

# 47. Mandatory Failure Conditions

Do not mark the feature as complete if any of these occur:

- View Catalog opens an external custom webpage instead of native WhatsApp Catalog
- Only one item can be ordered
- Quantity > 1 is not supported
- Separate Velonixs orders are created for each cart line
- Webhook parsing assumes one product
- Incoming price is blindly trusted
- Sold-out items are accepted without validation
- Duplicate webhooks create duplicate orders
- MCP bypasses tenant security
- Tokens are logged
- Meta Graph API fields are guessed rather than verified
- Business logic is duplicated in MCP handlers
- Existing working order workflow is unnecessarily replaced

---

# 48. Implementation Phases

Implement in this order.

## Phase 1 — Repository Assessment

- Inspect the solution
- Identify existing implementation
- Document integration points
- Identify files/classes to reuse

Do not make major changes before completing this assessment.

## Phase 2 — Domain / Database

- Add only required entity fields/tables
- Add indexes
- Add migration
- Build solution

## Phase 3 — Meta Catalog Service

- Implement API abstractions
- Implement product synchronization
- Implement error handling
- Add tests
- Build solution

## Phase 4 — Native Catalog Message

- Implement View Catalog message
- Validate current official Meta request schema
- Add test-send capability
- Build solution

## Phase 5 — Incoming Order Webhook

- Parse `type = order`
- Parse all `product_items[]`
- Map DTOs
- Add tests
- Build solution

## Phase 6 — Multi-Item Order Creation

- Validate all products
- Validate quantities
- Calculate totals
- Create one order with multiple order items
- Add idempotency
- Build solution

## Phase 7 — Dashboard

- Show new order
- Accept/reject
- preparation time
- notifications
- Build solution

## Phase 8 — Catalog Availability / Sold Out

- Local DB update first
- async Meta synchronization
- error/retry handling
- Build solution

## Phase 9 — MCP Server

- Add current supported .NET MCP implementation
- register MCP server
- configure authentication/authorization
- add tenant-aware context
- Build solution

## Phase 10 — MCP Tools

- implement catalog tools
- implement menu tools
- implement order tools
- reuse existing services
- add tests
- Build solution

## Phase 11 — Security Review

Verify:

- tenant isolation
- credentials
- logs
- error messages
- authorization
- MCP access control
- webhook validation

## Phase 12 — End-to-End Validation

Run all relevant tests.

Validate the critical customer flow.

---

# 49. Required Working Style

For every phase:

1. Inspect existing code first.
2. Make the smallest safe change.
3. Build the solution.
4. Fix compile errors.
5. Run relevant tests.
6. Fix failures.
7. List modified files.
8. Explain configuration requirements.
9. Continue to the next phase.

Do not stop after generating sample code.

Implement the changes in the existing repository.

---

# 50. Final Report

After implementation, provide:

## Architecture

- Final data flow
- Final service responsibilities
- MCP architecture
- Meta integration architecture

## Files Changed

For each modified/created file:

```text
File
Purpose
```

## Database

- migrations
- new fields
- indexes
- constraints

## Configuration

List required settings:

```text
Meta Business ID
WABA ID
Phone Number ID
Catalog ID
Access token / credential source
Graph API version
Webhook configuration
```

Do not display secrets.

## Meta Manual Setup

Document any configuration that must be completed manually in Meta Business Manager / WhatsApp Manager.

## Test Results

Provide a PASS/FAIL table:

| Requirement | Result |
|---|---|
| Native View Catalog | PASS/FAIL |
| Native catalog opens | PASS/FAIL |
| Multiple products | PASS/FAIL |
| Quantity > 1 | PASS/FAIL |
| View Cart | PASS/FAIL |
| One order with multiple OrderItems | PASS/FAIL |
| Server-side price validation | PASS/FAIL |
| Sold-out validation | PASS/FAIL |
| Duplicate webhook prevention | PASS/FAIL |
| Dashboard notification | PASS/FAIL |
| Catalog sync | PASS/FAIL |
| MCP server | PASS/FAIL |
| MCP tenant isolation | PASS/FAIL |
| Secrets not exposed | PASS/FAIL |

For each failed requirement:

1. Explain the cause.
2. Identify affected files.
3. Implement the correction.
4. Build again.
5. Rerun tests.
6. Update the result.

Do not report the feature as complete until all critical requirements pass.

---

# 51. Key Product Principle

The final product design must remain:

```text
Velonixs Menu
    |
    v
Velonixs Catalog Service
    |
    v
Meta Catalog
    |
    v
Native WhatsApp Catalog
    |
    v
Multiple Products + Multiple Quantities
    |
    v
Native View Cart
    |
    v
Customer Sends Cart
    |
    v
Velonixs Webhook
    |
    v
Order Service
    |
    v
Restaurant Dashboard
```

And:

```text
AI Agent
    |
    v
Velonixs MCP Server
    |
    v
Same Velonixs Application Services
```

MCP controls Velonixs capabilities.

Meta/WhatsApp provides the actual customer-facing catalog/cart experience.

---

# START IMPLEMENTATION

Begin now.

First:

1. Inspect the repository.
2. Produce the architecture assessment.
3. Identify the existing WhatsApp webhook and outbound message implementation.
4. Identify the existing menu/catalog/order services.
5. Identify the current tenant isolation strategy.
6. Identify the smallest set of required changes.

Then proceed through the implementation phases above without unnecessarily rewriting existing working code.
