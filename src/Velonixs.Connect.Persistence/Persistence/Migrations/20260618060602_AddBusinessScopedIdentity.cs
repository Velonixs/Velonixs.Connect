using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessScopedIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "AuthUser",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AuthUser",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthUser_BusinessId",
                table: "AuthUser",
                column: "BusinessId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuthUser_Restaurant_BusinessId",
                table: "AuthUser",
                column: "BusinessId",
                principalTable: "Restaurant",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuthUser_Restaurant_BusinessId",
                table: "AuthUser");

            migrationBuilder.DropIndex(
                name: "IX_AuthUser_BusinessId",
                table: "AuthUser");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "AuthUser");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AuthUser");
        }
    }
}
