using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceEditAuditEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceEditAudits",
                schema: "dbo",
                columns: table => new
                {
                    InvoiceEditAuditID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceID = table.Column<int>(type: "int", nullable: false),
                    InvoiceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OldGrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewGrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EditReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EditedBy = table.Column<int>(type: "int", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceEditAudits", x => x.InvoiceEditAuditID);
                    table.ForeignKey(
                        name: "FK_InvoiceEditAudits_Users_EditedBy",
                        column: x => x.EditedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEditAudits_EditedBy",
                schema: "dbo",
                table: "InvoiceEditAudits",
                column: "EditedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceEditAudits",
                schema: "dbo");
        }
    }
}
