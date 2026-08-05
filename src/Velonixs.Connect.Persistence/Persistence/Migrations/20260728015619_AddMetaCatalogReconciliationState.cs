using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMetaCatalogReconciliationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('MetaCatalogSetting', 'ReconciliationRequired') IS NULL
                BEGIN
                    ALTER TABLE [MetaCatalogSetting]
                    ADD [ReconciliationRequired] bit NOT NULL DEFAULT 0;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReconciliationRequired",
                table: "MetaCatalogSetting");
        }
    }
}
