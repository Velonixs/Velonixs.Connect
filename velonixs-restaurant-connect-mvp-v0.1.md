# Velonixs Restaurant Connect — MVP v0.1

## 1. Product Overview

**Velonixs Restaurant Connect** is a WhatsApp-based restaurant order automation MVP that allows a restaurant to receive customer messages on WhatsApp, automatically reply, show menu items, capture basic orders, and notify the restaurant team for manual confirmation and fulfillment.

This MVP is intentionally simple. The goal is not to replace Swiggy/Zomato or build a full POS system in v0.1. The goal is to prove that restaurants can receive direct WhatsApp orders in a structured way with minimum manual effort.

---

## 2. MVP Version

**Product Name:** Velonixs Restaurant Connect  
**Version:** 0.1  
**Target Users:** Small and medium restaurants, cloud kitchens, cafes, local food outlets  
**Primary Channel:** WhatsApp  
**Admin Channel:** Web dashboard or restaurant notification channel  
**MVP Goal:** Capture WhatsApp orders and notify the restaurant clearly

---

## 3. Core Features

### MVP Feature List

1. Receive WhatsApp message
2. Auto reply to customer
3. Show restaurant menu
4. Capture customer order
5. Notify restaurant

---

## 4. Out of Scope for v0.1

The following features should not be built in MVP v0.1 unless absolutely required by the pilot restaurant:

- Online payment integration
- Delivery partner integration
- Live order tracking
- Table booking
- POS integration
- Inventory management
- Customer loyalty points
- Multi-branch support
- AI-based smart recommendations
- Coupon system
- Advanced analytics
- Mobile app for restaurant
- Customer login/signup

These can be planned for future versions after validating the MVP.

---

## 5. Target Restaurant Use Case

A customer sends a message to the restaurant’s WhatsApp number.

Example:

> Hi

The system replies automatically:

> Welcome to ABC Restaurant.  
> Please choose an option:  
> 1. View Menu  
> 2. Place Order  
> 3. Restaurant Location  
> 4. Talk to Staff

Customer selects menu, enters order details, and the system captures the order and sends a notification to the restaurant.

Restaurant staff then manually confirms the order with the customer.

---

## 6. User Roles

### 6.1 Customer

The customer interacts only through WhatsApp.

Customer can:

- Start conversation
- View menu
- Select or type order items
- Share name
- Share mobile number if needed
- Share address for delivery
- Confirm order summary

### 6.2 Restaurant Staff

Restaurant staff receives order notification.

Staff can:

- View order details
- Call or message customer
- Confirm order manually
- Prepare order
- Mark order as handled if dashboard exists

### 6.3 Velonixs Admin / Super Admin

Velonixs admin manages restaurant onboarding and configuration.

Admin can:

- Add restaurant
- Configure WhatsApp number
- Add/edit menu
- Set auto-reply messages
- Configure notification email/WhatsApp
- Monitor incoming orders

---

## 7. Customer WhatsApp Flow

### 7.1 First Message Flow

**Trigger:** Customer sends any message such as `Hi`, `Hello`, `Menu`, or any text.

**System Reply:**

```text
Welcome to {{RestaurantName}} 👋

Please choose an option:
1. View Menu
2. Place Order
3. Restaurant Location
4. Talk to Staff

Reply with option number.
```

---

### 7.2 View Menu Flow

**Customer Input:**

```text
1
```

**System Reply:**

```text
Here is our menu:

🍕 Pizza
1. Margherita Pizza - ₹199
2. Paneer Pizza - ₹249

🍔 Burger
3. Veg Burger - ₹99
4. Cheese Burger - ₹129

🍛 Main Course
5. Paneer Butter Masala - ₹220
6. Veg Biryani - ₹180

To place order, reply like:
Order: 1 x 2, 4 x 1
```

### 7.3 Capture Order Flow

**Customer Input Example:**

```text
Order: 1 x 2, 4 x 1
```

**System Reply:**

```text
Your order summary:

2 x Margherita Pizza - ₹398
1 x Cheese Burger - ₹129

Total: ₹527

Please reply with your name.
```

---

### 7.4 Capture Customer Name

**Customer Input:**

```text
Rajesh
```

**System Reply:**

