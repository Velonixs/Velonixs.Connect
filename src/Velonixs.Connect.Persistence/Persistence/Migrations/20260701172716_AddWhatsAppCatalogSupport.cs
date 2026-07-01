using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppCatalogSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Restaurant', 'WhatsAppCatalogId') IS NULL
                BEGIN
                    ALTER TABLE [Restaurant] ADD [WhatsAppCatalogId] nvarchar(100) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('MenuItem', 'ProductRetailerId') IS NULL
                BEGIN
                    ALTER TABLE [MenuItem] ADD [ProductRetailerId] nvarchar(200) NULL;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Restaurant', 'WhatsAppCatalogId') IS NOT NULL
                BEGIN
                    ALTER TABLE [Restaurant] DROP COLUMN [WhatsAppCatalogId];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('MenuItem', 'ProductRetailerId') IS NOT NULL
                BEGIN
                    ALTER TABLE [MenuItem] DROP COLUMN [ProductRetailerId];
                END
                """);
        }
    }
}
