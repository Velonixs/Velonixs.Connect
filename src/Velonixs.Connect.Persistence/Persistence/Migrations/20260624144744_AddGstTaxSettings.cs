using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGstTaxSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CgstPercent",
                table: "Restaurant",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SgstPercent",
                table: "Restaurant",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CgstAmount",
                table: "Order",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CgstPercent",
                table: "Order",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SgstAmount",
                table: "Order",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SgstPercent",
                table: "Order",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubTotalAmount",
                table: "Order",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PlatformTaxSetting",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CgstPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SgstPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformTaxSetting", x => x.Id);
                });

            migrationBuilder.Sql("""
                UPDATE [Order]
                SET [SubTotalAmount] = [TotalAmount]
                WHERE [SubTotalAmount] = 0
                """);

            migrationBuilder.Sql("""
                INSERT INTO [PlatformTaxSetting] ([Id], [CgstPercent], [SgstPercent], [UpdatedAtUtc])
                SELECT 'GST', 0, 0, SYSDATETIMEOFFSET()
                WHERE NOT EXISTS (SELECT 1 FROM [PlatformTaxSetting] WHERE [Id] = 'GST')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformTaxSetting");

            migrationBuilder.DropColumn(
                name: "CgstPercent",
                table: "Restaurant");

            migrationBuilder.DropColumn(
                name: "SgstPercent",
                table: "Restaurant");

            migrationBuilder.DropColumn(
                name: "CgstAmount",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CgstPercent",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "SgstAmount",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "SgstPercent",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "SubTotalAmount",
                table: "Order");
        }
    }
}