```text
Thank you, Rajesh.

Please share your delivery address.
If this is pickup, reply: Pickup
```

---

### 7.5 Capture Address / Pickup

**Customer Input:**

```text
Near Station Road, Jamtara
```

**System Reply:**

```text
Please confirm your order:

Name: Rajesh
Order:
2 x Margherita Pizza
1 x Cheese Burger
Total: ₹527
Address: Near Station Road, Jamtara

Reply YES to confirm or NO to cancel.
```

---

### 7.6 Confirm Order

**Customer Input:**

```text
YES
```

**System Reply:**

```text
Thank you! Your order has been received.
Restaurant staff will confirm your order shortly.

Order ID: VRC-1001
```

**Restaurant Notification:**

```text
New WhatsApp Order Received

Order ID: VRC-1001
Customer: Rajesh
Phone: +91XXXXXXXXXX
Order:
2 x Margherita Pizza
1 x Cheese Burger
Total: ₹527
Address: Near Station Road, Jamtara

Please confirm with customer.
```

---

## 8. Conversation States

The system should maintain a simple conversation state for each customer.

| State | Description |
|---|---|
| NEW | Customer has sent first message |
| MENU_SENT | Menu has been shown |
| ORDER_INPUT_PENDING | Waiting for customer order |
| ORDER_RECEIVED | Order items captured |
| CUSTOMER_NAME_PENDING | Waiting for customer name |
| ADDRESS_PENDING | Waiting for delivery address or pickup |
| CONFIRMATION_PENDING | Waiting for YES/NO confirmation |
| CONFIRMED | Customer confirmed order |
| CANCELLED | Customer cancelled order |
| STAFF_HANDOVER | Customer wants to talk to staff |

---

## 9. Functional Requirements

### 9.1 Receive WhatsApp Message

The system must receive incoming WhatsApp messages through WhatsApp Business Cloud API webhook.

Requirements:

- Expose webhook endpoint
- Verify webhook with Meta challenge token
- Receive customer message
- Extract sender phone number
- Extract message text
- Store incoming message in database
- Identify restaurant based on WhatsApp phone number ID

Webhook endpoint example:

```http
POST /api/webhooks/whatsapp
```

---

### 9.2 Auto Reply

The system must send automated replies based on customer message and current conversation state.

Requirements:

- Send welcome message for new conversation
- Send menu when customer selects menu option
- Ask for required details step by step
- Send order confirmation message
- Send fallback message for invalid input

Fallback message example:

```text
Sorry, I could not understand your message.
Please reply with one of these options:
1. View Menu
2. Place Order
3. Restaurant Location
4. Talk to Staff
```

---

### 9.3 Show Menu

The system must show restaurant menu on WhatsApp.

For MVP v0.1, menu can be text-based.

Requirements:

- Store menu items in database
- Group menu items by category
- Show item number, name, and price
- Hide unavailable items if supported
- Support simple order format using item number and quantity

Example:

```text
1. Veg Burger - ₹99
2. Cheese Burger - ₹129
3. Paneer Pizza - ₹249
```

---

### 9.4 Capture Order

The system must capture simple order details from WhatsApp message.

Supported MVP order format:

```text
1 x 2, 3 x 1
```

Meaning:

- Item 1 quantity 2
- Item 3 quantity 1

Alternative supported format:

```text
1-2, 3-1
```

Requirements:

- Parse item number and quantity
- Validate item exists
- Calculate item total
- Calculate final total
- Ask customer for name
- Ask for delivery address or pickup
- Ask for final confirmation
- Store order in database

---

### 9.5 Notify Restaurant

The system must notify restaurant when customer confirms order.

MVP notification options:

1. Email notification
2. WhatsApp notification to restaurant owner/staff number
3. Dashboard notification

Recommended MVP approach:

- Start with email notification and dashboard entry
- Add WhatsApp staff notification if feasible

Notification must include:

- Order ID
- Customer phone number
- Customer name
- Order items
- Quantity
- Total amount
- Address or pickup
- Timestamp
- Customer message link or conversation reference

---

## 10. Non-Functional Requirements

### 10.1 Performance

- Auto reply should be sent within 2–5 seconds
- Webhook should return success quickly
- Long processing should be handled asynchronously if needed

### 10.2 Security

