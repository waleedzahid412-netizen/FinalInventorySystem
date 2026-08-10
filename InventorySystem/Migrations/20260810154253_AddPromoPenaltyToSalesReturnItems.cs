using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPromoPenaltyToSalesReturnItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PromoPenaltyAmount",
                schema: "dbo",
                table: "SalesReturnItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PromoPenaltyQuantity",
                schema: "dbo",
                table: "SalesReturnItems",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromoPenaltyAmount",
                schema: "dbo",
                table: "SalesReturnItems");

            migrationBuilder.DropColumn(
                name: "PromoPenaltyQuantity",
                schema: "dbo",
                table: "SalesReturnItems");
        }
    }
}
