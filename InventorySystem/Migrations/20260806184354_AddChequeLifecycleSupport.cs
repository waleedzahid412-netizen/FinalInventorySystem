using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddChequeLifecycleSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankName",
                schema: "dbo",
                table: "CustomerPayments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BounceRemarks",
                schema: "dbo",
                table: "CustomerPayments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BouncedAt",
                schema: "dbo",
                table: "CustomerPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChequeDate",
                schema: "dbo",
                table: "CustomerPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChequeNumber",
                schema: "dbo",
                table: "CustomerPayments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChequeStatus",
                schema: "dbo",
                table: "CustomerPayments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClearedAt",
                schema: "dbo",
                table: "CustomerPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClearedBy",
                schema: "dbo",
                table: "CustomerPayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IssueDate",
                schema: "dbo",
                table: "CustomerPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                schema: "dbo",
                table: "CompanyPayments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BounceRemarks",
                schema: "dbo",
                table: "CompanyPayments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BouncedAt",
                schema: "dbo",
                table: "CompanyPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChequeDate",
                schema: "dbo",
                table: "CompanyPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChequeNumber",
                schema: "dbo",
                table: "CompanyPayments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChequeStatus",
                schema: "dbo",
                table: "CompanyPayments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClearedAt",
                schema: "dbo",
                table: "CompanyPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClearedBy",
                schema: "dbo",
                table: "CompanyPayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IssueDate",
                schema: "dbo",
                table: "CompanyPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChequeStatusAudits",
                schema: "dbo",
                columns: table => new
                {
                    ChequeStatusAuditID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentID = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChequeStatusAudits", x => x.ChequeStatusAuditID);
                    table.ForeignKey(
                        name: "FK_ChequeStatusAudits_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChequeStatusAudits_UpdatedBy",
                schema: "dbo",
                table: "ChequeStatusAudits",
                column: "UpdatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChequeStatusAudits",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "BankName",
                schema: "dbo",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "BounceRemarks",
                schema: "dbo",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "BouncedAt",
                schema: "dbo",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "ChequeDate",
                schema: "dbo",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "ChequeNumber",
                schema: "dbo",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "ChequeStatus",
                schema: "dbo",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "ClearedAt",
                schema: "dbo",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "ClearedBy",
                schema: "dbo",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "IssueDate",
                schema: "dbo",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "BankName",
                schema: "dbo",
                table: "CompanyPayments");

            migrationBuilder.DropColumn(
                name: "BounceRemarks",
                schema: "dbo",
                table: "CompanyPayments");

            migrationBuilder.DropColumn(
                name: "BouncedAt",
                schema: "dbo",
                table: "CompanyPayments");

            migrationBuilder.DropColumn(
                name: "ChequeDate",
                schema: "dbo",
                table: "CompanyPayments");

            migrationBuilder.DropColumn(
                name: "ChequeNumber",
                schema: "dbo",
                table: "CompanyPayments");

            migrationBuilder.DropColumn(
                name: "ChequeStatus",
                schema: "dbo",
                table: "CompanyPayments");

            migrationBuilder.DropColumn(
                name: "ClearedAt",
                schema: "dbo",
                table: "CompanyPayments");

            migrationBuilder.DropColumn(
                name: "ClearedBy",
                schema: "dbo",
                table: "CompanyPayments");

            migrationBuilder.DropColumn(
                name: "IssueDate",
                schema: "dbo",
                table: "CompanyPayments");
        }
    }
}
