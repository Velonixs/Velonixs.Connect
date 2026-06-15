# Velonixs Restaurant Connect - Local Setup

## Run Locally

1. Open `Velonixs.RestaurantConnect.sln` in Visual Studio.
2. Set `Velonixs.Restaurant.Api` as the startup project.
3. Run the `http` profile.
4. The API starts at `http://localhost:5077`.

On startup, the API auto-applies EF Core migrations and seeds a demo restaurant/menu when these settings are enabled:

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

Use `src/Velonixs.Restaurant.Api/Velonixs.Restaurant.Api.http`.

Run these requests in order:

1. `GET /api/restaurants`
2. Copy the restaurant `id` into the `@RestaurantId` variable.
3. `GET /api/restaurants/{restaurantId}/menu`
4. Run the `local-test` requests:
   - `Hi`
   - `1`
   - `Order: 1 x 2, 4 x 1`
   - `Rajesh`
   - `Near Station Road, Jamtara`
   - `YES`
5. Run `GET /api/restaurants/{restaurantId}/orders`.

When real WhatsApp sending is enabled, the welcome options and menu are sent as WhatsApp interactive list messages. Customers can tap:

- View Menu
- Place Order
- Restaurant Location
- Talk to Staff

The menu list lets a customer tap one menu item, then tap a quantity from 1 to 5. Multi-item orders still use text format, for example:

```text
Order: 1 x 2, 4 x 1
```

## Required Production Configuration

Set these values in environment variables, user secrets, Azure App Service configuration, or `appsettings.Production.json`.

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

The phone number id must be saved on the restaurant record as `WhatsAppPhoneNumberId`.

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

Each restaurant can also have its own `NotificationEmail`.

## Meta Webhook URLs

After deployment, configure these in Meta:

```text
GET/POST https://your-domain.com/api/webhooks/whatsapp
```

Use your configured `WhatsApp__VerifyToken` during webhook verification.

## Useful Endpoints

```text
GET  /health
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
