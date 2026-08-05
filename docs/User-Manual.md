# Velonixs Connect User Manual

Velonixs Connect manages a business catalog, receives WhatsApp orders, and gives platform and business users separate operational workspaces.

The primary workspaces are:

- **Admin Blazor:** `http://localhost:5080/admin/blazor`
- **Business Portal Blazor:** `http://localhost:5090/portal/blazor`

> Important: Velonixs Connect can synchronize products to an **existing** Meta catalog. It does not create a Meta Business account, WhatsApp Business Account, phone number, or catalog for you. Provision those assets in Meta first.

## 1. Prerequisites and local setup

You need .NET 8 SDK, SQL Server or Azure SQL, and a database connection string. API, Admin, and Portal must use the same database connection and data-encryption key. Each host also needs an authentication signing key:

- `ConnectionStrings__RestaurantConnect`
- `DataEncryption__Key` - a Base64-encoded 32-byte key
- `Auth__SigningKey` - at least 32 characters

The database key is required to read encrypted data. Keep it outside source control and do not replace it without a planned key rotation.

For the first platform administrator, configure `Auth__DefaultAdminEmail` and `Auth__DefaultAdminPassword` on the API host. The API is the database initializer: it applies migrations, creates the standard roles, and creates or restores that admin account when both values are present. Start the API before the Admin and Portal hosts.

For local development, use Secret Manager or environment variables rather than editing `appsettings.json`. For example, run the following for each of the API, Admin, and Portal projects (substitute real values):

```powershell
dotnet user-secrets set --project src/Velonixs.Connect.Api "ConnectionStrings:RestaurantConnect" "<SQL connection string>"
dotnet user-secrets set --project src/Velonixs.Connect.Api "DataEncryption:Key" "<Base64 32-byte key>"
dotnet user-secrets set --project src/Velonixs.Connect.Api "Auth:SigningKey" "<32-or-more-character signing key>"
dotnet user-secrets set --project src/Velonixs.Connect.Api "Auth:DefaultAdminEmail" "admin@example.com"
dotnet user-secrets set --project src/Velonixs.Connect.Api "Auth:DefaultAdminPassword" "<strong password>"
```

Repeat the connection, encryption-key, and signing-key settings for `src/Velonixs.Connect.Admin` and `src/Velonixs.Connect.Portal`.

Start the applications in three terminals, preferably starting the API first:

```powershell
dotnet restore Velonixs.Connect.sln
dotnet build Velonixs.Connect.sln

dotnet run --project src/Velonixs.Connect.Api --launch-profile http
dotnet run --project src/Velonixs.Connect.Admin --launch-profile http
dotnet run --project src/Velonixs.Connect.Portal --launch-profile http
```

Useful local addresses:

| Service | Address |
| --- | --- |
| API health check | `http://localhost:5077/health` |
| Admin sign-in | `http://localhost:5080/admin/login` |
| Admin Blazor | `http://localhost:5080/admin/blazor` |
| Portal sign-in | `http://localhost:5090/portal/login` |
| Portal Blazor | `http://localhost:5090/portal/blazor` |

The Admin application accepts only localhost requests by default. Set `Admin__AllowRemote=true` only when remote administrative access is deliberately required and properly protected.

## 2. Roles and access

| Role | Scope and current access |
| --- | --- |
| `PlatformAdmin` | Uses Admin Blazor; manages every business, platform tax defaults, master catalog, business owners, catalog sync, and cross-business orders/customers. |
| `BusinessOwner` | Belongs to one business; can manage that business's menu and, in the classic Portal Staff Users screen, create/enable/disable manager, cashier, and staff accounts. |
| `BusinessManager` | Belongs to one business and can manage its menu. |
| `Cashier` | Belongs to one business and has the business operational workspace. |
| `Staff` | Belongs to one business and has the business operational workspace. |

All Portal accounts are restricted to the `BusinessId` on their signed-in account. In the current Portal Blazor workspace, all business roles can view the dashboard, orders, customers, and menu; only owners and managers can change menu data. The order queue permits signed-in Portal roles to make valid status transitions. Browser sessions require an active account and are invalidated when its role or business assignment no longer matches.

