using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountModeAndHistoricalSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Incidental model drift: composite unique index on Categories already covers CompanyID.
            migrationBuilder.DropIndex(
                name: "IX_Categories_CompanyID",
                schema: "dbo",
                table: "Categories");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                schema: "dbo",
                table: "SalesReturnItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountMode",
                schema: "dbo",
                table: "SalesInvoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountRate",
                schema: "dbo",
                table: "SalesInvoiceItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "DiscountRuleID",
                schema: "dbo",
                table: "InvoiceDiscounts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "DiscountSource",
                schema: "dbo",
                table: "InvoiceDiscounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Automatic");

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumOrderAmount",
                schema: "dbo",
                table: "InvoiceDiscounts",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderAmount",
                schema: "dbo",
                table: "InvoiceDiscounts",
                type: "decimal(18,2)",
                nullable: true);

            // ---- Backfill (safe / non-invented) ----
            // 1) Invoices that already have an InvoiceDiscount row were Automatic at sale.
            migrationBuilder.Sql(@"
UPDATE si
SET si.DiscountMode = N'Automatic'
FROM [dbo].[SalesInvoices] si
WHERE EXISTS (
    SELECT 1 FROM [dbo].[InvoiceDiscounts] id WHERE id.InvoiceID = si.InvoiceID
)
AND (si.DiscountMode IS NULL OR si.DiscountMode = N'' OR si.DiscountMode = N'None');
");

            // 2) Existing InvoiceDiscounts are Automatic source; snapshot threshold from current rule
            //    ONE TIME so future returns never need live DiscountRules.
            //    NOTE: if an admin already changed the live rule's MinimumOrderAmount, this
            //    best-effort copy may not match the exact historical threshold at sale time.
            //    New invoices always write the true snapshot at create/update.
            migrationBuilder.Sql(@"
UPDATE id
SET
    id.DiscountSource = N'Automatic',
    id.MinimumOrderAmount = dr.MinimumOrderAmount,
    id.MaximumOrderAmount = dr.MaximumOrderAmount
FROM [dbo].[InvoiceDiscounts] id
INNER JOIN [dbo].[DiscountRules] dr ON dr.DiscountRuleID = id.DiscountRuleID
WHERE id.DiscountRuleID IS NOT NULL
  AND id.MinimumOrderAmount IS NULL;
");

            // 3) DO NOT invent SalesInvoiceItems.DiscountAmount / DiscountRate allocation when
            //    existing rows are 0 while InvoiceDiscounts.DiscountAmount > 0.
            //    Insufficient historical data to safely redistribute without guessing.
            //    New invoices (Phase 2+) always allocate correctly at sale time.
            //    Old invoices with item DiscountAmount = 0 will not get remaining-state item
            //    discount release until manually corrected or left on header-only legacy path.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                schema: "dbo",
                table: "SalesReturnItems");

            migrationBuilder.DropColumn(
                name: "DiscountMode",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "DiscountRate",
                schema: "dbo",
                table: "SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "DiscountSource",
                schema: "dbo",
                table: "InvoiceDiscounts");

            migrationBuilder.DropColumn(
                name: "MaximumOrderAmount",
                schema: "dbo",
                table: "InvoiceDiscounts");

            migrationBuilder.DropColumn(
                name: "MinimumOrderAmount",
                schema: "dbo",
                table: "InvoiceDiscounts");

            migrationBuilder.AlterColumn<int>(
                name: "DiscountRuleID",
                schema: "dbo",
                table: "InvoiceDiscounts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CompanyID",
                schema: "dbo",
                table: "Categories",
                column: "CompanyID");
        }
    }
}
