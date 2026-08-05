using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogSyncQueueCatalogBoundary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('CatalogSyncQueueItem', 'CatalogId') IS NULL
                BEGIN
                    ALTER TABLE [CatalogSyncQueueItem] ADD [CatalogId] nvarchar(100) NULL;
                END
                """);

            // Preserve deploy-time work for an unchanged connection while
            // making every future item explicitly bound to its source catalog.
            migrationBuilder.Sql(@"
                UPDATE [CatalogSyncQueueItem]
                SET [CatalogId] = [MetaCatalogSetting].[CatalogId]
                FROM [CatalogSyncQueueItem]
                INNER JOIN [MetaCatalogSetting]
                    ON [MetaCatalogSetting].[BusinessId] = [CatalogSyncQueueItem].[BusinessId]
                WHERE [CatalogSyncQueueItem].[CatalogId] IS NULL
                  AND [MetaCatalogSetting].[CatalogId] IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatalogId",
                table: "CatalogSyncQueueItem");
        }
    }
}
