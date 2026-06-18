# Velonixs Connect - Architecture and UML Diagrams

**Version:** 1.1  
**Product:** Velonixs Connect  
**Owner:** Velonixs Technologies  
**Purpose:** Current implementation reference plus explicitly identified target architecture.

---

## 1. Document Scope

This document separates implemented architecture from planned architecture.

- **Current:** present in the repository and database.
- **Compatibility:** retained restaurant/menu naming that supports existing data and clients.
- **Planned:** proposed future capability; not currently implemented.

The current browser applications are server-rendered ASP.NET Core Razor applications. They use cookie authentication and call application services and EF Core directly. They do not currently call the REST API for their dashboards.

---

## 2. Current High-Level Architecture

```mermaid
flowchart TB
    Customer[Customer on WhatsApp]
    WhatsApp[WhatsApp Cloud API]
    API[Velonixs.Connect.Api]
    Admin[Velonixs.Connect.Admin]
    Portal[Velonixs.Connect.Portal]
    Services[Application and Infrastructure Services]
    DB[(SQL Server)]
    Secrets[Secret Manager / Environment / Key Vault]

    Customer --> WhatsApp
    WhatsApp --> API
    API --> WhatsApp

    API --> Services
    Admin --> Services
    Portal --> Services

    Services --> DB
    Secrets --> API
    Secrets --> Admin
    Secrets --> Portal
```

The API, Admin, and Portal are separate executable applications, but all three currently use the same persistence and service layers.

---

## 3. Current Application Architecture

```mermaid
flowchart TB
    subgraph Hosts[Executable Hosts]
        Api[Velonixs.Connect.Api]
        Admin[Velonixs.Connect.Admin]
        Portal[Velonixs.Connect.Portal]
    end

    subgraph Core[Core Projects]
        Application[Velonixs.Connect.Application]
        Domain[Velonixs.Connect.Domain]
        Shared[Velonixs.Connect.Shared]
    end

    subgraph Adapters[Adapters]
        Infrastructure[Velonixs.Connect.Infrastructure]
        Persistence[Velonixs.Connect.Persistence]
    end

    External[WhatsApp / SMTP]
    SQL[(SQL Server)]

    Api --> Application
    Api --> Infrastructure
    Api --> Persistence

    Admin --> Application
    Admin --> Infrastructure
    Admin --> Persistence
    Admin --> Shared

    Portal --> Application
    Portal --> Infrastructure
    Portal --> Persistence
    Portal --> Shared

    Application --> Domain
    Infrastructure --> Application
    Infrastructure --> Domain
    Infrastructure --> Persistence
    Infrastructure --> Shared
    Persistence --> Domain
    Persistence --> Shared

    Infrastructure --> External
    Persistence --> SQL
```

`Velonixs.Connect.Application` contains service contracts and DTOs. Most current service implementations live in `Velonixs.Connect.Infrastructure`.

---

## 4. Current Deployment Architecture

```mermaid
flowchart TB
    AdminUser[Platform Admin]
    StaffUser[Business Owner / Manager / Cashier / Staff]
    Customer[WhatsApp Customer]

    AdminApp[Admin Web App]
    PortalApp[Business Portal Web App]
    ApiApp[REST API and WhatsApp Webhook]

    WhatsApp[WhatsApp Cloud API]
    SQL[(SQL Server / Azure SQL)]
    KeyStore[Secret Store / Azure Key Vault]

    AdminUser --> AdminApp
    StaffUser --> PortalApp
    Customer --> WhatsApp
    WhatsApp --> ApiApp
    ApiApp --> WhatsApp

    AdminApp --> SQL
    PortalApp --> SQL
    ApiApp --> SQL

    KeyStore --> AdminApp
    KeyStore --> PortalApp
    KeyStore --> ApiApp
```

Azure Blob Storage, Application Insights integration, and API-only browser clients are planned, not current dependencies.

---

## 5. Browser Authentication Flow

