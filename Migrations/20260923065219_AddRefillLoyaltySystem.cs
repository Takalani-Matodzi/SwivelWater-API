using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwivelWater.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRefillLoyaltySystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefillLoyaltyCards",
                columns: table => new
                {
                    RefillLoyaltyCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TickCount = table.Column<int>(type: "integer", nullable: false),
                    FreeRefillsAvailable = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefillLoyaltyCards", x => x.RefillLoyaltyCardId);
                    table.CheckConstraint("CK_RefillLoyaltyCards_FreeRefills", "\"FreeRefillsAvailable\" >= 0");
                    table.CheckConstraint("CK_RefillLoyaltyCards_TickCount", "\"TickCount\" >= 0 AND \"TickCount\" <= 9");
                    table.ForeignKey(
                        name: "FK_RefillLoyaltyCards_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefillLoyaltyTransactions",
                columns: table => new
                {
                    RefillLoyaltyTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefillLoyaltyCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionType = table.Column<string>(type: "text", nullable: false),
                    Litres = table.Column<int>(type: "integer", nullable: false),
                    TicksAdded = table.Column<int>(type: "integer", nullable: false),
                    FreeRefillsAdded = table.Column<int>(type: "integer", nullable: false),
                    FreeRefillsUsed = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefillLoyaltyTransactions", x => x.RefillLoyaltyTransactionId);
                    table.CheckConstraint("CK_RefillLoyaltyTransactions_FreeAdded", "\"FreeRefillsAdded\" >= 0");
                    table.CheckConstraint("CK_RefillLoyaltyTransactions_FreeUsed", "\"FreeRefillsUsed\" >= 0");
                    table.CheckConstraint("CK_RefillLoyaltyTransactions_Litres", "\"Litres\" > 0");
                    table.CheckConstraint("CK_RefillLoyaltyTransactions_TicksAdded", "\"TicksAdded\" >= 0");
                    table.CheckConstraint("CK_RefillLoyaltyTransactions_Type", "\"TransactionType\" IN ('TICK_EARNED', 'FREE_REFILL_REDEEMED')");
                    table.ForeignKey(
                        name: "FK_RefillLoyaltyTransactions_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "OrderItemId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RefillLoyaltyTransactions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RefillLoyaltyTransactions_RefillLoyaltyCards_RefillLoyaltyC~",
                        column: x => x.RefillLoyaltyCardId,
                        principalTable: "RefillLoyaltyCards",
                        principalColumn: "RefillLoyaltyCardId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefillLoyaltyCards_CustomerId",
                table: "RefillLoyaltyCards",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefillLoyaltyTransactions_OrderId",
                table: "RefillLoyaltyTransactions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RefillLoyaltyTransactions_OrderItemId",
                table: "RefillLoyaltyTransactions",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RefillLoyaltyTransactions_RefillLoyaltyCardId",
                table: "RefillLoyaltyTransactions",
                column: "RefillLoyaltyCardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefillLoyaltyTransactions");

            migrationBuilder.DropTable(
                name: "RefillLoyaltyCards");
        }
    }
}
