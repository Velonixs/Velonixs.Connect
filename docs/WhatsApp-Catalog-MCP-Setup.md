# Native WhatsApp Catalog Setup

This guide deploys Velonixs Connect's native WhatsApp catalog, cart-order webhook, and Meta catalog synchronization. It describes the current implementation; Meta's console labels, eligibility rules, and supported Graph API versions can change, so validate them against the official links before each production rollout.

No secrets are included in this document. Keep all tokens, signing keys, and app secrets in a managed secret store or deployment configuration; never commit them or send them in a query string.

## What the feature needs

- A Meta business portfolio, WhatsApp Business Account (WABA), registered business phone number, and an eligible commerce catalog associated with that WhatsApp business. The catalog can be created and associated from Velonixs Admin or can be an existing Meta catalog.
- An HTTPS API host reachable by Meta at `https://<api-host>/api/webhooks/whatsapp`.
- An API deployment running the catalog background worker. The Admin and Portal hosts do not own the worker.
- At least one active, available menu item that has successfully synchronized. Each product being created or updated needs a public, non-loopback HTTPS image URL; this implementation rejects an item without one.

The local Velonixs menu is the source of truth. Do not use Meta as the authoritative place to edit restaurant prices or availability. The Admin application's **Create catalog in Meta** action can create the commerce catalog, attach it to the configured WABA, save the returned catalog ID, and queue the locally managed products; a catalog already created in Meta can also be connected by entering its ID.

## Host configuration

Set the following through environment variables, Azure App Service configuration, .NET user secrets, or another secret provider. Double underscores map to configuration sections.

```text
# Required API authentication
Auth__RequireAuthentication=true
Auth__Issuer=<issuer>
Auth__Audience=<audience>
Auth__SigningKey=<managed-secret, at-least-32-characters>

# WhatsApp Cloud API and webhook
WhatsApp__BaseUrl=https://graph.facebook.com
WhatsApp__ApiVersion=<currently-supported-graph-version>
WhatsApp__VerifyToken=<managed-secret>
WhatsApp__AccessToken=<managed-secret>
WhatsApp__AppSecret=<managed-secret>
WhatsApp__DisableSending=false
WhatsApp__SendTimeoutSeconds=10

# Meta catalog synchronization and commerce validation
MetaCatalog__GraphApiBaseUrl=https://graph.facebook.com
MetaCatalog__GraphApiVersion=<currently-supported-graph-version>
MetaCatalog__Currency=INR
MetaCatalog__DisableSending=false
MetaCatalog__MaxRetryCount=5
MetaCatalog__BatchSize=20
MetaCatalog__WorkerIntervalSeconds=60
MetaCatalog__ProcessingLeaseSeconds=300
```

Use an active Graph version in both version settings; the current sample configuration uses `v26.0`, but that is not a promise that it will still be supported when deploying. `MetaCatalog__Currency` must be a three-letter ISO 4217 code and is used for all synced product prices.

Both `WhatsApp__DisableSending` and `MetaCatalog__DisableSending` must be `false` for a live catalog:

- The first enables real Cloud API messages. In a non-Development environment, startup also requires the global WhatsApp access token, app secret, and verify token.
- The second enables product synchronization and remote commerce-settings validation. When it is `true`, sync and commerce updates are simulated.

The `WHATSAPP_ACCESS_TOKEN`, `WHATSAPP_VERIFY_TOKEN`, `META_APP_SECRET`, `WHATSAPP_API_VERSION`, `META_CATALOG_GRAPH_API_BASE_URL`, `META_CATALOG_GRAPH_API_VERSION`, `META_CATALOG_CURRENCY`, and `META_CATALOG_DISABLE_SENDING` aliases are also supported by the API. Prefer the section-style names above for clarity.

## Meta manual setup

