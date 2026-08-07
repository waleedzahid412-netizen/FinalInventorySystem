using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class ProductionHardeningInvoiceEdit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "SalesInvoices",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "dbo",
                table: "SalesInvoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                schema: "dbo",
                table: "SalesInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "dbo",
                table: "SalesInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "PurchaseInvoices",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "dbo",
                table: "PurchaseInvoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                schema: "dbo",
                table: "PurchaseInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "dbo",
                table: "PurchaseInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "NewDiscountTotal",
                schema: "dbo",
                table: "InvoiceEditAudits",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "NewItemCount",
                schema: "dbo",
                table: "InvoiceEditAudits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "NewSubTotal",
                schema: "dbo",
                table: "InvoiceEditAudits",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "NewVersion",
                schema: "dbo",
                table: "InvoiceEditAudits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "OldDiscountTotal",
                schema: "dbo",
                table: "InvoiceEditAudits",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OldSubTotal",
                schema: "dbo",
                table: "InvoiceEditAudits",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PreviousItemCount",
                schema: "dbo",
                table: "InvoiceEditAudits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PreviousVersion",
                schema: "dbo",
                table: "InvoiceEditAudits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PurchaseReturns",
                schema: "dbo",
                columns: table => new
                {
                    PurchaseReturnID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseInvoiceID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturns", x => x.PurchaseReturnID);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Companies_CompanyID",
                        column: x => x.CompanyID,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "CompanyID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_PurchaseInvoices_PurchaseInvoiceID",
                        column: x => x.PurchaseInvoiceID,
                        principalSchema: "dbo",
                        principalTable: "PurchaseInvoices",
                        principalColumn: "PurchaseInvoiceID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_UpdatedBy",
                schema: "dbo",
                table: "SalesInvoices",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_UpdatedBy",
                schema: "dbo",
                table: "PurchaseInvoices",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_CompanyID",
                schema: "dbo",
                table: "PurchaseReturns",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_CreatedBy",
                schema: "dbo",
                table: "PurchaseReturns",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_PurchaseInvoiceID",
                schema: "dbo",
                table: "PurchaseReturns",
                column: "PurchaseInvoiceID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Users_UpdatedBy",
                schema: "dbo",
                table: "PurchaseInvoices",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Users_UpdatedBy",
                schema: "dbo",
                table: "SalesInvoices",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_Users_UpdatedBy",
                schema: "dbo",
                table: "PurchaseInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Users_UpdatedBy",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropTable(
                name: "PurchaseReturns",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_UpdatedBy",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_UpdatedBy",
                schema: "dbo",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "dbo",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "dbo",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                schema: "dbo",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "dbo",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "NewDiscountTotal",
                schema: "dbo",
                table: "InvoiceEditAudits");

            migrationBuilder.DropColumn(
                name: "NewItemCount",
                schema: "dbo",
                table: "InvoiceEditAudits");

            migrationBuilder.DropColumn(
                name: "NewSubTotal",
                schema: "dbo",
                table: "InvoiceEditAudits");

            migrationBuilder.DropColumn(
                name: "NewVersion",
                schema: "dbo",
                table: "InvoiceEditAudits");

            migrationBuilder.DropColumn(
                name: "OldDiscountTotal",
                schema: "dbo",
                table: "InvoiceEditAudits");

            migrationBuilder.DropColumn(
                name: "OldSubTotal",
                schema: "dbo",
                table: "InvoiceEditAudits");

            migrationBuilder.DropColumn(
                name: "PreviousItemCount",
                schema: "dbo",
                table: "InvoiceEditAudits");

            migrationBuilder.DropColumn(
                name: "PreviousVersion",
                schema: "dbo",
                table: "InvoiceEditAudits");
        }
    }
}
