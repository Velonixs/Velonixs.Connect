# Velonixs Connect - WhatsApp Catalog Management

## 1. Purpose

Velonixs Connect uses a WhatsApp-first catalog experience so businesses can publish products, let customers browse them inside WhatsApp, and receive orders directly through the WhatsApp webhook flow.

The catalog module is the foundation for product discovery, order capture, and business operations in the MVP.

## 2. Goals

- Allow business admins to manage categories and products from the admin experience.
- Support a customer-facing catalog that can be shared in WhatsApp conversations.
- Capture customer orders and route them to the existing order workflow.
- Keep the design flexible for restaurants, grocery stores, bakeries, and other local businesses.

## 3. Scope

### In Scope for MVP
- Category management
- Product management
- Product availability and active/inactive state
- Basic catalog retrieval for business admin and customer flows
- Order intake from WhatsApp catalog interactions
- Basic catalog sync metadata for future Meta integration

### Out of Scope for MVP
- Full image library management
- Advanced inventory control
- Product variants and modifiers
- Bulk import/export
- Multi-branch catalog inheritance
- Advanced analytics

## 4. User Roles

- Business Admin: manages categories, products, pricing, availability, and catalog visibility
- Staff: reviews incoming orders and confirms them
- Customer: browses the catalog and submits an order through WhatsApp

## 5. Functional Requirements

### 5.1 Category Management

Admins must be able to:
- Create a category
- Edit a category
- Activate or deactivate a category
- View products grouped by category

Suggested category examples:
- Pizza
- Burgers
- Beverages
- Desserts
- Milk
- Grocery

### 5.2 Product Management

Admins must be able to:
- Create a product
- Edit a product
- Deactivate a product
- Toggle availability on/off
- Assign the product to a category
- Set pricing and description

### 5.3 Product Fields

| Field | Required | Notes |
| --- | --- | --- |
| Product Name | Yes | Displayed in catalog |
| SKU / Product Code | Yes | Unique business-level identifier |
| Category | Yes | Must belong to a valid category |
| Price | Yes | Stored as decimal currency |
| Description | Optional | Short product description |
| Image | Optional | URL or stored asset reference |
| Availability | Yes | Indicates whether the product is currently available |
| Active Status | Yes | Controls whether the product is visible in the catalog |
| Meta Product Id | Auto / Optional | Used for future Meta catalog sync |
| Sync Status | Auto | Pending, Synced, Failed |

## 6. Customer Experience Flow

1. Customer sends a greeting message such as "Hi".
2. The system replies with a welcome message and a catalog link or catalog preview.
3. The customer browses the catalog.
4. The customer adds items to a cart-like order draft.
5. The customer submits the order.
6. The order is recorded and surfaced in the admin/order workflow.

Example welcome message:

Hi 👋 Welcome to {{BusinessName}}

Please view our catalog below.

{{Catalog Link}}

Add items to your cart and send the order.

## 7. Admin Workflow

1. Business admin signs in to the admin panel.
2. Admin creates or updates catalog categories.
3. Admin creates or updates products with pricing and availability.
4. Admin optionally syncs the catalog to Meta/WhatsApp catalog services.
5. Admin monitors order intake and confirms orders.

## 8. API Surface

The current implementation exposes catalog-oriented endpoints for business catalog operations.

### Catalog APIs
- GET /api/businesses/{businessId}/catalog
- POST /api/businesses/{businessId}/catalog-categories
- POST /api/businesses/{businessId}/catalog-products
- PUT /api/catalog-products/{id}
- DELETE /api/catalog-products/{id}

### Order APIs
- GET /api/orders
- POST /api/orders/{id}/confirm
- POST /api/webhook

## 9. Technical Architecture

- Admin UI: Blazor / ASP.NET MVC admin experience
- API layer: ASP.NET Core Web API
- Application layer: catalog services, business access rules, DTOs
- Data layer: EF Core with SQL Server
- Messaging: WhatsApp Cloud API and webhook integration
- Optional future integration: Meta catalog APIs and product sync jobs

## 10. Data Model

### Core entities
- Businesses / Restaurants
- Categories
- Products / Menu Items
- Product Images
- Catalog Sync Logs
- Customers
- Orders
- Order Items
- WhatsApp Messages

### Suggested sync states
- Pending
- Synced
- Failed

## 11. Current Implementation Status

The repository already includes a working foundation for catalog management:
- Category and product CRUD flows are available through the catalog service layer
- Menu and catalog retrieval APIs are available for business-scoped access
- Admin and order workflows already exist around the catalog data model

Remaining enhancements for full alignment with this spec:
- Full category update/delete lifecycle
- Richer product metadata such as SKU and image handling
- Explicit sync status tracking and Meta product identifiers
- Catalog sync jobs and retry management

## 12. Delivery Phases

### Phase 1 - Core Catalog Management
- Category management
- Product management
- Availability and active-state control

### Phase 2 - Catalog Sync
- Meta catalog synchronization
- Sync status tracking
- Retry failed syncs

### Phase 3 - WhatsApp Customer Ordering
- Catalog sharing in WhatsApp
- Cart/order submission
- Webhook-driven order capture

### Phase 4 - Business Operations
- Order dashboard
- Confirmation workflow
- Staff notifications

## 13. Acceptance Criteria

The catalog module is considered complete for MVP when:
- An admin can create and manage categories
- An admin can create and manage products
- Products can be marked active/inactive and available/unavailable
- Customers can browse the catalog through the WhatsApp flow
- Orders created from the catalog are recorded in the system
- Catalog data is available through the API for downstream features
