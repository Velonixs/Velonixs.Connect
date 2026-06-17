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
src/Velonixs.Connect.Admin           Platform admin UI
src/Velonixs.Connect.Portal          Staff operations UI
src/Velonixs.Connect.Application     Service contracts and DTOs
src/Velonixs.Connect.Domain          Entities and domain constants
src/Velonixs.Connect.Infrastructure  Messaging, external integrations, application services
src/Velonixs.Connect.Persistence     EF Core, SQL Server, migrations, database initialization
```

## Technology

- ASP.NET Core Web API
- Razor/Bootstrap UI currently; Blazor Web App is the target UI direction
- SQL Server with Entity Framework Core
- WhatsApp Cloud API
- Azure App Service and Azure SQL for production hosting

## Pending Architecture Work

- Add shared UI/contracts project if needed
- Continue generalizing restaurant-specific domain names into business/catalog terminology

## Business Type Support

The platform now records a business type on the current restaurant/business record. Version 1.0 defaults to `Restaurant`, with planned support for grocery stores, fish markets, bakeries, sweet shops, pharmacies, and retail stores.

## API Compatibility

The API exposes first-class business/catalog endpoints such as `/api/businesses` and `/api/businesses/{id}/catalog` through dedicated application contracts. The original restaurant/menu endpoints remain available for existing local tests and clients while the domain model continues its staged migration.

## Authentication

ASP.NET Identity is the user store for API and browser authentication. API authorization can be enforced with `Auth:RequireAuthentication=true` and bearer tokens from `/api/auth/login`. Admin and Portal use cookie sessions and can be protected independently with `Admin:RequireAuthentication=true` and `Portal:RequireAuthentication=true`. Local development keeps these disabled by default. The database stores Identity data in `Auth*` tables.
