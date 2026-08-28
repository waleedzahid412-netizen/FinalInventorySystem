using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class MakeSalesInvoiceCompanyRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dev wipe: mixed-company / null-CompanyID invoices are invalid under BR-044. Reseed after update.
            // InventoryTransactions has no ReferenceType — link via SalesInvoiceItemID / SalesReturnItemID.
            migrationBuilder.Sql(@"
DELETE FROM dbo.InvoiceEditAudits;
DELETE FROM dbo.InvoiceDiscounts;
DELETE FROM dbo.InvoicePromotions;
DELETE FROM dbo.InventoryTransactions WHERE SalesReturnItemID IS NOT NULL OR SalesInvoiceItemID IS NOT NULL;
DELETE FROM dbo.SalesReturnItems;
DELETE FROM dbo.SalesReturns;
DELETE FROM dbo.CustomerLedger WHERE SalesInvoiceID IS NOT NULL OR CustomerPaymentID IS NOT NULL OR SalesReturnID IS NOT NULL;
DELETE FROM dbo.ChequeStatusAudits WHERE PaymentType = N'Customer';
DELETE FROM dbo.CustomerPayments;
DELETE FROM dbo.SalesInvoiceItems;
DELETE FROM dbo.SalesInvoices;
");

            migrationBuilder.AlterColumn<int>(
                name: "CompanyID",
                schema: "dbo",
                table: "SalesInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Remove scaffolding default so inserts must supply CompanyID.
            migrationBuilder.AlterColumn<int>(
                name: "CompanyID",
                schema: "dbo",
                table: "SalesInvoices",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CompanyID",
                schema: "dbo",
                table: "SalesInvoices",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