## 3. Admin Blazor

Sign in at `/admin/login`, then open `/admin/blazor`.

### Create and maintain a business

On **Dashboard**, set platform CGST/SGST defaults if required, then use **Create Business**. Name, WhatsApp Phone Number ID, and owner name/email/password are required. Creating a business also creates its initial `BusinessOwner` account.

Open **Settings** for a business to maintain:

- business name and type;
- WhatsApp Phone Number ID (unique across businesses);
- contact, notification-email, staff-WhatsApp, address, tax, and active-state settings;
- the read-only catalog-ID mirror and a link to **Catalog Sync**, where catalog changes are reconciled safely; and
- additional owner accounts and password resets for business users.

Only an active business with the matching WhatsApp Phone Number ID processes customer webhook messages. Deactivating a business prevents it from being selected for new incoming messages.

### Master catalog and business catalog

Use **Master Catalog** to maintain reusable platform categories and product templates. A master item/category that is already mapped by a business cannot be deleted; mark it inactive instead.

For an individual business, select **Products** to create, edit, deactivate, or delete categories and products. A category cannot be deleted while it still contains products. Products include an item code, price, description, optional SKU (the WhatsApp retailer ID), optional image URL, availability, and active state. Product codes and retailer IDs must be unique within a business. A product may be local-only without an image, but Meta synchronization requires a public HTTPS image URL.

If no retailer ID is supplied, the application creates one for a new product. This retailer ID/SKU is immutable after creation so it remains a stable Meta identity; create a replacement product if it must change. Deactivation makes the product unavailable locally and, where Meta sync is active, queues a remote delete event. Permanent deletion requires an active catalog connection so the durable Meta delete snapshot can be recorded; otherwise deactivate the product and remove it after the connection is restored.

### Admin orders, customers, and sync

The Admin business pages provide cross-business order status updates, a customer list, and the **Catalog Sync** screen. Status updates still enforce the normal order transition rules described below.

## 4. Business Portal Blazor

Sign in at `/portal/login`, then open `/portal/blazor`.

### Dashboard and customers

The dashboard displays pending confirmations, orders and revenue for today, available products, and recent orders for the signed-in business. **Customers** is a business-only directory built from that business's orders; it supports name, phone, and address search and provides a WhatsApp link when a phone number is available.

### Menu

Owners and managers can:

1. Add a category, optionally based on a platform master category.
2. Add a product, optionally based on a master product template.
3. Set item code, price, description, retailer ID/SKU, and a public image URL.
4. Mark a product temporarily unavailable or deactivate it.

Cashiers and staff see the menu but have read-only access. Only active, available products are offered to customers.

The Blazor navigation includes **Open classic portal**. The classic Portal's **Staff Users** page is currently where a `BusinessOwner` creates or enables/disables `BusinessManager`, `Cashier`, and `Staff` accounts.

### Orders

Use **Orders** to filter the business order queue and update the customer-facing status. The allowed progression is:

```text
Pending Confirmation -> Confirmed | Rejected | Cancelled
Confirmed            -> Preparing | Ready for Pickup | Out for Delivery | Delivered | Cancelled
Preparing            -> Ready for Pickup | Out for Delivery | Delivered | Cancelled
Ready for Pickup     -> Delivered | Cancelled
Out for Delivery     -> Delivered | Cancelled
```

Confirming an order requires a ready-time of 1-1,440 minutes. Rejecting it requires a reason. Each valid status change is recorded in history; customer WhatsApp updates are attempted when WhatsApp sending is enabled and credentials are configured.

## 5. WhatsApp catalog and Meta synchronization

### Configure the customer catalog

Before enabling live sync, create the Meta catalog and collect its Catalog ID, WABA ID, phone number ID, and a token that can manage catalog items. In Admin Blazor:

