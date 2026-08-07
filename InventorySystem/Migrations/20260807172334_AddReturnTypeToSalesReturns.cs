using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnTypeToSalesReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "InvoiceID",
                schema: "dbo",
                table: "SalesReturns",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "ReturnType",
                schema: "dbo",
                table: "SalesReturns",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SettlementMethod",
                schema: "dbo",
                table: "SalesReturns",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "WarehouseID",
                schema: "dbo",
                table: "SalesReturns",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceItemID",
                schema: "dbo",
                table: "SalesReturnItems",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_WarehouseID",
                schema: "dbo",
                table: "SalesReturns",
                column: "WarehouseID");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturns_Warehouses_WarehouseID",
                schema: "dbo",
                table: "SalesReturns",
                column: "WarehouseID",
                principalSchema: "dbo",
                principalTable: "Warehouses",
                principalColumn: "WarehouseID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturns_Warehouses_WarehouseID",
                schema: "dbo",
                table: "SalesReturns");

            migrationBuilder.DropIndex(
                name: "IX_SalesReturns_WarehouseID",
                schema: "dbo",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "ReturnType",
                schema: "dbo",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "SettlementMethod",
                schema: "dbo",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "WarehouseID",
                schema: "dbo",
                table: "SalesReturns");

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceID",
                schema: "dbo",
                table: "SalesReturns",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceItemID",
                schema: "dbo",
                table: "SalesReturnItems",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
