using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddBookerCompanyAndProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dev wipe: existing bookers have no CompanyID; clear FKs then delete rows before NOT NULL FK.
            migrationBuilder.Sql("UPDATE dbo.SalesInvoices SET BookerID = NULL WHERE BookerID IS NOT NULL;");
            migrationBuilder.Sql("DELETE FROM dbo.Bookers;");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "dbo",
                table: "Bookers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CNIC",
                schema: "dbo",
                table: "Bookers",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyID",
                schema: "dbo",
                table: "Bookers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "Bookers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                schema: "dbo",
                table: "Bookers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditLimit",
                schema: "dbo",
                table: "Bookers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "dbo",
                table: "Bookers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                schema: "dbo",
                table: "Bookers",
                type: "int",
                nullable: true);

            // Remove temporary scaffolding defaults so app must supply CompanyID/CreatedAt.
            migrationBuilder.AlterColumn<int>(
                name: "CompanyID",
                schema: "dbo",
                table: "Bookers",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "Bookers",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Bookers_CompanyID",
                schema: "dbo",
                table: "Bookers",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookers_CreatedBy",
                schema: "dbo",
                table: "Bookers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Bookers_UpdatedBy",
                schema: "dbo",
                table: "Bookers",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UX_Bookers_CNIC",
                schema: "dbo",
                table: "Bookers",
                column: "CNIC",
                unique: true,
                filter: "[IsDeleted] = 0 AND [CNIC] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Bookers_CompanyID_Name",
                schema: "dbo",
                table: "Bookers",
                columns: new[] { "CompanyID", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookers_Companies_CompanyID",
                schema: "dbo",
                table: "Bookers",
                column: "CompanyID",
                principalSchema: "dbo",
                principalTable: "Companies",
                principalColumn: "CompanyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookers_Users_CreatedBy",
                schema: "dbo",
                table: "Bookers",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookers_Users_UpdatedBy",
                schema: "dbo",
                table: "Bookers",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookers_Companies_CompanyID",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookers_Users_CreatedBy",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookers_Users_UpdatedBy",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropIndex(
                name: "IX_Bookers_CompanyID",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropIndex(
                name: "IX_Bookers_CreatedBy",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropIndex(
                name: "IX_Bookers_UpdatedBy",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropIndex(
                name: "UX_Bookers_CNIC",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropIndex(
                name: "UX_Bookers_CompanyID_Name",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropColumn(
                name: "Address",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropColumn(
                name: "CNIC",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropColumn(
                name: "CompanyID",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropColumn(
                name: "CreditLimit",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "dbo",
                table: "Bookers");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                schema: "dbo",
                table: "Bookers");
        }
    }
}