- Validate WhatsApp webhook signature if possible
- Store tokens securely in environment variables
- Do not expose access tokens in frontend
- Use HTTPS only
- Restrict admin dashboard access

### 10.3 Reliability

- Store every incoming and outgoing message
- Log failed WhatsApp API calls
- Retry failed restaurant notifications
- Avoid duplicate order creation for repeated webhook events

### 10.4 Privacy

- Store only required customer data
- Do not collect sensitive personal information
- Allow restaurant to delete customer/order data if requested

---

## 11. Suggested Tech Stack

Since the product can be built using .NET and Azure, the following stack is recommended.

### Backend

- ASP.NET Core Web API
- C#
- Entity Framework Core
- REST APIs

### Database

MVP options:

- Azure SQL Database
- PostgreSQL
- SQL Server Express for local development

### Hosting

- Azure App Service
- Azure Static Web Apps if frontend is separate
- Azure SQL for database

### Frontend / Admin Dashboard

MVP options:

- Razor Pages
- Blazor Server
- React
- Simple Bootstrap admin panel

For fastest MVP, use:

- ASP.NET Core MVC or Razor Pages
- Bootstrap

### WhatsApp Integration

- WhatsApp Business Cloud API by Meta
- Webhook verification
- Send message API

### Notification

- SMTP email
- SendGrid
- WhatsApp message to restaurant staff

---

## 12. High-Level Architecture

```text
Customer WhatsApp
      |
      v
WhatsApp Business Cloud API
      |
      v
Velonixs Webhook API
      |
      v
Conversation Engine
      |
      +--> Database
      |
      +--> WhatsApp Reply Service
      |
      +--> Order Service
      |
      +--> Restaurant Notification Service
      |
      v
Restaurant Staff / Admin Dashboard
```

---

## 13. Database Design

### 13.1 Restaurants

| Column | Type | Description |
|---|---|---|
| Id | Guid / int | Primary key |
| Name | string | Restaurant name |
| WhatsAppPhoneNumberId | string | Meta phone number ID |
| BusinessPhone | string | Restaurant contact number |
| NotificationEmail | string | Email for order notifications |
| StaffWhatsAppNumber | string | Staff WhatsApp number |
| Address | string | Restaurant address |
| IsActive | bool | Active/inactive |
| CreatedAt | datetime | Created timestamp |

---

### 13.2 MenuCategories

| Column | Type | Description |
|---|---|---|
| Id | Guid / int | Primary key |
| RestaurantId | Guid / int | Restaurant reference |
| Name | string | Category name |
| DisplayOrder | int | Sort order |
| IsActive | bool | Active/inactive |

---

### 13.3 MenuItems

| Column | Type | Description |
|---|---|---|
| Id | Guid / int | Primary key |
| RestaurantId | Guid / int | Restaurant reference |
| CategoryId | Guid / int | Category reference |
| ItemCode | int | Customer-facing item number |
| Name | string | Item name |
| Description | string | Optional description |
| Price | decimal | Item price |
| IsAvailable | bool | Availability |
| IsActive | bool | Active/inactive |

---

### 13.4 Customers

| Column | Type | Description |
|---|---|---|
| Id | Guid / int | Primary key |
| RestaurantId | Guid / int | Restaurant reference |
| PhoneNumber | string | WhatsApp number |
| Name | string | Customer name |
| LastAddress | string | Last used address |
| CreatedAt | datetime | Created timestamp |
| LastInteractionAt | datetime | Last message time |

---

### 13.5 Conversations

| Column | Type | Description |
|---|---|---|
| Id | Guid / int | Primary key |
| RestaurantId | Guid / int | Restaurant reference |
| CustomerId | Guid / int | Customer reference |
| WhatsAppNumber | string | Customer WhatsApp number |
| CurrentState | string | Current conversation state |
| TempOrderJson | string | Temporary order data |
| IsActive | bool | Active conversation |
| CreatedAt | datetime | Created timestamp |
| UpdatedAt | datetime | Updated timestamp |

---

### 13.6 Messages