Admin and Portal use ASP.NET Core cookie authentication.

```mermaid
sequenceDiagram
    actor User
    participant UI as Admin or Business Portal
    participant Identity as ASP.NET Identity
    participant DB as SQL Server

    User->>UI: Submit email and password
    UI->>Identity: Validate password and active status
    Identity->>DB: Read AuthUser and AuthUserRole
    DB-->>Identity: User, roles, BusinessId
    Identity-->>UI: Validation result
    UI->>UI: Validate portal-specific role
    UI-->>User: Issue protected authentication cookie
    User->>UI: Request dashboard
    UI->>UI: Revalidate active account and claims
    UI->>DB: Query authorized data
    DB-->>UI: Business-scoped result
```

- Admin Portal requires `PlatformAdmin`.
- Business Portal requires an active user with a `BusinessId`.
- Disabled accounts are rejected even when an older cookie still exists.

---

## 6. API Authentication and Tenant Authorization

API clients use JWT bearer tokens from `POST /api/auth/login`.

```mermaid
sequenceDiagram
    actor Client
    participant API as Velonixs Connect API
    participant Identity as ASP.NET Identity
    participant Guard as API Business Access Guard
    participant DB as SQL Server

    Client->>API: POST /api/auth/login
    API->>Identity: Validate credentials
    Identity->>DB: Read user, roles, BusinessId
    DB-->>Identity: Identity data
    Identity-->>API: Valid user
    API-->>Client: JWT with role and BusinessId claims

    Client->>API: Authorized business request
    API->>Guard: Validate role and requested resource
    Guard->>DB: Resolve order/product business when required
    DB-->>Guard: Owning BusinessId
    Guard-->>API: Permit or deny
    API->>DB: Execute permitted operation
    DB-->>API: Result
    API-->>Client: Response
```

### Authorization Rules

- `PlatformAdmin` can access every business and create or update businesses.
- Owners and managers can read full configuration only for their assigned business.
- Cashier and staff accounts are limited to their business catalog and orders.
- Owners and managers can modify catalog availability and products.
- Owner, manager, cashier, and staff roles can access orders for their business.
- Modern business/catalog routes and compatibility restaurant/menu routes use the same tenant guard.
- WhatsApp webhook verification and delivery remain anonymous because Meta calls them.
- `/api/webhooks/whatsapp/local-test` is available only in Development.

---

## 7. Multi-Tenant Request Rule

```mermaid
flowchart LR
    Token[Cookie or JWT Claims]
    Role[Role Claim]
    BusinessClaim[BusinessId Claim]
    Request[Requested Resource]
    Resolver[Resolve Owning Business]
    Decision{Platform Admin or Same Business?}
    Allow[Allow]
    Deny[Forbid]

    Token --> Role
    Token --> BusinessClaim
    Request --> Resolver
    Role --> Decision
    BusinessClaim --> Decision
    Resolver --> Decision
    Decision -- Yes --> Allow
    Decision -- No --> Deny
```

Business Portal queries also filter by the authenticated `BusinessId` before data is presented.

---

## 8. User Provisioning Flow

```mermaid
flowchart TD
    PlatformAdmin[Platform Admin]
    CreateBusiness[Create Business]
    CreateOwner[Create Business Owner]
    OwnerLogin[Owner Logs Into Business Portal]
    CreateStaff[Owner Creates Manager / Cashier / Staff]
    StaffLogin[Staff User Logs In]
    ScopedDashboard[Business-Scoped Dashboard]

    PlatformAdmin --> CreateBusiness
    CreateBusiness --> CreateOwner
    CreateOwner --> OwnerLogin
    OwnerLogin --> CreateStaff
    CreateStaff --> StaffLogin
    StaffLogin --> ScopedDashboard
```

Only `BusinessOwner` currently manages staff users. Manager-based staff administration is not implemented.

---

## 9. WhatsApp Message and Order Flow

