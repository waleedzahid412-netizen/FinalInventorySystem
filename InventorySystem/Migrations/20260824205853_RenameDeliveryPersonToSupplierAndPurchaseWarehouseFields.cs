using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class RenameDeliveryPersonToSupplierAndPurchaseWarehouseFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_DeliveryPersons_DeliveryPersonID",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.RenameTable(
                name: "DeliveryPersons",
                schema: "dbo",
                newName: "Suppliers");

            migrationBuilder.RenameColumn(
                name: "DeliveryPersonID",
                schema: "dbo",
                table: "Suppliers",
                newName: "SupplierID");

            migrationBuilder.Sql("EXEC sp_rename N'[dbo].[PK_DeliveryPersons]', N'PK_Suppliers', N'OBJECT';");

            migrationBuilder.RenameColumn(
                name: "DeliveryPersonID",
                schema: "dbo",
                table: "SalesInvoices",
                newName: "SupplierID");

            migrationBuilder.RenameIndex(
                name: "IX_SalesInvoices_DeliveryPersonID",
                schema: "dbo",
                table: "SalesInvoices",
                newName: "IX_SalesInvoices_SupplierID");

            migrationBuilder.AddColumn<string>(
                name: "CNIC",
                schema: "dbo",
                table: "Suppliers",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "dbo",
                table: "Suppliers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "Suppliers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                schema: "dbo",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "dbo",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                schema: "dbo",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE [dbo].[Suppliers]
SET [CNIC] = CONCAT(N'42101-', RIGHT(REPLICATE(N'0', 7) + CAST([SupplierID] AS nvarchar(7)), 7), N'-1')
WHERE [CNIC] IS NULL OR LTRIM(RTRIM([CNIC])) = N'';
");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CreatedBy",
                schema: "dbo",
                table: "Suppliers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_UpdatedBy",
                schema: "dbo",
                table: "Suppliers",
                column: "UpdatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Suppliers_SupplierID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "SupplierID",
                principalSchema: "dbo",
                principalTable: "Suppliers",
                principalColumn: "SupplierID");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Users_CreatedBy",
                schema: "dbo",
                table: "Suppliers",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Users_UpdatedBy",
                schema: "dbo",
                table: "Suppliers",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddColumn<string>(
                name: "SupplierInvoiceNumber",
                schema: "dbo",
                table: "PurchaseInvoices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMain",
                schema: "dbo",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
UPDATE [dbo].[Warehouses]
SET [IsMain] = CASE WHEN [Name] = N'Main Warehouse' THEN 1 ELSE 0 END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Warehouses] WHERE [IsMain] = 1 AND [IsDeleted] = 0)
BEGIN
    ;WITH cte AS (
        SELECT TOP (1) [WarehouseID]
        FROM [dbo].[Warehouses]
        WHERE [IsDeleted] = 0
        ORDER BY [WarehouseID]
    )
    UPDATE w
    SET [IsMain] = 1
    FROM [dbo].[Warehouses] w
    INNER JOIN cte ON cte.[WarehouseID] = w.[WarehouseID];
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Suppliers_SupplierID",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Users_CreatedBy",
                schema: "dbo",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Users_UpdatedBy",
                schema: "dbo",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CreatedBy",
                schema: "dbo",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_UpdatedBy",
                schema: "dbo",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CNIC",
                schema: "dbo",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Address",
                schema: "dbo",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "dbo",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "dbo",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "dbo",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                schema: "dbo",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "SupplierInvoiceNumber",
                schema: "dbo",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "IsMain",
                schema: "dbo",
                table: "Warehouses");

            migrationBuilder.RenameIndex(
                name: "IX_SalesInvoices_SupplierID",
                schema: "dbo",
                table: "SalesInvoices",
                newName: "IX_SalesInvoices_DeliveryPersonID");

            migrationBuilder.RenameColumn(
                name: "SupplierID",
                schema: "dbo",
                table: "SalesInvoices",
                newName: "DeliveryPersonID");

            migrationBuilder.Sql("EXEC sp_rename N'[dbo].[PK_Suppliers]', N'PK_DeliveryPersons', N'OBJECT';");

            migrationBuilder.RenameColumn(
                name: "SupplierID",
                schema: "dbo",
                table: "Suppliers",
                newName: "DeliveryPersonID");

            migrationBuilder.RenameTable(
                name: "Suppliers",
                schema: "dbo",
                newName: "DeliveryPersons");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_DeliveryPersons_DeliveryPersonID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "DeliveryPersonID",
                principalSchema: "dbo",
                principalTable: "DeliveryPersons",
                principalColumn: "DeliveryPersonID");
        }
    }
}
