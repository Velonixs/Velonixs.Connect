using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterCatalogMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MasterMenuItemId",
                table: "MenuItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MasterCategoryId",
                table: "MenuCategory",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MasterMenuCategory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterMenuCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MasterMenuItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MasterCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterMenuItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MasterMenuItem_MasterMenuCategory_MasterCategoryId",
                        column: x => x.MasterCategoryId,
                        principalTable: "MasterMenuCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItem_MasterMenuItemId",
                table: "MenuItem",
                column: "MasterMenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategory_MasterCategoryId",
                table: "MenuCategory",
                column: "MasterCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MasterMenuCategory_Name",
                table: "MasterMenuCategory",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasterMenuItem_MasterCategoryId_Name",
                table: "MasterMenuItem",
                columns: new[] { "MasterCategoryId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuCategory_MasterMenuCategory_MasterCategoryId",
                table: "MenuCategory",
                column: "MasterCategoryId",
                principalTable: "MasterMenuCategory",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItem_MasterMenuItem_MasterMenuItemId",
                table: "MenuItem",
                column: "MasterMenuItemId",
                principalTable: "MasterMenuItem",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuCategory_MasterMenuCategory_MasterCategoryId",
                table: "MenuCategory");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItem_MasterMenuItem_MasterMenuItemId",
                table: "MenuItem");

            migrationBuilder.DropTable(
                name: "MasterMenuItem");

            migrationBuilder.DropTable(
                name: "MasterMenuCategory");

            migrationBuilder.DropIndex(
                name: "IX_MenuItem_MasterMenuItemId",
                table: "MenuItem");

            migrationBuilder.DropIndex(
                name: "IX_MenuCategory_MasterCategoryId",
                table: "MenuCategory");

            migrationBuilder.DropColumn(
                name: "MasterMenuItemId",
                table: "MenuItem");

            migrationBuilder.DropColumn(
                name: "MasterCategoryId",
                table: "MenuCategory");
        }
    }
}