```mermaid
sequenceDiagram
    actor Customer
    participant WA as WhatsApp Cloud API
    participant API as Webhook Controller
    participant Conversation as Conversation Service
    participant DB as SQL Server
    participant Notify as Staff Notification

    Customer->>WA: Send message or interactive selection
    WA->>API: POST webhook event
    API->>API: Validate Meta signature
    API->>Conversation: Process incoming message
    Conversation->>DB: Find business by phone-number ID
    Conversation->>DB: Find or create customer and conversation
    Conversation->>DB: Save encrypted message body
    Conversation->>Conversation: Advance conversation state
    Conversation->>DB: Create order after confirmation
    Conversation->>Notify: Notify staff
    Conversation->>WA: Send response
    WA-->>Customer: WhatsApp response
```

The business is currently found through the compatibility `Restaurant.WhatsAppPhoneNumberId` field.

---

## 10. Current Order Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> PendingConfirmation
    PendingConfirmation --> Confirmed
    Confirmed --> Notified
    Confirmed --> Failed
    Notified --> Handled

    PendingConfirmation --> Cancelled
    Confirmed --> Cancelled
    Notified --> Cancelled

    Handled --> [*]
    Cancelled --> [*]
    Failed --> [*]
```

Preparing, Ready, Delivered, and customer-facing status notifications are planned workflow enhancements.

---

## 11. Current Role-Based Access Matrix

| Feature | PlatformAdmin | BusinessOwner | BusinessManager | Cashier | Staff |
|---|---:|---:|---:|---:|---:|
| Admin Portal | Yes | No | No | No | No |
| View all businesses | Yes | No | No | No | No |
| Create business and owner | Yes | No | No | No | No |
| Business dashboard | No | Yes | Yes | Yes | Yes |
| View business orders | Yes through API/Admin | Yes | Yes | Yes | Yes |
| Update order status | Yes through API/Admin | Yes | Yes | Yes | Yes |
| View catalog and customers | Yes | Yes | Yes | Yes | Yes |
| Change catalog availability | Yes through API/Admin | Yes | Yes | No | No |
| Create manager/cashier/staff | No | Yes | No | No | No |
| Enable or disable staff | No | Yes | No | No | No |

Billing, support, kitchen, viewer, and subscription-management roles are planned.

---

## 12. Current Physical Domain Model

The code and database retain restaurant/menu names for compatibility.

```mermaid
classDiagram
    class Restaurant {
        +Guid Id
        +string Name
        +string BusinessType
        +string WhatsAppPhoneNumberId
        +bool IsActive
    }

    class ApplicationUser {
        +Guid Id
        +Guid? BusinessId
        +string DisplayName
        +string Email
        +bool IsActive
    }

    class Customer {
        +Guid Id
        +Guid RestaurantId
        +string PhoneNumber
        +string Name
        +string LastAddress
    }

    class MenuCategory {
        +Guid Id
        +Guid RestaurantId
        +string Name
        +bool IsActive
    }

    class MenuItem {
        +Guid Id
        +Guid RestaurantId
        +Guid CategoryId
        +int ItemCode
        +string Name
        +decimal Price
        +bool IsAvailable
    }

    class Order {
        +Guid Id
        +Guid RestaurantId
        +Guid CustomerId
        +string OrderNumber
        +string OrderStatus
        +decimal TotalAmount
    }

    class OrderItem {
        +Guid Id
        +Guid OrderId
        +Guid MenuItemId
        +int Quantity
        +decimal UnitPrice
        +decimal LineTotal
    }

    class Conversation {
        +Guid Id
        +Guid RestaurantId
        +Guid CustomerId
        +string CurrentState
        +bool IsActive
    }

    class MessageLog {
        +Guid Id
        +Guid RestaurantId
        +Guid? CustomerId
        +Guid? ConversationId
        +string Direction
        +string MessageText
        +string Status
    }

    Restaurant "1" --> "many" ApplicationUser
    Restaurant "1" --> "many" Customer
    Restaurant "1" --> "many" MenuCategory
    Restaurant "1" --> "many" MenuItem
    Restaurant "1" --> "many" Order
    Restaurant "1" --> "many" Conversation
    Restaurant "1" --> "many" MessageLog
    MenuCategory "1" --> "many" MenuItem
    Customer "1" --> "many" Order
    Customer "1" --> "many" Conversation
    Order "1" --> "many" OrderItem
    MenuItem "1" --> "many" OrderItem
    Conversation "1" --> "many" MessageLog
