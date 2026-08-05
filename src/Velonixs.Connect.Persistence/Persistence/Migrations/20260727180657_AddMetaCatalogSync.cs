using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMetaCatalogSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ImageUrl was present in some pre-catalog deployments before it
            // was captured in this migration. Keep the migration safe for
            // both schema histories rather than failing on a duplicate column.
            migrationBuilder.Sql("""
                IF COL_LENGTH('MenuItem', 'ImageUrl') IS NULL
                BEGIN
                    ALTER TABLE [MenuItem] ADD [ImageUrl] nvarchar(1000) NULL;
                END
                """);

            // Some early local catalog prototypes added these columns before
            // the durable outbox migration existed. Guard them so an
            // otherwise valid, partially upgraded database can continue.
            migrationBuilder.Sql("""
                IF COL_LENGTH('MenuItem', 'MetaProductId') IS NULL
                BEGIN
                    ALTER TABLE [MenuItem] ADD [MetaProductId] nvarchar(200) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('MenuItem', 'SyncStatus') IS NULL
                BEGIN
                    ALTER TABLE [MenuItem] ADD [SyncStatus] nvarchar(20) NOT NULL DEFAULT N'NotQueued';
                END
                """);

            // Adopt tables created by an earlier catalog prototype rather than
            // failing before EF can record this migration in its history.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[CatalogSyncLog]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [CatalogSyncLog]
                    (
                        [Id] uniqueidentifier NOT NULL,
                        [BusinessId] uniqueidentifier NOT NULL,
                        [ProductId] uniqueidentifier NOT NULL,
                        [EventType] nvarchar(50) NOT NULL,
                        [Status] nvarchar(20) NOT NULL,
                        [ResponseCode] int NOT NULL,
                        [ErrorMessage] nvarchar(1000) NULL,
                        [ResponseBody] nvarchar(max) NULL,
                        [CreatedAt] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_CatalogSyncLog] PRIMARY KEY ([Id])
                    );
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[CatalogSyncQueueItem]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [CatalogSyncQueueItem]
                    (
                        [Id] uniqueidentifier NOT NULL,
                        [BusinessId] uniqueidentifier NOT NULL,
                        [ProductId] uniqueidentifier NOT NULL,
                        [ProductRetailerId] nvarchar(200) NOT NULL,
                        [EventType] nvarchar(50) NOT NULL,
                        [PayloadJson] nvarchar(max) NULL,
                        [Status] nvarchar(20) NOT NULL,
                        [RetryCount] int NOT NULL,
                        [NextAttemptAt] datetimeoffset NULL,
                        [LastError] nvarchar(1000) NULL,
                        [LeaseId] uniqueidentifier NULL,
                        [LeaseExpiresAt] datetimeoffset NULL,
                        [CreatedAt] datetimeoffset NOT NULL,
                        [UpdatedAt] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_CatalogSyncQueueItem] PRIMARY KEY ([Id])
                    );
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[MetaCatalogSetting]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [MetaCatalogSetting]
                    (
                        [Id] uniqueidentifier NOT NULL,
                        [BusinessId] uniqueidentifier NOT NULL,
                        [WabaId] nvarchar(100) NULL,
                        [CatalogId] nvarchar(100) NULL,
                        [PhoneNumberId] nvarchar(100) NULL,
                        [AccessTokenEncrypted] nvarchar(max) NULL,
                        [WebhookVerifyTokenEncrypted] nvarchar(max) NULL,
                        [IsEnabled] bit NOT NULL,
                        [SyncMode] nvarchar(50) NOT NULL,
                        [CreatedAt] datetimeoffset NOT NULL,
                        [UpdatedAt] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_MetaCatalogSetting] PRIMARY KEY ([Id])
                    );
                END
                """);

            // A pre-outbox queue table can exist without the retry/lease
            // fields below. Add every nullable processing field before any
            // queue index refers to it.
            migrationBuilder.Sql("""
                IF COL_LENGTH('CatalogSyncQueueItem', 'PayloadJson') IS NULL
                BEGIN
                    ALTER TABLE [CatalogSyncQueueItem] ADD [PayloadJson] nvarchar(max) NULL;
                END

                IF COL_LENGTH('CatalogSyncQueueItem', 'NextAttemptAt') IS NULL
                BEGIN
                    ALTER TABLE [CatalogSyncQueueItem] ADD [NextAttemptAt] datetimeoffset NULL;
                END

                IF COL_LENGTH('CatalogSyncQueueItem', 'LastError') IS NULL
                BEGIN
                    ALTER TABLE [CatalogSyncQueueItem] ADD [LastError] nvarchar(1000) NULL;
                END

                IF COL_LENGTH('CatalogSyncQueueItem', 'LeaseId') IS NULL
                BEGIN
                    ALTER TABLE [CatalogSyncQueueItem] ADD [LeaseId] uniqueidentifier NULL;
                END

                IF COL_LENGTH('CatalogSyncQueueItem', 'LeaseExpiresAt') IS NULL
                BEGIN
                    ALTER TABLE [CatalogSyncQueueItem] ADD [LeaseExpiresAt] datetimeoffset NULL;
                END
                """);

            // Older installations could contain duplicate optional retailer IDs.
            // Keep the first deterministic value and replace later duplicates
            // before enforcing the business-level uniqueness invariant.
            migrationBuilder.Sql(@"
                ;WITH DuplicateRetailerIds AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [RestaurantId], [ProductRetailerId]
                               ORDER BY [Id]
                           ) AS [DuplicateNumber]
                    FROM [MenuItem]
                    WHERE [ProductRetailerId] IS NOT NULL
                )
                UPDATE [MenuItem]
                SET [ProductRetailerId] = CONCAT('vxc-', REPLACE(CONVERT(nvarchar(36), [MenuItem].[Id]), '-', ''))
                FROM [MenuItem]
                INNER JOIN DuplicateRetailerIds
                    ON DuplicateRetailerIds.[Id] = [MenuItem].[Id]
                WHERE DuplicateRetailerIds.[DuplicateNumber] > 1;");

            migrationBuilder.Sql("""
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_MenuItem_RestaurantId_ProductRetailerId'
                      AND [object_id] = OBJECT_ID(N'[MenuItem]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_MenuItem_RestaurantId_ProductRetailerId]
                    ON [MenuItem] ([RestaurantId], [ProductRetailerId])
                    WHERE [ProductRetailerId] IS NOT NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_CatalogSyncQueueItem_BusinessId_Status_NextAttemptAt'
                      AND [object_id] = OBJECT_ID(N'[CatalogSyncQueueItem]')
                )
                BEGIN
                    CREATE INDEX [IX_CatalogSyncQueueItem_BusinessId_Status_NextAttemptAt]
                    ON [CatalogSyncQueueItem] ([BusinessId], [Status], [NextAttemptAt]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_CatalogSyncQueueItem_Status_NextAttemptAt_LeaseExpiresAt'
                      AND [object_id] = OBJECT_ID(N'[CatalogSyncQueueItem]')
                )
                BEGIN
                    CREATE INDEX [IX_CatalogSyncQueueItem_Status_NextAttemptAt_LeaseExpiresAt]
                    ON [CatalogSyncQueueItem] ([Status], [NextAttemptAt], [LeaseExpiresAt]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_MetaCatalogSetting_BusinessId'
                      AND [object_id] = OBJECT_ID(N'[MetaCatalogSetting]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_MetaCatalogSetting_BusinessId]
                    ON [MetaCatalogSetting] ([BusinessId]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogSyncLog");

            migrationBuilder.DropTable(
                name: "CatalogSyncQueueItem");

            migrationBuilder.DropTable(
                name: "MetaCatalogSetting");

            migrationBuilder.DropIndex(
                name: "IX_MenuItem_RestaurantId_ProductRetailerId",
                table: "MenuItem");

            // Do not drop ImageUrl during rollback. It existed in some
            // pre-catalog schemas, and retaining an extra nullable column is
            // safer than deleting legacy product image data.

            migrationBuilder.DropColumn(
                name: "MetaProductId",
                table: "MenuItem");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "MenuItem");
        }
    }
}
