using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Velonixs.Connect.Persistence.Persistence;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations;

/// <summary>
/// Repairs catalog-sync tables created by the early durable-outbox migration.
/// That migration created <c>ProductRetailerId</c> for new tables but did not
/// add it when adopting an already-existing prototype queue table.
/// </summary>
[DbContext(typeof(RestaurantConnectDbContext))]
[Migration("20260908114000_RepairCatalogSyncQueueProductRetailerId")]
public sealed class RepairCatalogSyncQueueProductRetailerId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The current model snapshot already includes this required column.
        // Keep the repair idempotent so both clean installs and upgraded
        // prototype databases can apply the migration safely.
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'CatalogSyncQueueItem', N'ProductRetailerId') IS NULL
            BEGIN
                ALTER TABLE [CatalogSyncQueueItem]
                    ADD [ProductRetailerId] nvarchar(200) NOT NULL DEFAULT N'';
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This is a corrective migration for a column that belongs to the
        // preceding model. Retaining the column preserves that model if an
        // application is rolled back.
    }
}