```

Target API contracts expose Business, Catalog, Category, and Product terminology without renaming the physical tables yet.

---

## 13. Current Database Overview

```mermaid
erDiagram
    RESTAURANT ||--o{ AUTH_USER : assigns
    RESTAURANT ||--o{ CUSTOMER : has
    RESTAURANT ||--o{ MENU_CATEGORY : has
    RESTAURANT ||--o{ MENU_ITEM : has
    RESTAURANT ||--o{ ORDER : has
    RESTAURANT ||--o{ CONVERSATION : has
    RESTAURANT ||--o{ MESSAGE_LOG : has

    AUTH_USER ||--o{ AUTH_USER_ROLE : receives
    AUTH_ROLE ||--o{ AUTH_USER_ROLE : contains

    MENU_CATEGORY ||--o{ MENU_ITEM : contains
    CUSTOMER ||--o{ ORDER : places
    CUSTOMER ||--o{ CONVERSATION : starts
    ORDER ||--o{ ORDER_ITEM : contains
    MENU_ITEM ||--o{ ORDER_ITEM : references
    CONVERSATION ||--o{ MESSAGE_LOG : contains

    RESTAURANT {
        uniqueidentifier Id PK
        string Name
        string BusinessType
        string WhatsAppPhoneNumberId
        bool IsActive
    }

    AUTH_USER {
        uniqueidentifier Id PK
        uniqueidentifier BusinessId FK
        string DisplayName
        string Email
        string PasswordHash
        bool IsActive
    }

    AUTH_ROLE {
        uniqueidentifier Id PK
        string Name
    }

    CUSTOMER {
        uniqueidentifier Id PK
        uniqueidentifier RestaurantId FK
        string PhoneNumber
        string Name
        string LastAddress
    }

    MENU_CATEGORY {
        uniqueidentifier Id PK
        uniqueidentifier RestaurantId FK
        string Name
    }

    MENU_ITEM {
        uniqueidentifier Id PK
        uniqueidentifier RestaurantId FK
        uniqueidentifier CategoryId FK
        int ItemCode
        string Name
        decimal Price
    }

    ORDER {
        uniqueidentifier Id PK
        uniqueidentifier RestaurantId FK
        uniqueidentifier CustomerId FK
        string OrderNumber
        string OrderStatus
        decimal TotalAmount
    }

    ORDER_ITEM {
        uniqueidentifier Id PK
        uniqueidentifier OrderId FK
        uniqueidentifier MenuItemId FK
        int Quantity
        decimal UnitPrice
        decimal LineTotal
    }
```

Identity also uses `AuthUserClaim`, `AuthUserLogin`, `AuthRoleClaim`, and `AuthUserToken`.

---

## 14. Data Protection Architecture

```mermaid
flowchart LR
    SecretStore[Secret Manager / Environment / Key Vault]
    SharedKey[Shared 256-bit Encryption Key]
    App[API / Admin / Portal]
    Converter[EF Core Value Converters]
    AES[AES-256-GCM]
    SQL[(SQL Server Ciphertext)]
    Marker[DataProtectionState]

    SecretStore --> SharedKey
    SharedKey --> App
    App --> Converter
    Converter --> AES
    AES --> SQL
    Marker --> App
```

Sensitive non-searchable fields are encrypted with a random nonce and authenticated tag. Existing plaintext rows are rewritten once and recorded in `DataProtectionState`.

The following remain searchable plaintext and require a future HMAC lookup migration:

- `Customer.PhoneNumber`
- `Restaurant.WhatsAppPhoneNumberId`
- Identity email and normalized email

See `docs/Data-Security.md`.

---

## 15. Current Package Diagram

```mermaid
flowchart LR
    Admin[Admin]
    Portal[Portal]
    Api[API]
    Application[Application]
    Domain[Domain]
    Infrastructure[Infrastructure]
    Persistence[Persistence]
    Shared[Shared]

    Admin --> Application
    Admin --> Infrastructure
    Admin --> Persistence
    Admin --> Shared

    Portal --> Application
    Portal --> Infrastructure
    Portal --> Persistence
    Portal --> Shared

    Api --> Application
    Api --> Infrastructure
    Api --> Persistence

    Application --> Domain
    Infrastructure --> Application
    Infrastructure --> Domain
    Infrastructure --> Persistence
    Infrastructure --> Shared
    Persistence --> Domain
    Persistence --> Shared
```

---

## 16. Current Business Owner and Staff Creation

```mermaid
sequenceDiagram
    actor Admin
    participant AdminUI as Admin Portal
    participant Identity as ASP.NET Identity
    participant DB as SQL Server

    Admin->>AdminUI: Create business and owner
    AdminUI->>Identity: Create active user
    Identity->>DB: Save user with BusinessId
    AdminUI->>Identity: Add BusinessOwner role
    Identity->>DB: Save role assignment
    AdminUI-->>Admin: Business and owner created
```

```mermaid
sequenceDiagram
    actor Owner
    participant Portal as Business Portal
    participant Identity as ASP.NET Identity
    participant DB as SQL Server

    Owner->>Portal: Open Staff Users
    Owner->>Portal: Enter staff credentials and role
    Portal->>Portal: Confirm owner role and BusinessId
    Portal->>Identity: Create active user
    Identity->>DB: Save user under owner's BusinessId
    Portal->>Identity: Assign manager, cashier, or staff role
    Identity->>DB: Save role assignment
    Portal-->>Owner: Staff account created
```

Email invitations and password-reset links are planned. Current provisioning uses a temporary password entered by the administrator or owner.

---

## 17. Planned Target Architecture

The following components are planned and must not be interpreted as implemented:

```mermaid
flowchart TB
    Browser[Blazor or API-Only Browser Clients]
    Gateway[API Gateway]
    API[Velonixs Connect API]
    Blob[Azure Blob Storage]
    Insights[Application Insights]
    Reports[Reporting Service]
    Billing[Billing and Subscription Service]
    Audit[Audit Log Service]

    Browser -. planned .-> Gateway
    Gateway -. planned .-> API
    API -. planned .-> Blob
    API -. planned .-> Insights
    API -. planned .-> Reports
    API -. planned .-> Billing
    API -. planned .-> Audit
```

Planned roles may include support, billing, kitchen, and read-only reporting users, but they are not present in `AppRoles` today.

---

## 18. Architecture Backlog

1. Add HMAC lookup columns and encrypt customer phone numbers.
2. Move Admin and Portal toward API-only data access if independent service boundaries are required.
3. Add audit logs for authentication, user management, order changes, and catalog changes.
4. Add customer WhatsApp notifications for order-status transitions.
5. Expand order states to Preparing, Ready, and Delivered.
6. Add password-reset and invitation workflows instead of distributing temporary passwords.
7. Add reporting, billing, subscription, support, Blob Storage, and Application Insights components.
8. Complete the physical rename from Restaurant/MenuItem to Business/Product when compatibility constraints permit.

---

## 19. Related Documentation

```text
docs/
|-- PRD.md
|-- Architecture.md
|-- Architecture-Diagrams.md
|-- Data-Security.md
|-- Database-Design.md
|-- WhatsApp-Integration.md
|-- Deployment-Guide.md
|-- local-setup.md
`-- Roadmap.md
```
