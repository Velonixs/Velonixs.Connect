# Velonixs Connect - Local Setup

## Run Locally

1. Open `Velonixs.Connect.sln` in Visual Studio.
2. Set `src/Velonixs.Connect.Api` as the startup project for WhatsApp webhooks and REST API.
3. Run the API `http` profile.
4. The API starts at `http://localhost:5077`.
5. Set `src/Velonixs.Connect.Admin` as the startup project for the platform admin UI.
6. Run the Admin `http` profile.
7. Open the admin UI at `http://localhost:5080/admin`.
8. Set `src/Velonixs.Connect.Portal` as the startup project for business staff operations.
9. Run the Portal `http` profile.
10. Open the business portal at `http://localhost:5090/portal`.

You can also configure Visual Studio for multiple startup projects and run both:

- `src/Velonixs.Connect.Api`
- `src/Velonixs.Connect.Admin`
- `src/Velonixs.Connect.Portal`

The admin UI is a separate project and is local-only by default. It returns `403` through any non-local host unless you explicitly set this on the Admin project:

```text
Admin__AllowRemote=true
```

Admin and Portal browser authentication is optional locally. To require sign-in for the browser UIs, seed a first admin user with the `Auth__DefaultAdmin*` settings below and enable:

```text
Admin__RequireAuthentication=true
Portal__RequireAuthentication=true
```

The browser sign-in pages are:

```text
http://localhost:5080/admin/login
http://localhost:5090/portal/login
```

On startup, the API auto-applies EF Core migrations and seeds a demo business/catalog when these settings are enabled:

```json
"RestaurantConnect": {
  "AutoMigrateDatabase": true,
  "SeedDemoData": true
}
```

The demo WhatsApp phone number id is:

```text
1196816620181240
```

The demo webhook verify token is:

```text
local-verify-token
```

## Local WhatsApp Flow Test

Use `src/Velonixs.Connect.Api/Velonixs.Connect.Api.http`.

Run these requests in order:

1. `GET /api/businesses`
2. Copy the business `id` into the `@RestaurantId` variable.
3. `GET /api/businesses/{restaurantId}/catalog`
4. Run the `local-test` requests:
   - `Hi`
   - `1`
   - `Order: 1 x 2, 4 x 1`
   - `Rajesh`
   - `Near Station Road, Jamtara`
   - `YES`
5. Run `GET /api/businesses/{restaurantId}/orders`.

## Projects

```text
src/Velonixs.Connect.Api             REST API, WhatsApp webhook, health endpoint
src/Velonixs.Connect.Admin           Platform admin UI for all businesses
src/Velonixs.Connect.Portal          Business staff UI for orders and catalog availability
src/Velonixs.Connect.Application     Service contracts and DTOs
src/Velonixs.Connect.Domain          Entities and domain constants
src/Velonixs.Connect.Infrastructure  Messaging, external integrations, application services
src/Velonixs.Connect.Persistence     EF Core, SQL Server, migrations, database initialization
```

When real WhatsApp sending is enabled, the welcome options and catalog are sent as WhatsApp interactive list messages. Customers can tap:

- View Menu
- Place Order
- Restaurant Location
- Talk to Staff

The catalog list lets a customer tap one product, then tap a quantity from 1 to 5. Multi-product orders still use text format, for example:

```text
Order: 1 x 2, 4 x 1
```

## Required Production Configuration

Set these values in environment variables, user secrets, Azure App Service configuration, or `appsettings.Production.json`.

### Authentication

Authentication plumbing is available but optional locally. To require JWT auth for API controllers, set:

```text
Auth__RequireAuthentication=true
Auth__Issuer=Velonixs.Connect
Auth__Audience=Velonixs.Connect
Auth__SigningKey=
Auth__TokenMinutes=120
```

To seed a first admin user on startup, configure both:

```text
Auth__DefaultAdminEmail=
Auth__DefaultAdminPassword=
Auth__DefaultAdminDisplayName=Velonixs Admin
```

Then request a bearer token from:

```text
POST /api/auth/login
```

Admin and Portal use cookie authentication for browser sessions. To protect the UI apps, set:

```text
Admin__RequireAuthentication=true
Portal__RequireAuthentication=true
```

### Database

```text
ConnectionStrings__RestaurantConnect=
```

Use an Azure SQL or SQL Server connection string.

### WhatsApp Cloud API

```text
WhatsApp__ApiVersion=v20.0
WhatsApp__VerifyToken=
WhatsApp__AccessToken=
WhatsApp__AppSecret=
WhatsApp__DisableSending=false
```

You need these from Meta:

- WhatsApp Business Cloud API permanent or system-user access token
- Webhook verify token you choose and also enter in Meta webhook setup
- Meta app secret
- WhatsApp phone number id

The phone number id must be saved on the business record as `WhatsAppPhoneNumberId`.

### SMTP Email Notification

```text
Smtp__EnableEmail=true
Smtp__Host=
Smtp__Port=587
Smtp__Username=
Smtp__Password=
Smtp__EnableSsl=true
Smtp__DefaultFromEmail=
Smtp__AdminEmail=
```

Each business can also have its own `NotificationEmail`.

## Meta Webhook URLs

After deployment, configure these in Meta:

```text
GET/POST https://your-domain.com/api/webhooks/whatsapp
```

Use your configured `WhatsApp__VerifyToken` during webhook verification.

## Useful Endpoints

```text
GET  /health
POST /api/auth/login
GET  /api/businesses
POST /api/businesses
GET  /api/businesses/{id}
PUT  /api/businesses/{id}
GET  /api/businesses/{restaurantId}/catalog
POST /api/businesses/{restaurantId}/catalog-categories
POST /api/businesses/{restaurantId}/catalog-products
PUT  /api/catalog-products/{id}
DELETE /api/catalog-products/{id}
GET  /api/businesses/{restaurantId}/orders
GET  /api/restaurants
POST /api/restaurants
GET  /api/restaurants/{id}
PUT  /api/restaurants/{id}
GET  /api/restaurants/{restaurantId}/menu
POST /api/restaurants/{restaurantId}/menu-categories
POST /api/restaurants/{restaurantId}/menu-items
PUT  /api/menu-items/{id}
DELETE /api/menu-items/{id}
GET  /api/restaurants/{restaurantId}/orders
GET  /api/orders/{id}
PUT  /api/orders/{id}/status
GET  /api/webhooks/whatsapp
POST /api/webhooks/whatsapp
POST /api/webhooks/whatsapp/local-test
```