1. In Meta for Developers, create or select the app and add the WhatsApp product. Complete the required business, phone-number, and production-access checks for the intended account.
2. In WhatsApp Manager, record the WABA ID and the **phone number ID**. Register the business number if it is not already registered.
3. In the Velonixs Admin application, open the restaurant's **Catalog Sync** page. Save the Meta business ID, WABA ID, phone number ID, and access token with the connection disabled, then select **Create catalog in Meta**. Velonixs creates a commerce catalog, attaches it to that WABA, and saves the catalog ID automatically. Alternatively, enter the ID of an existing eligible catalog.
4. Create a system-user or other production access token with the minimum Meta permissions needed by the account. Cloud API message sending needs `whatsapp_business_messaging`; Meta documents `whatsapp_business_management` for WhatsApp business asset management. Add only any additional permissions Meta requires for the specific catalog-management setup.
5. Configure the webhook callback URL as `https://<api-host>/api/webhooks/whatsapp` and enter the exact `WhatsApp__VerifyToken` value. Subscribe the app/WABA to `messages`. Configure `WhatsApp__AppSecret` on the API so POST deliveries are validated using `X-Hub-Signature-256`; production webhook posts are rejected when that secret is absent or the signature is invalid.
6. Confirm the Catalog Sync page has the WABA ID, catalog ID, phone number ID, and restaurant access token, then enable the connection and choose `default`/`automatic` sync. The catalog settings phone number ID must exactly match the restaurant's `WhatsAppPhoneNumberId`, which is also how incoming webhooks are routed.
7. Queue **Sync all**, then wait for successful queue entries. Each active product needs a stable retailer ID, a non-negative price, and a publicly reachable HTTPS image. Correct failed rows and retry them; do not mark the catalog live based only on a simulated sync.
8. Call the commerce diagnostics endpoint and confirm that the catalog is configured, the phone number matches, the catalog is visible, the cart is enabled, and remote validation succeeded. If the account supports remote updates, the API can set catalog visibility/cart settings through the WhatsApp commerce-settings endpoint.

The webhook verifier in the deployed API uses the global `WhatsApp__VerifyToken`; a value saved in a restaurant's Meta catalog record is not used for inbound webhook verification. Keep the global callback credentials under platform control.

## Native catalog behavior and Meta constraints

Velonixs sends the supported Cloud API `interactive` `catalog_message` to `/{phone-number-id}/messages`. It opens the catalog associated with the sender's WhatsApp account; WhatsApp renders product selection, quantity controls, cart, and checkout UI.

- The recipient is an individual WhatsApp user. Messaging consent, the customer-service window, template requirements for business-initiated messages, policy restrictions, catalog review, and country/account eligibility remain Meta requirements.
- A sent catalog message needs at least one active, available, successfully synchronized product so that a thumbnail retailer ID can be supplied.
- A submitted native cart arrives in the `messages` webhook as `type: "order"`, with `catalog_id` and `product_items`. Velonixs treats retailer IDs as references and rechecks local product state and price; do not trust the submitted price for billing.
- Client UI and feature availability can differ by WhatsApp version and account eligibility. Test with the exact production WABA and phone number.

Useful official references:

- [Meta's WhatsApp Cloud API collection and setup overview](https://www.postman.com/meta/whatsapp-business-platform/documentation/wlk6lh4/whatsapp-cloud-api)
- [Meta's current catalog-message request example](https://www.postman.com/meta/whatsapp-business-platform/documentation/wlk6lh4/whatsapp-cloud-api?entity=request-13382743-c2de1330-ab8b-4ec9-9a58-dfe88481b82a)
- [Meta's inbound `order` webhook fields](https://www.postman.com/meta/whatsapp-business-platform/folder/1dtuocp/messages-object)

## Verify the restaurant connection

Use an authenticated catalog-manager token for the following API calls:

```text
GET  /api/restaurants/{restaurantId}/whatsapp/commerce/diagnostics
PUT  /api/restaurants/{restaurantId}/whatsapp/commerce/settings
POST /api/restaurants/{restaurantId}/whatsapp/catalog/test
GET  /api/restaurants/{restaurantId}/catalog/status

POST /api/businesses/{businessId}/meta-sync/sync-all
GET  /api/businesses/{businessId}/meta-sync/queue
GET  /api/businesses/{businessId}/meta-sync/logs
```

The Meta sync settings and queue endpoints are platform-admin operations. Send a test catalog only after diagnostics are ready and at least one product is `Synced`. The expected result is WhatsApp's native **View Catalog** entry point, followed by a cart submission that becomes one order with multiple items when applicable.

## Production acceptance checklist

- [ ] API is HTTPS-only, healthy at `/health`, and its public webhook URL verifies in Meta.
- [ ] Webhook POST signatures are accepted with the configured app secret; unsigned production posts are rejected.
- [ ] Both live-sending flags are false, and no queue item is merely `Simulated`.
- [ ] Catalog and restaurant phone number IDs match, catalog visibility/cart diagnostics are ready, and products are `Synced`.
- [ ] A test customer sees the native catalog, can add more than one item and quantity, and submits one order.
