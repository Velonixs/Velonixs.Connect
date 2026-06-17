# Database Design

## Current Tables

- Restaurants
- Customers
- Conversations
- Menu categories
- Menu items
- Orders
- Order items
- Message logs

## Target Terminology

The broader Velonixs Connect platform should evolve these concepts:

```text
Restaurant       -> Business
MenuCategory     -> Category
MenuItem         -> Product
Restaurant order -> Order
```

## Target Core Tables

- Businesses
- Customers
- Categories
- Products
- Orders
- OrderItems
- MessageLogs
- AuditLogs

Database renaming is intentionally deferred from the first structural slices to avoid breaking the existing local data and EF migrations.

The current `Restaurant` table now carries a `BusinessType` column so the platform can distinguish restaurants from planned business categories without renaming existing tables yet.

## Persistence Project

EF Core DbContext, migrations, database initialization, and design-time DbContext factory now live in:

```text
src/Velonixs.Connect.Persistence
```
