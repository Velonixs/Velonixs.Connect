# Velonixs Connect Architecture

## Target Shape

```text
Customer WhatsApp
  -> WhatsApp Cloud API
  -> Velonixs Connect API
  -> SQL Server data store
  -> Admin / staff portals
```

## Projects

```text
src/Velonixs.Connect.Api             REST API, WhatsApp webhook, health endpoint
src/Velonixs.Connect.Contracts       Shared API contracts and cross-client response shapes
src/Velonixs.Connect.Admin           Platform admin UI
src/Velonixs.Connect.Portal          Staff operations UI
src/Velonixs.Connect.Application     Service contracts and DTOs
src/Velonixs.Connect.Domain          Entities and domain constants
src/Velonixs.Connect.Infrastructure  Messaging, external integrations, application services
src/Velonixs.Connect.Persistence     EF Core, SQL Server, migrations, database initialization
src/Velonixs.Connect.Shared          Shared security roles and claim names
```

## Technology

- ASP.NET Core Web API
- Razor/Bootstrap UI currently; Blazor Web App is the target UI direction
- SQL Server with Entity Framework Core
- WhatsApp Cloud API
- Azure App Service and Azure SQL for production hosting

## Pending Architecture Work

- Move Admin and Portal workflows to API-backed Blazor applications
- Extract shared Portal Razor components for web and MAUI reuse
- Add typed API clients, refresh-token persistence, SignalR order notifications, and MAUI Android packaging
- Continue generalizing restaurant-specific domain names into business/catalog terminology

See `docs/api-first-migration-plan.md` for the API-first migration assessment, dependency diagram, risks, and phased implementation plan.

## Business Type Support

The platform now records a business type on the current restaurant/business record. Version 1.0 defaults to `Restaurant`, with planned support for grocery stores, fish markets, bakeries, sweet shops, pharmacies, and retail stores.

## API Compatibility

The API exposes first-class business/catalog endpoints such as `/api/businesses` and `/api/businesses/{id}/catalog` through dedicated application contracts. Phase 1 adds versioned `/api/v1/...` routes while keeping the original `/api/...` endpoints available for existing local tests, Azure configuration, and WhatsApp webhook compatibility.

Order operations now include `PATCH /api/v1/orders/{id}/status`, `POST /api/v1/orders/{id}/confirm`, and `POST /api/v1/orders/{id}/reject`. Order status-transition validation lives in `Velonixs.Connect.Domain.Services.OrderStatusTransitionPolicy`.

## Authentication

ASP.NET Identity is the user store for API and browser authentication. API authorization is enabled by default and uses bearer tokens from `/api/auth/login`. Admin and Portal always require cookie authentication.

`PlatformAdmin` users access the Admin Portal without a business assignment. Business Portal users have a required `BusinessId` claim and one of `BusinessOwner`, `BusinessManager`, `Cashier`, or `Staff`. Portal controllers and REST API tenant guards validate that claim before reading or changing business resources. The database stores Identity data in `Auth*` tables.

## Data Protection

Sensitive non-searchable fields are encrypted with AES-256-GCM before persistence. Keys are supplied outside source control and must be shared by API, Admin, and Portal instances. See `docs/Data-Security.md` for encrypted fields, remaining searchable plaintext fields, and production key-management requirements.