| Column | Type | Description |
|---|---|---|
| Id | Guid / int | Primary key |
| RestaurantId | Guid / int | Restaurant reference |
| CustomerId | Guid / int | Customer reference |
| ConversationId | Guid / int | Conversation reference |
| Direction | string | Incoming/Outgoing |
| MessageText | string | Message content |
| WhatsAppMessageId | string | Meta message ID |
| Status | string | Received/Sent/Failed |
| CreatedAt | datetime | Created timestamp |

---

### 13.7 Orders

| Column | Type | Description |
|---|---|---|
| Id | Guid / int | Primary key |
| OrderNumber | string | Example: VRC-1001 |
| RestaurantId | Guid / int | Restaurant reference |
| CustomerId | Guid / int | Customer reference |
| CustomerName | string | Customer name |
| CustomerPhone | string | WhatsApp phone number |
| Address | string | Delivery address or Pickup |
| OrderStatus | string | Pending/Confirmed/Cancelled/Handled |
| TotalAmount | decimal | Order total |
| Source | string | WhatsApp |
| CreatedAt | datetime | Created timestamp |

---

### 13.8 OrderItems

| Column | Type | Description |
|---|---|---|
| Id | Guid / int | Primary key |
| OrderId | Guid / int | Order reference |
| MenuItemId | Guid / int | Menu item reference |
| ItemName | string | Snapshot of item name |
| UnitPrice | decimal | Snapshot of price |
| Quantity | int | Quantity ordered |
| LineTotal | decimal | UnitPrice x Quantity |

---

## 14. API Endpoints

### 14.1 WhatsApp Webhook Verification

```http
GET /api/webhooks/whatsapp
```

Purpose:

- Verify webhook with Meta
- Return challenge when token is valid

Query parameters:

- hub.mode
- hub.verify_token
- hub.challenge

---

### 14.2 Receive WhatsApp Message

```http
POST /api/webhooks/whatsapp
```

Purpose:

- Receive incoming WhatsApp message
- Store message
- Process conversation state
- Send auto reply

---

### 14.3 Restaurant APIs

```http
GET /api/restaurants
POST /api/restaurants
GET /api/restaurants/{id}
PUT /api/restaurants/{id}
```

---

### 14.4 Menu APIs

```http
GET /api/restaurants/{restaurantId}/menu
POST /api/restaurants/{restaurantId}/menu-items
PUT /api/menu-items/{id}
DELETE /api/menu-items/{id}
```

---

### 14.5 Order APIs

```http
GET /api/restaurants/{restaurantId}/orders
GET /api/orders/{id}
PUT /api/orders/{id}/status
```

---

## 15. WhatsApp Message Templates

### 15.1 Welcome Message

```text
Welcome to {{RestaurantName}} 👋

Please choose an option:
1. View Menu
2. Place Order
3. Restaurant Location
4. Talk to Staff

Reply with option number.
```

---

### 15.2 Menu Message

```text
Here is our menu:

{{MenuItems}}

To place order, reply like:
Order: 1 x 2, 4 x 1
```

---

### 15.3 Invalid Input Message

```text
Sorry, I could not understand your message.
Please reply with a valid option or type Menu.
```

---

### 15.4 Order Summary Message

```text
Your order summary:

{{OrderItems}}

Total: ₹{{TotalAmount}}

Please reply with your name.
```

---

### 15.5 Final Confirmation Message

```text
Please confirm your order:

Name: {{CustomerName}}
Order:
{{OrderItems}}
Total: ₹{{TotalAmount}}
Address: {{Address}}

Reply YES to confirm or NO to cancel.
```

---

### 15.6 Order Received Message

```text
Thank you! Your order has been received.
Restaurant staff will confirm your order shortly.

Order ID: {{OrderNumber}}
```

---

### 15.7 Restaurant Notification Message

```text
New WhatsApp Order Received

Order ID: {{OrderNumber}}
Customer: {{CustomerName}}
Phone: {{CustomerPhone}}
Order:
{{OrderItems}}
Total: ₹{{TotalAmount}}
Address: {{Address}}

Please confirm with customer.
```

---

## 16. Admin Dashboard Pages

For MVP v0.1, keep dashboard simple.

### 16.1 Login Page

Basic admin login.

Fields:

- Email
- Password

---

### 16.2 Restaurant Setup Page

Fields:

- Restaurant name
- WhatsApp phone number ID
- Business phone
- Notification email
- Staff WhatsApp number
- Address
- Active/inactive

