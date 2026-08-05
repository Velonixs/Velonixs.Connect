using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogSyncQueueRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('CatalogSyncQueueItem', 'RowVersion') IS NULL
                BEGIN
                    ALTER TABLE [CatalogSyncQueueItem] ADD [RowVersion] rowversion;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CatalogSyncQueueItem");
        }
    }
}
