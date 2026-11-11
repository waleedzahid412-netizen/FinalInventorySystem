using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class addprductdiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ApplyProductDiscount",
                schema: "dbo",
                table: "SalesInvoices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProductDiscountAmount",
                schema: "dbo",
                table: "SalesInvoiceItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProductDiscountType",
                schema: "dbo",
                table: "SalesInvoiceItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProductDiscountValue",
                schema: "dbo",
                table: "SalesInvoiceItems",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ApplyProductDiscount",
                schema: "dbo",
                table: "Quotations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProductDiscountAmount",
                schema: "dbo",
                table: "QuotationItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProductDiscountType",
                schema: "dbo",
                table: "QuotationItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProductDiscountValue",
                schema: "dbo",
                table: "QuotationItems",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductDiscountType",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProductDiscountValue",
                schema: "dbo",
                table: "Products",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplyProductDiscount",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "ProductDiscountAmount",
                schema: "dbo",
                table: "SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ProductDiscountType",
                schema: "dbo",
                table: "SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ProductDiscountValue",
                schema: "dbo",
                table: "SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ApplyProductDiscount",
                schema: "dbo",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "ProductDiscountAmount",
                schema: "dbo",
                table: "QuotationItems");

            migrationBuilder.DropColumn(
                name: "ProductDiscountType",
                schema: "dbo",
                table: "QuotationItems");

            migrationBuilder.DropColumn(
                name: "ProductDiscountValue",
                schema: "dbo",
                table: "QuotationItems");

            migrationBuilder.DropColumn(
                name: "ProductDiscountType",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductDiscountValue",
                schema: "dbo",
                table: "Products");
        }
    }
}