---

### 16.3 Menu Management Page

Features:

- Add category
- Add menu item
- Edit item name
- Edit price
- Mark item available/unavailable
- Delete/deactivate item

---

### 16.4 Orders Page

Columns:

- Order number
- Customer name
- Customer phone
- Total amount
- Address/pickup
- Status
- Created time
- Action

Actions:

- View order
- Mark as handled
- Cancel order

---

### 16.5 Order Detail Page

Show:

- Customer details
- Order items
- Total amount
- Address
- WhatsApp conversation history if available
- Status update option

---

## 17. MVP Conversation Logic

### 17.1 Basic Rules

- If customer says `Hi`, `Hello`, `Menu`, or any unknown message in NEW state, send welcome menu.
- If customer replies `1`, show menu.
- If customer replies `2`, ask them to send order format.
- If customer replies `3`, send restaurant location/address.
- If customer replies `4`, notify restaurant for manual follow-up.
- If message starts with `Order:` or matches item quantity format, parse order.
- If order is valid, ask for name.
- After name, ask for address or pickup.
- After address, ask for YES/NO confirmation.
- If YES, create confirmed order and notify restaurant.
- If NO, cancel temporary order.

---

## 18. Error Handling

### Invalid Menu Item

```text
Some items in your order are not valid.
Please check the menu and try again.
Example: Order: 1 x 2, 4 x 1
```

### Invalid Quantity

```text
Please enter valid quantity.
Example: Order: 1 x 2, 4 x 1
```

### Empty Menu

```text
Menu is currently not available. Please contact restaurant staff.
```

### Notification Failure

If notification fails:

- Save failure log
- Mark order as `NotificationFailed`
- Retry notification
- Show order in dashboard

---

## 19. Order Statuses

| Status | Description |
|---|---|
| Draft | Customer is still entering details |
| PendingConfirmation | Waiting for final YES/NO |
| Confirmed | Customer confirmed order |
| Notified | Restaurant has been notified |
| Handled | Restaurant staff handled order |
| Cancelled | Customer cancelled order |
| Failed | System failed to process order |

---

## 20. Environment Variables

```env
WHATSAPP_ACCESS_TOKEN=
WHATSAPP_VERIFY_TOKEN=
WHATSAPP_API_VERSION=v20.0
META_APP_SECRET=
DATABASE_CONNECTION_STRING=
SMTP_HOST=
SMTP_PORT=
SMTP_USERNAME=
SMTP_PASSWORD=
DEFAULT_FROM_EMAIL=
ADMIN_EMAIL=
```

---

## 21. Folder Structure Suggestion

```text
Velonixs.Connect/

Velonixs.Connect.Api/
    Controllers/
      WhatsAppWebhookController.cs
      RestaurantsController.cs
      MenuController.cs
      OrdersController.cs

Velonixs.Connect.Admin/
  Controllers/
  Models/
  Views/
  wwwroot/

Velonixs.Connect.Portal/
  Controllers/
  Models/
  Views/
  wwwroot/

Velonixs.Connect.Application/
  Abstractions/
  Models/

Velonixs.Connect.Domain/
  Entities/

Velonixs.Connect.Infrastructure/
  Configuration/
  Persistence/
  Services/

tests/
  Velonixs.Connect.Tests/

docs/
  mvp-v0.1.md
```

---

## 22. Suggested Development Milestones

### Day 1 — Project Setup

Tasks:

- Create ASP.NET Core solution
- Setup database
- Create core entities
- Configure EF Core migrations
- Setup environment variables
- Create basic health check endpoint

Deliverable:

- Backend project running locally
- Database connected

---

### Day 2 — WhatsApp Webhook

Tasks:

- Create webhook verification endpoint
- Create webhook receive endpoint
- Parse incoming WhatsApp payload
- Store incoming messages
- Send basic reply through WhatsApp API

Deliverable:

- Customer sends WhatsApp message and receives auto reply

---

### Day 3 — Menu Management

Tasks:

- Create restaurant table
- Create menu category table
- Create menu item table
- Seed sample restaurant and menu
- Generate menu text message

Deliverable:

- Customer can request and receive menu

---

### Day 4 — Conversation Engine

Tasks:

