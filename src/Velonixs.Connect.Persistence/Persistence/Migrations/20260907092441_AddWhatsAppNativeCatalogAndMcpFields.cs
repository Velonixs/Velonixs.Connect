using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppNativeCatalogAndMcpFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerInstructions",
                table: "OrderItem",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductRetailerId",
                table: "OrderItem",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Order",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<string>(
                name: "ExternalWhatsAppMessageId",
                table: "Order",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialReference",
                table: "MetaCatalogSetting",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCartEnabled",
                table: "MetaCatalogSetting",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSuccessfulSyncAt",
                table: "MetaCatalogSetting",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaBusinessId",
                table: "MetaCatalogSetting",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "MenuItem",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "MenuItem",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPrice",
                table: "MenuItem",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVegetarian",
                table: "MenuItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastSyncError",
                table: "MenuItem",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAt",
                table: "MenuItem",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaCatalogId",
                table: "MenuItem",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreparationTimeMinutes",
                table: "MenuItem",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "MenuItem",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "MenuItem",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.CreateIndex(
                name: "IX_Order_ExternalWhatsAppMessageId",
                table: "Order",
                column: "ExternalWhatsAppMessageId",
                unique: true,
                filter: "[ExternalWhatsAppMessageId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Order_ExternalWhatsAppMessageId",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CustomerInstructions",
                table: "OrderItem");

            migrationBuilder.DropColumn(
                name: "ProductRetailerId",
                table: "OrderItem");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "ExternalWhatsAppMessageId",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CredentialReference",
                table: "MetaCatalogSetting");

            migrationBuilder.DropColumn(
                name: "IsCartEnabled",
                table: "MetaCatalogSetting");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulSyncAt",
                table: "MetaCatalogSetting");

            migrationBuilder.DropColumn(
                name: "MetaBusinessId",
                table: "MetaCatalogSetting");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MenuItem");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "MenuItem");

            migrationBuilder.DropColumn(
                name: "DiscountPrice",
                table: "MenuItem");

            migrationBuilder.DropColumn(
                name: "IsVegetarian",
                table: "MenuItem");

            migrationBuilder.DropColumn(
                name: "LastSyncError",
                table: "MenuItem");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "MenuItem");

            migrationBuilder.DropColumn(
                name: "MetaCatalogId",
                table: "MenuItem");

            migrationBuilder.DropColumn(
                name: "PreparationTimeMinutes",
                table: "MenuItem");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "MenuItem");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MenuItem");
        }
    }
}