1. Open the business **Settings** and ensure the business WhatsApp Phone Number ID is correct. This ID routes customer messages to the business.
2. Open **Catalog Sync**.
3. Enter the Meta WABA ID (reference), **existing Catalog ID**, the Meta phone number ID that exactly matches the business routing ID, and an access token.
4. Select a sync mode and save. Enabling sync requires a Catalog ID and access token.

The Catalog ID is also copied to the business's WhatsApp Catalog ID. Customer product-list messages use that ID only for products whose sync status is **Synced** and whose returned Meta product ID has been recorded; product retailer IDs must match the retailer IDs in the Meta catalog. Catalog credentials and queue controls are limited to `PlatformAdmin`; business users manage local products through the Portal.

Webhook verification and Cloud-message delivery use the API-wide `WhatsApp` configuration, not a per-business catalog form. Configure its verify token and app secret in the API host as shown below.

If you change a business's Catalog ID, Velonixs clears every recorded Meta product ID and removes those products from WhatsApp product-list messages until the new catalog is synchronized. In Default/Automatic mode it queues that reconciliation automatically; if the host stops immediately after the settings save, the API worker detects the resulting `NotQueued` products and resumes that reconciliation on its next pass. In Manual mode choose **Sync all** after saving. Cleanup of items in the old external Meta catalog remains an operator task because its credentials/catalog context were replaced.

### Sync modes and queue

- **Default** and **Automatic:** creating, editing, or deactivating a product queues its catalog event while the business connection is enabled. Changes made while sync is unavailable are marked for reconciliation; after a connection is re-enabled, the API worker resumes paused work and queues those marked products without resetting unrelated failed items.
- **Manual:** product changes do not auto-queue. Switching into Manual cancels outstanding automatic snapshots and marks their affected products for explicit reconciliation, so use a product's **Sync** button or **Sync all** to queue the current state deliberately.
- **Sync all:** queues updates for active products and delete events for inactive products, reconciling changes made while sync was disabled or manual.
- **Process pending:** normal production processing is owned by the API worker (every 60 seconds by default). The Admin button is available only when that Admin host is itself configured for live sending.

The queue coalesces outstanding work for a product and serializes an in-flight event before a later create/update/delete event for the same SKU. It records `Pending`, `Waiting`, `Processing`, `Paused`, `Synced`, `Simulated`, `Cancelled`, or `Failed` outcomes, retry count, next attempt, and the latest error. Failed entries can be retried from the screen; normal retries use backoff and the default maximum is five attempts.

### Dry run, then live sync

Meta catalog sending is disabled by default. Keep this setting on while validating configuration:

```text
META_CATALOG_DISABLE_SENDING=true
```

With dry run enabled, queue items complete as **Simulated** and no HTTP request is sent to Meta. You must still save a Catalog ID and non-empty access token before enabling the business connection. A simulated result does not add products to Meta.

After checking the outgoing catalog details and preparing the target catalog, set the API environment setting to `META_CATALOG_DISABLE_SENDING=false` and restart the API. The next API queue pass automatically replays prior **Simulated** work in product order. The service upserts create/update events by product retailer ID and records Meta's returned product ID; it uses that recorded ID for later deletions. Check the queue and logs for **Synced** or **Failed** results; do not treat a local product record or dry-run result as proof that a remote Meta product exists.

For real WhatsApp customer messaging, configure the API separately with:

```text
WhatsApp__AccessToken=
WhatsApp__VerifyToken=
WhatsApp__AppSecret=
WhatsApp__DisableSending=false
```

The catalog-sync access token is per business; the WhatsApp Cloud message sender uses the API's `WhatsApp` configuration.

## 6. Customer WhatsApp order flow

For a complete, dummy-data walkthrough with webhook payloads and a Mermaid sequence diagram, see [WhatsApp Messaging Demo Flow](WhatsApp-Demo-Flow.md).

Configure Meta to verify and deliver webhooks to:

```text
GET/POST https://<your-domain>/api/webhooks/whatsapp
```

