using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerLoadSheetAndInvoiceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BrokerID",
                schema: "dbo",
                table: "SalesInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyID",
                schema: "dbo",
                table: "SalesInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SalespersonID",
                schema: "dbo",
                table: "SalesInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Brokers",
                schema: "dbo",
                columns: table => new
                {
                    BrokerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brokers", x => x.BrokerID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_BrokerID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "BrokerID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_CompanyID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_SalespersonID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "SalespersonID");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Brokers_BrokerID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "BrokerID",
                principalSchema: "dbo",
                principalTable: "Brokers",
                principalColumn: "BrokerID");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Companies_CompanyID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "CompanyID",
                principalSchema: "dbo",
                principalTable: "Companies",
                principalColumn: "CompanyID");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Users_SalespersonID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "SalespersonID",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            // ---- Backfill historical invoices (safe / non-invented) ----
            migrationBuilder.Sql(@"
UPDATE dbo.SalesInvoices
SET SalespersonID = CreatedBy
WHERE SalespersonID IS NULL AND CreatedBy IS NOT NULL;
");

            migrationBuilder.Sql(@"
UPDATE si
SET si.CompanyID = x.CompanyID
FROM dbo.SalesInvoices si
INNER JOIN (
    SELECT sii.InvoiceID, MIN(p.CompanyID) AS CompanyID
    FROM dbo.SalesInvoiceItems sii
    INNER JOIN dbo.Products p ON p.ProductID = sii.ProductID
    WHERE sii.ProductID IS NOT NULL AND sii.IsDeleted = 0
    GROUP BY sii.InvoiceID
    HAVING COUNT(DISTINCT p.CompanyID) = 1
) x ON x.InvoiceID = si.InvoiceID
WHERE si.CompanyID IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Brokers_BrokerID",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Companies_CompanyID",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Users_SalespersonID",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropTable(
                name: "Brokers",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_BrokerID",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_CompanyID",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_SalespersonID",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "BrokerID",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "CompanyID",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "SalespersonID",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "dbo",
                table: "Products");
        }
    }
}
