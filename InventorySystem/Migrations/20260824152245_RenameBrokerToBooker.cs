using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class RenameBrokerToBooker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Brokers_BrokerID",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.RenameTable(
                name: "Brokers",
                schema: "dbo",
                newName: "Bookers");

            migrationBuilder.RenameColumn(
                name: "BrokerID",
                schema: "dbo",
                table: "Bookers",
                newName: "BookerID");

            // RenameTable/RenameColumn leave the PK constraint name as PK_Brokers.
            migrationBuilder.Sql("EXEC sp_rename N'[dbo].[PK_Brokers]', N'PK_Bookers', N'OBJECT';");

            migrationBuilder.RenameColumn(
                name: "BrokerID",
                schema: "dbo",
                table: "SalesInvoices",
                newName: "BookerID");

            migrationBuilder.RenameIndex(
                name: "IX_SalesInvoices_BrokerID",
                schema: "dbo",
                table: "SalesInvoices",
                newName: "IX_SalesInvoices_BookerID");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Bookers_BookerID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "BookerID",
                principalSchema: "dbo",
                principalTable: "Bookers",
                principalColumn: "BookerID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Bookers_BookerID",
                schema: "dbo",
                table: "SalesInvoices");

            migrationBuilder.RenameIndex(
                name: "IX_SalesInvoices_BookerID",
                schema: "dbo",
                table: "SalesInvoices",
                newName: "IX_SalesInvoices_BrokerID");

            migrationBuilder.RenameColumn(
                name: "BookerID",
                schema: "dbo",
                table: "SalesInvoices",
                newName: "BrokerID");

            migrationBuilder.Sql("EXEC sp_rename N'[dbo].[PK_Bookers]', N'PK_Brokers', N'OBJECT';");

            migrationBuilder.RenameColumn(
                name: "BookerID",
                schema: "dbo",
                table: "Bookers",
                newName: "BrokerID");

            migrationBuilder.RenameTable(
                name: "Bookers",
                schema: "dbo",
                newName: "Brokers");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Brokers_BrokerID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "BrokerID",
                principalSchema: "dbo",
                principalTable: "Brokers",
                principalColumn: "BrokerID");
        }
    }
}
