using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyID",
                schema: "dbo",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("UPDATE [dbo].[Products] SET [CompanyID] = (SELECT TOP 1 [CompanyID] FROM [dbo].[Companies]) WHERE [CompanyID] = 0 OR [CompanyID] NOT IN (SELECT [CompanyID] FROM [dbo].[Companies]);");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CompanyID",
                schema: "dbo",
                table: "Products",
                column: "CompanyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Companies_CompanyID",
                schema: "dbo",
                table: "Products",
                column: "CompanyID",
                principalSchema: "dbo",
                principalTable: "Companies",
                principalColumn: "CompanyID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Companies_CompanyID",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_CompanyID",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CompanyID",
                schema: "dbo",
                table: "Products");
        }
    }
}
