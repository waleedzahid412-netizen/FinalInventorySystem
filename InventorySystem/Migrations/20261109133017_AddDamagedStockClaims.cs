using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDamagedStockClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DamagedStockClaimItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClaimPendingQuantity",
                schema: "dbo",
                table: "InventoryStock",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DamagedStockClaimID",
                schema: "dbo",
                table: "CompanyLedger",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DamagedStockClaims",
                schema: "dbo",
                columns: table => new
                {
                    DamagedStockClaimID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    ClaimDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaimStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SettlementStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TotalClaimedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    TotalAcceptedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    TotalRejectedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    TotalClaimAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalSettledAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupplierResponseNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<int>(type: "int", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DamagedStockClaims", x => x.DamagedStockClaimID);
                    table.ForeignKey(
                        name: "FK_DamagedStockClaims_Companies_CompanyID",
                        column: x => x.CompanyID,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "CompanyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DamagedStockClaims_Users_ClosedBy",
                        column: x => x.ClosedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_DamagedStockClaims_Users_ConfirmedBy",
                        column: x => x.ConfirmedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_DamagedStockClaims_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_DamagedStockClaims_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "DamagedStockClaimItems",
                schema: "dbo",
                columns: table => new
                {
                    DamagedStockClaimItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DamagedStockClaimID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    WarehouseID = table.Column<int>(type: "int", nullable: false),
                    InventoryStockID = table.Column<int>(type: "int", nullable: false),
                    DamagedQuantityAvailableAtClaim = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ClaimedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitClaimCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LineClaimAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AcceptedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    RejectedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    RejectedDecision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RejectedDecisionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedDecisionBy = table.Column<int>(type: "int", nullable: true),
                    DamageReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DamagedStockClaimItems", x => x.DamagedStockClaimItemID);
                    table.ForeignKey(
                        name: "FK_DamagedStockClaimItems_DamagedStockClaims_DamagedStockClaimID",
                        column: x => x.DamagedStockClaimID,
                        principalSchema: "dbo",
                        principalTable: "DamagedStockClaims",
                        principalColumn: "DamagedStockClaimID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DamagedStockClaimItems_InventoryStock_InventoryStockID",
                        column: x => x.InventoryStockID,
                        principalSchema: "dbo",
                        principalTable: "InventoryStock",
                        principalColumn: "InventoryStockID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DamagedStockClaimItems_Products_ProductID",
                        column: x => x.ProductID,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DamagedStockClaimItems_Users_RejectedDecisionBy",
                        column: x => x.RejectedDecisionBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_DamagedStockClaimItems_Warehouses_WarehouseID",
                        column: x => x.WarehouseID,
                        principalSchema: "dbo",
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DamagedStockClaimSettlements",
                schema: "dbo",
                columns: table => new
                {
                    DamagedStockClaimSettlementID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DamagedStockClaimID = table.Column<int>(type: "int", nullable: false),
                    SettlementMethod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SettlementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    WarehouseID = table.Column<int>(type: "int", nullable: true),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    UnitCostInBase = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IsVerifiedReceived = table.Column<bool>(type: "bit", nullable: false),
                    IsPosted = table.Column<bool>(type: "bit", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompanyLedgerID = table.Column<int>(type: "int", nullable: true),
                    InventoryTransactionID = table.Column<int>(type: "int", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DamagedStockClaimSettlements", x => x.DamagedStockClaimSettlementID);
                    table.ForeignKey(
                        name: "FK_DamagedStockClaimSettlements_CompanyLedger_CompanyLedgerID",
                        column: x => x.CompanyLedgerID,
                        principalSchema: "dbo",
                        principalTable: "CompanyLedger",
                        principalColumn: "CompanyLedgerID");
                    table.ForeignKey(
                        name: "FK_DamagedStockClaimSettlements_DamagedStockClaims_DamagedStockClaimID",
                        column: x => x.DamagedStockClaimID,
                        principalSchema: "dbo",
                        principalTable: "DamagedStockClaims",
                        principalColumn: "DamagedStockClaimID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DamagedStockClaimSettlements_InventoryTransactions_InventoryTransactionID",
                        column: x => x.InventoryTransactionID,
                        principalSchema: "dbo",
                        principalTable: "InventoryTransactions",
                        principalColumn: "TransactionID");
                    table.ForeignKey(
                        name: "FK_DamagedStockClaimSettlements_Products_ProductID",
                        column: x => x.ProductID,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DamagedStockClaimSettlements_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_DamagedStockClaimSettlements_Warehouses_WarehouseID",
                        column: x => x.WarehouseID,
                        principalSchema: "dbo",
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_DamagedStockClaimItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "DamagedStockClaimItemID");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyLedger_DamagedStockClaimID",
                schema: "dbo",
                table: "CompanyLedger",
                column: "DamagedStockClaimID");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaimItems_DamagedStockClaimID",
                schema: "dbo",
                table: "DamagedStockClaimItems",
                column: "DamagedStockClaimID");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaimItems_InventoryStockID",
                schema: "dbo",
                table: "DamagedStockClaimItems",
                column: "InventoryStockID");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaimItems_ProductID",
                schema: "dbo",
                table: "DamagedStockClaimItems",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaimItems_RejectedDecisionBy",
                schema: "dbo",
                table: "DamagedStockClaimItems",
                column: "RejectedDecisionBy");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaimItems_WarehouseID",
                schema: "dbo",
                table: "DamagedStockClaimItems",
                column: "WarehouseID");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaims_ClaimNumber",
                schema: "dbo",
                table: "DamagedStockClaims",
                column: "ClaimNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaims_ClosedBy",
                schema: "dbo",
                table: "DamagedStockClaims",
                column: "ClosedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaims_CompanyID",
                schema: "dbo",
                table: "DamagedStockClaims",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaims_ConfirmedBy",
                schema: "dbo",
                table: "DamagedStockClaims",
                column: "ConfirmedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaims_CreatedBy",
                schema: "dbo",
                table: "DamagedStockClaims",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaims_UpdatedBy",
                schema: "dbo",
                table: "DamagedStockClaims",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaimSettlements_CompanyLedgerID",
                schema: "dbo",
                table: "DamagedStockClaimSettlements",
                column: "CompanyLedgerID");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaimSettlements_CreatedBy",
                schema: "dbo",
                table: "DamagedStockClaimSettlements",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaimSettlements_DamagedStockClaimID",
                schema: "dbo",
                table: "DamagedStockClaimSettlements",
                column: "DamagedStockClaimID");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaimSettlements_InventoryTransactionID",
                schema: "dbo",
                table: "DamagedStockClaimSettlements",
                column: "InventoryTransactionID");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaimSettlements_ProductID",
                schema: "dbo",
                table: "DamagedStockClaimSettlements",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_DamagedStockClaimSettlements_WarehouseID",
                schema: "dbo",
                table: "DamagedStockClaimSettlements",
                column: "WarehouseID");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyLedger_DamagedStockClaims_DamagedStockClaimID",
                schema: "dbo",
                table: "CompanyLedger",
                column: "DamagedStockClaimID",
                principalSchema: "dbo",
                principalTable: "DamagedStockClaims",
                principalColumn: "DamagedStockClaimID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_DamagedStockClaimItems_DamagedStockClaimItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "DamagedStockClaimItemID",
                principalSchema: "dbo",
                principalTable: "DamagedStockClaimItems",
                principalColumn: "DamagedStockClaimItemID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyLedger_DamagedStockClaims_DamagedStockClaimID",
                schema: "dbo",
                table: "CompanyLedger");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_DamagedStockClaimItems_DamagedStockClaimItemID",
                schema: "dbo",
                table: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "DamagedStockClaimItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DamagedStockClaimSettlements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DamagedStockClaims",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_DamagedStockClaimItemID",
                schema: "dbo",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CompanyLedger_DamagedStockClaimID",
                schema: "dbo",
                table: "CompanyLedger");

            migrationBuilder.DropColumn(
                name: "DamagedStockClaimItemID",
                schema: "dbo",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "ClaimPendingQuantity",
                schema: "dbo",
                table: "InventoryStock");

            migrationBuilder.DropColumn(
                name: "DamagedStockClaimID",
                schema: "dbo",
                table: "CompanyLedger");
        }
    }
}