Use the same `WhatsApp__VerifyToken` in Meta during verification. Configure `WhatsApp__AppSecret` to validate `X-Hub-Signature-256` on incoming requests. In non-development environments, requests without a valid app-secret signature are rejected.

When a customer sends `Hi`, `Menu`, or `Order`:

1. The application identifies the active business from the webhook phone-number ID.
2. If that business has a WhatsApp Catalog ID and eligible active, in-stock products in active categories with retailer IDs that are confirmed **Synced** to Meta, it sends a WhatsApp product list. The list contains up to 30 products across up to 10 sections.
3. If no catalog list is configured/eligible, it falls back to a numbered, paged menu (six products per page). Customers can also search by item name or use `Order: 1 x 2, 4 x 1`.
4. A customer may send a WhatsApp catalog cart. The application accepts only retailer IDs that map to confirmed-synced, active, available local products in active categories, then shows the cart.
5. The customer selects quantity (buttons offer 1-3; typed quantities from 1-100 are accepted), reviews the cart, chooses delivery or pickup, gives a name/address when needed, and replies **Yes** to confirm.
6. The system creates a `PendingConfirmation` WhatsApp order, stores the current tax values with it, and notifies configured staff by email and/or WhatsApp when sending is enabled.

Customers can cancel, ask for the business address, or ask to connect to staff. Staff-handover requests notify the configured notification email when SMTP is enabled.

## 7. Troubleshooting

| Symptom | Check |
| --- | --- |
| Admin returns 403 outside localhost | `Admin__AllowRemote` is false by default. Enable remote access only when intended. |
| Sign-in succeeds but a workspace is unavailable | Confirm the user is active, has the required role, and (for Portal) has the correct business assignment. |
| No WhatsApp catalog list | Confirm Catalog ID, active/in-stock products, retailer IDs, and that the corresponding products exist in the existing Meta catalog. Otherwise customers receive the local numbered menu when no catalog is configured/eligible. |
| Queue shows `Simulated` | This is expected with `META_CATALOG_DISABLE_SENDING=true`; no remote product was created. |
| Queue shows `Waiting` or `Paused` | `Waiting` follows an earlier event for the same product; if that earlier row is `Failed`, correct it and retry that **Failed** row to preserve remote ordering. `Paused` means the business connection is disabled or incomplete; re-enable the same connection to resume paused work. |
| Queue shows `Failed` | Review the latest error and Meta credentials/catalog/retailer ID/image URL, then retry after correcting the issue. |
| Customer or staff messages are skipped | Set a valid WhatsApp access token and `WhatsApp__DisableSending=false`; ensure the business phone number ID and recipient number are valid. |
| Webhook is rejected or ignored | Verify the webhook URL/token, app-secret signature, active business state, and matching WhatsApp Phone Number ID. Duplicate message IDs are ignored deliberately. |
| Application cannot start or cannot read data | Check that all hosts use the same valid `DataEncryption__Key`, database connection string, and signing key. |

## 8. Security and operating limits

- Secrets, tokens, SQL credentials, and encryption keys belong in Secret Manager, environment configuration, or a production secret store - never source control.
- Passwords are managed by ASP.NET Identity; browser access uses authenticated cookie sessions.
- Sensitive customer, order, message, contact, address, and Meta-token fields are encrypted at rest with AES-256-GCM. The shared data-encryption key is essential for recovery.
- Business Portal data is tenant-scoped. A Portal user cannot access another business's catalog, customers, or orders through the normal application workflow.
- Catalog tokens, catalog-ID changes, and sync queue controls are `PlatformAdmin` operations. They are not exposed to business Portal roles.
- Keep product names and category names concise for WhatsApp: list titles are truncated to 24 characters; customer product lists are limited to 30 products; interactive reply messages use at most three buttons.
- A public HTTPS image URL is required for live Meta synchronization. Velonixs does not host or upload product images, and Meta must be able to retrieve the supplied URL.

For implementation and deployment detail, see [local setup](local-setup.md), [WhatsApp integration](WhatsApp-Integration.md), and [data security](Data-Security.md).
