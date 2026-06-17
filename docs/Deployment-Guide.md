# Deployment Guide

## Requirements

- SQL Server or Azure SQL
- Azure App Service or equivalent ASP.NET hosting
- Meta Developer app with WhatsApp Cloud API configured
- Production configuration for database, WhatsApp, and SMTP

## Build

```text
dotnet restore Velonixs.Connect.sln
dotnet build Velonixs.Connect.sln
```

## Run Locally

See `docs/local-setup.md`.

## Production Settings

Set secrets through environment variables, Azure App Service configuration, or a secure secret store. Do not commit production tokens or passwords.

Required authentication settings for protected deployments:

```text
Auth__RequireAuthentication=true
Admin__RequireAuthentication=true
Portal__RequireAuthentication=true
Auth__Issuer=
Auth__Audience=
Auth__SigningKey=
Auth__DefaultAdminEmail=
Auth__DefaultAdminPassword=
```

Use a strong signing key and first-user password stored outside source control. API clients use `POST /api/auth/login` for bearer tokens. Admin and Portal users sign in through `/admin/login` and `/portal/login`.
