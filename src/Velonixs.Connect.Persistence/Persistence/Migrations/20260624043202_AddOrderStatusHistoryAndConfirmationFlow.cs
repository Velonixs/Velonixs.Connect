using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderStatusHistoryAndConfirmationFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimatedMinutes",
                table: "Order",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestaurantComment",
                table: "Order",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_Order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_OrderId_NewStatus_UpdatedAtUtc",
                table: "OrderStatusHistory",
                columns: new[] { "OrderId", "NewStatus", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderStatusHistory");

            migrationBuilder.DropColumn(
                name: "EstimatedMinutes",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "RestaurantComment",
                table: "Order");
        }
    }
}
