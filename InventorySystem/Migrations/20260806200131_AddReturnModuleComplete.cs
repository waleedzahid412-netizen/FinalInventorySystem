using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnModuleComplete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                schema: "dbo",
                table: "SalesReturns",
                newName: "PromoPenalty");

            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                schema: "dbo",
                table: "PurchaseReturns",
                newName: "NetRefundAmount");

            migrationBuilder.AddColumn<decimal>(
                name: "ClawbackPenalty",
                schema: "dbo",
                table: "SalesReturns",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossAmount",
                schema: "dbo",
                table: "SalesReturns",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetRefundAmount",
                schema: "dbo",
                table: "SalesReturns",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ReturnNumber",
                schema: "dbo",
                table: "SalesReturns",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ConvertedQuantity",
                schema: "dbo",
                table: "SalesReturnItems",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "dbo",
                table: "SalesReturnItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundUnitPrice",
                schema: "dbo",
                table: "SalesReturnItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ReturnCondition",
                schema: "dbo",
                table: "SalesReturnItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "GrossAmount",
                schema: "dbo",
                table: "PurchaseReturns",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ReturnNumber",
                schema: "dbo",
                table: "PurchaseReturns",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "WarehouseID",
                schema: "dbo",
                table: "PurchaseReturns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseReturnItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseReturnID",
                schema: "dbo",
                table: "CompanyLedger",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseReturnItems",
                schema: "dbo",
                columns: table => new
                {
                    PurchaseReturnItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseReturnID = table.Column<int>(type: "int", nullable: false),
                    PurchaseInvoiceItemID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    ProductUnitID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ConvertedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RefundUnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReturnCondition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturnItems", x => x.PurchaseReturnItemID);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_ProductUnits_ProductUnitID",
                        column: x => x.ProductUnitID,
                        principalSchema: "dbo",
                        principalTable: "ProductUnits",
                        principalColumn: "ProductUnitID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_Products_ProductID",
                        column: x => x.ProductID,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_PurchaseInvoiceItems_PurchaseInvoiceItemID",
                        column: x => x.PurchaseInvoiceItemID,
                        principalSchema: "dbo",
                        principalTable: "PurchaseInvoiceItems",
                        principalColumn: "PurchaseItemID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturnID",
                        column: x => x.PurchaseReturnID,
                        principalSchema: "dbo",
                        principalTable: "PurchaseReturns",
                        principalColumn: "PurchaseReturnID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_WarehouseID",
                schema: "dbo",
                table: "PurchaseReturns",
                column: "WarehouseID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_PurchaseReturnItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "PurchaseReturnItemID");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyLedger_PurchaseReturnID",
                schema: "dbo",
                table: "CompanyLedger",
                column: "PurchaseReturnID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_ProductID",
                schema: "dbo",
                table: "PurchaseReturnItems",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_ProductUnitID",
                schema: "dbo",
                table: "PurchaseReturnItems",
                column: "ProductUnitID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_PurchaseInvoiceItemID",
                schema: "dbo",
                table: "PurchaseReturnItems",
                column: "PurchaseInvoiceItemID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_PurchaseReturnID",
                schema: "dbo",
                table: "PurchaseReturnItems",
                column: "PurchaseReturnID");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyLedger_PurchaseReturns_PurchaseReturnID",
                schema: "dbo",
                table: "CompanyLedger",
                column: "PurchaseReturnID",
                principalSchema: "dbo",
                principalTable: "PurchaseReturns",
                principalColumn: "PurchaseReturnID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_PurchaseReturnItems_PurchaseReturnItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "PurchaseReturnItemID",
                principalSchema: "dbo",
                principalTable: "PurchaseReturnItems",
                principalColumn: "PurchaseReturnItemID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Warehouses_WarehouseID",
                schema: "dbo",
                table: "PurchaseReturns",
                column: "WarehouseID",
                principalSchema: "dbo",
                principalTable: "Warehouses",
                principalColumn: "WarehouseID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyLedger_PurchaseReturns_PurchaseReturnID",
                schema: "dbo",
                table: "CompanyLedger");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_PurchaseReturnItems_PurchaseReturnItemID",
                schema: "dbo",
                table: "InventoryTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Warehouses_WarehouseID",
                schema: "dbo",
                table: "PurchaseReturns");

            migrationBuilder.DropTable(
                name: "PurchaseReturnItems",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_WarehouseID",
                schema: "dbo",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_PurchaseReturnItemID",
                schema: "dbo",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CompanyLedger_PurchaseReturnID",
                schema: "dbo",
                table: "CompanyLedger");

            migrationBuilder.DropColumn(
                name: "ClawbackPenalty",
                schema: "dbo",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "GrossAmount",
                schema: "dbo",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "NetRefundAmount",
                schema: "dbo",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "ReturnNumber",
                schema: "dbo",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "ConvertedQuantity",
                schema: "dbo",
                table: "SalesReturnItems");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "dbo",
                table: "SalesReturnItems");

            migrationBuilder.DropColumn(
                name: "RefundUnitPrice",
                schema: "dbo",
                table: "SalesReturnItems");

            migrationBuilder.DropColumn(
                name: "ReturnCondition",
                schema: "dbo",
                table: "SalesReturnItems");

            migrationBuilder.DropColumn(
                name: "GrossAmount",
                schema: "dbo",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "ReturnNumber",
                schema: "dbo",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "WarehouseID",
                schema: "dbo",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "PurchaseReturnItemID",
                schema: "dbo",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "PurchaseReturnID",
                schema: "dbo",
                table: "CompanyLedger");

            migrationBuilder.RenameColumn(
                name: "PromoPenalty",
                schema: "dbo",
                table: "SalesReturns",
                newName: "TotalAmount");

            migrationBuilder.RenameColumn(
                name: "NetRefundAmount",
                schema: "dbo",
                table: "PurchaseReturns",
                newName: "TotalAmount");
        }
    }
}