- Add conversation state management
- Handle menu option selection
- Capture order input
- Validate item and quantity
- Calculate total

Deliverable:

- Customer can select menu items and receive order summary

---

### Day 5 — Order Capture

Tasks:

- Capture customer name
- Capture address or pickup
- Confirm YES/NO
- Create order and order items
- Generate order number

Deliverable:

- Complete WhatsApp order is stored in database

---

### Day 6 — Restaurant Notification

Tasks:

- Send order notification email
- Optional: send WhatsApp notification to staff
- Create basic orders dashboard
- Add order status update

Deliverable:

- Restaurant receives clear order notification

---

### Day 7 — Pilot Testing

Tasks:

- Test full flow with sample restaurant
- Fix bugs
- Improve message wording
- Add logging
- Deploy to Azure
- Connect production webhook

Deliverable:

- MVP v0.1 ready for pilot restaurant demo

---

## 23. MVP Acceptance Criteria

The MVP is considered complete when:

- Customer can send WhatsApp message to restaurant number
- System sends automatic welcome reply
- Customer can view menu
- Customer can place order using item number and quantity
- System calculates total amount
- System captures name and address/pickup
- Customer can confirm order
- Order is stored in database
- Restaurant receives order notification
- Admin can view order details
- Basic error handling works
- System is deployed and accessible publicly over HTTPS

---

## 24. Demo Script for Restaurant Owner

Use this script during demo.

### Step 1

Ask restaurant owner to send `Hi` to WhatsApp number.

Expected result:

- Auto welcome message received

### Step 2

Ask them to reply `1`.

Expected result:

- Menu appears on WhatsApp

### Step 3

Ask them to send:

```text
Order: 1 x 2, 3 x 1
```

Expected result:

- Order summary appears with total

### Step 4

Enter customer name.

Expected result:

- System asks for address or pickup

### Step 5

Enter address or `Pickup`.

Expected result:

- System asks for final confirmation

### Step 6

Reply `YES`.

Expected result:

- Customer receives order received message
- Restaurant receives order notification
- Order appears in dashboard

---

## 25. Pilot Restaurant Pitch

Simple pitch:

```text
This system helps you receive direct orders from WhatsApp without asking the customer again and again for menu, items, quantity, name, and address.

Customer sends WhatsApp message, system shows menu, captures order, and sends you a clean order notification.

You still stay in control. The system does not auto-confirm or auto-deliver. Your staff can call or message customer before preparing the order.
```

---

## 26. Pricing Idea for Future

Do not focus on pricing during MVP pilot, but possible future pricing:

### Starter

- ₹999/month
- 1 restaurant
- WhatsApp auto reply
- Menu
- Order capture
- Email notification

### Growth

- ₹1,999/month
- WhatsApp staff notification
- Dashboard
- Customer history
- Basic analytics

### Pro

- ₹3,999/month+
- Multi-user dashboard
- Offers
- Reports
- Integrations

For pilot customer, recommended approach:

- Free for 15–30 days
- Ask for feedback
- Convert to paid only after value is proven

---

## 27. Recommended v0.1 Strategy

Build only what helps close the first pilot:

1. WhatsApp incoming message
2. Auto reply
3. Text menu
4. Simple order format
5. Customer name/address capture
6. Restaurant notification
7. Basic order dashboard

Avoid unnecessary complexity.

The MVP should prove this question:

> Will a restaurant owner use WhatsApp automation to capture direct customer orders more efficiently?

If the answer is yes, then future versions can add payment, analytics, offers, customer database, and delivery workflow.

---

## 28. Future Version Ideas

### Version 0.2

- Better admin dashboard
- Editable message templates
- Order status update
- Customer repeat order
- Menu availability toggle

### Version 0.3

- WhatsApp interactive buttons/lists
- Payment link
- Offers and coupons
- Customer database

### Version 1.0

- Full SaaS onboarding
- Multi-restaurant support
- Billing
- Analytics
- Team access
- Subscription management

---

## 29. Final MVP Definition

Velonixs Restaurant Connect v0.1 is complete when a real restaurant can receive a customer order from WhatsApp without manually asking for menu selection, quantity, name, and address.

The restaurant should receive one clear notification containing all order details and then manually confirm the order with the customer.

This is enough for first pilot validation.
