using InventorySystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260918203000_RemoveAreaAndSubAreaCode")]
    public partial class RemoveAreaAndSubAreaCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Areas_Code",
                schema: "dbo",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "dbo",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "dbo",
                table: "SubAreas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "dbo",
                table: "SubAreas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "dbo",
                table: "Areas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Areas_Code",
                schema: "dbo",
                table: "Areas",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");
        }
    }
}
