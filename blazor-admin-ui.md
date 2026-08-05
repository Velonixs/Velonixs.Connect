# Blazor Admin UI Specification

## 1. Purpose

The Blazor admin UI will provide a modern, responsive control center for business operations in Velonixs Connect. It will replace the current server-rendered admin experience with a component-driven interface for catalog, orders, customer management, and sync operations.

## 2. Goals

- Provide a unified admin experience for restaurants and other businesses
- Make catalog management fast and intuitive
- Support order monitoring and status changes from one screen
- Expose WhatsApp and Meta configuration in a secure admin section
- Prepare the solution for future expansion into multi-tenant operations

## 3. Recommended Stack

- Blazor Web App or Blazor Server
- MudBlazor for layout, forms, dialogs, tables, and notifications
- ASP.NET Core APIs for business operations
- ASP.NET Identity for authentication and authorization
- EF Core for data access
- SignalR for live updates (optional for order activity)

## 4. Navigation Structure

- Dashboard
- Products
- Categories
- Orders
- Customers
- Catalog Sync
- Settings

## 5. Module Requirements

### 5.1 Dashboard

Show summary cards for:
- Total Products
- Active Products
- Synced Products
- Pending Sync
- Today’s Orders
- Total Revenue

Also include:
- Recent orders panel
- Recent sync failures panel
- Quick actions

### 5.2 Products

Provide a searchable, filterable grid with columns:
- Image
- Name
- Category
- Price
- Availability
- Active Status
- Sync Status
- Actions

Actions:
- Add Product
- Edit Product
- Deactivate Product
- Sync Product
- View History

### 5.3 Product Form

Fields:
- Product Name
- Product Code / SKU
- Category
- Price
- Description
- Image Upload or URL
- Available
- Active

Buttons:
- Save
- Save & Sync
- Cancel

Validation:
- Required product name
- Valid category selection
- Positive price value
- Valid image URL or uploaded file

### 5.4 Categories

Provide a management screen with:
- Add Category
- Edit Category
- Activate / Deactivate Category
- Delete Category (only when unused)

Display:
- Category name
- Display order
- Active flag
- Product count

### 5.5 Orders

Display an orders grid with columns:
- Order No
- Customer
- WhatsApp Number
- Amount
- Status
- Created Time

Actions:
- Confirm Order
- Mark Preparing
- Mark Ready
- Mark Delivered
- Reject / Cancel

### 5.6 Customers

Provide a customer overview with:
- Customer name
- WhatsApp number
- Last interaction
- Total orders
- Order history

### 5.7 Catalog Sync

Provide a sync management page with:
- Sync All
- Sync Selected
- Retry Failed
- View Logs
- Filter by status

Show statuses:
- Pending
- Synced
- Failed

### 5.8 Settings

Organize settings into sections:
- Business Details
- WhatsApp Configuration
- Meta Configuration
- Access Token
- Catalog ID
- Webhook Settings
- Security and Audit Preferences

## 6. Recommended UI Components

- MudBlazor DataGrid for catalog and orders
- MudDialog for add/edit forms
- MudSnackbar for success and error feedback
- MudFileUpload for product images
- MudTabs for settings sections
- MudTable for lightweight lists
- Search and filter toolbar
- Breadcrumb navigation

## 7. API Integration Pattern

The UI should call existing API endpoints such as:
- GET /api/businesses/{businessId}/catalog
- POST /api/businesses/{businessId}/catalog-categories
- POST /api/businesses/{businessId}/catalog-products
- PUT /api/catalog-products/{id}
- DELETE /api/catalog-products/{id}
- GET /api/orders
- POST /api/orders/{id}/confirm

Use:
- typed DTOs
- centralized service layer
- exception handling for API failures
- toast notifications for outcomes

## 8. Implementation Plan

### Phase 1 - Admin Shell
- Create the Blazor app shell
- Add authentication and role-based routing
- Build the navigation layout
- Add dashboard summary cards

### Phase 2 - Catalog Management
- Implement products page
- Implement categories page
- Build add/edit forms
- Connect to catalog API endpoints

### Phase 3 - Orders and Customers
- Build order management grid
- Add status actions
- Add customer profile and order history

### Phase 4 - Sync and Settings
- Add sync management page
- Add Meta and WhatsApp configuration screens
- Add logging and retry support

## 9. Acceptance Criteria

The Blazor admin UI is complete when:
- An admin can browse catalog items and categories
- An admin can create, edit, and deactivate products
- An admin can manage order statuses from the UI
- Sync operations and failures are visible in the interface
- Settings for WhatsApp and Meta are configurable through the UI
- The experience is responsive and accessible on desktop and tablet devices
