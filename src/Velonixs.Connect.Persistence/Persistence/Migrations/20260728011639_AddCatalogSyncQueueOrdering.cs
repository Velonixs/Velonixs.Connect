using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogSyncQueueOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('CatalogSyncQueueItem', 'PredecessorQueueItemId') IS NULL
                BEGIN
                    ALTER TABLE [CatalogSyncQueueItem] ADD [PredecessorQueueItemId] uniqueidentifier NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_CatalogSyncQueueItem_PredecessorQueueItemId'
                      AND [object_id] = OBJECT_ID(N'[CatalogSyncQueueItem]')
                )
                BEGIN
                    CREATE INDEX [IX_CatalogSyncQueueItem_PredecessorQueueItemId]
                    ON [CatalogSyncQueueItem] ([PredecessorQueueItemId]);
                END
                """);

            // Older releases did not enforce one outstanding event per product.
            // Keep the most recently updated record and cancel superseded work
            // before introducing the filtered uniqueness guarantees below.
            migrationBuilder.Sql(@"
                ;WITH RankedQueueItems AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [BusinessId], [ProductId]
                               ORDER BY [UpdatedAt] DESC, [Id] DESC
                           ) AS [QueueRank]
                    FROM [CatalogSyncQueueItem]
                    WHERE [Status] IN ('Pending', 'Failed', 'Processing', 'Paused', 'Simulated')
                )
                UPDATE [CatalogSyncQueueItem]
                SET [Status] = 'Cancelled',
                    [LastError] = 'Superseded during the catalog queue ordering upgrade.',
                    [NextAttemptAt] = NULL,
                    [LeaseId] = NULL,
                    [LeaseExpiresAt] = NULL,
                    [UpdatedAt] = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
                FROM [CatalogSyncQueueItem]
                INNER JOIN RankedQueueItems
                    ON RankedQueueItems.[Id] = [CatalogSyncQueueItem].[Id]
                WHERE RankedQueueItems.[QueueRank] > 1;");

            migrationBuilder.Sql("""
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'UX_CatalogSyncQueue_ActiveProduct'
                      AND [object_id] = OBJECT_ID(N'[CatalogSyncQueueItem]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [UX_CatalogSyncQueue_ActiveProduct]
                    ON [CatalogSyncQueueItem] ([BusinessId], [ProductId])
                    WHERE [Status] IN ('Pending', 'Failed', 'Processing', 'Paused', 'Simulated');
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'UX_CatalogSyncQueue_WaitingProduct'
                      AND [object_id] = OBJECT_ID(N'[CatalogSyncQueueItem]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [UX_CatalogSyncQueue_WaitingProduct]
                    ON [CatalogSyncQueueItem] ([BusinessId], [ProductId])
                    WHERE [Status] = 'Waiting';
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogSyncQueueItem_PredecessorQueueItemId",
                table: "CatalogSyncQueueItem");

            migrationBuilder.DropIndex(
                name: "UX_CatalogSyncQueue_ActiveProduct",
                table: "CatalogSyncQueueItem");

            migrationBuilder.DropIndex(
                name: "UX_CatalogSyncQueue_WaitingProduct",
                table: "CatalogSyncQueueItem");

            migrationBuilder.DropColumn(
                name: "PredecessorQueueItemId",
                table: "CatalogSyncQueueItem");
        }
    }
}
