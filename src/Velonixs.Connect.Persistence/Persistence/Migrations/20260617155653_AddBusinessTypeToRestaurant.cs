using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Velonixs.Connect.Persistence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessTypeToRestaurant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                table: "Restaurant",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Restaurant");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "Restaurant");
        }
    }
}
