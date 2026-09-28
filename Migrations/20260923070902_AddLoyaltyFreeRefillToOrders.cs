using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwivelWater.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyFreeRefillToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UsesLoyaltyFreeRefill",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsesLoyaltyFreeRefill",
                table: "Orders");
        }
    }
}
