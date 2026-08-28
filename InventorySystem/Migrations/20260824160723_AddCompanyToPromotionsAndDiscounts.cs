using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyToPromotionsAndDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dev wipe: existing campaigns/rules have no CompanyID; reseed after update.
            migrationBuilder.Sql(@"
DELETE FROM dbo.InvoicePromotions;
UPDATE dbo.InvoiceDiscounts SET DiscountRuleID = NULL WHERE DiscountRuleID IS NOT NULL;
DELETE FROM dbo.PromotionRules;
DELETE FROM dbo.PromotionCampaigns;
DELETE FROM dbo.DiscountRules;
");

            migrationBuilder.AddColumn<int>(
                name: "CompanyID",
                schema: "dbo",
                table: "PromotionCampaigns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyID",
                schema: "dbo",
                table: "DiscountRules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Remove scaffolding defaults so inserts must supply CompanyID.
            migrationBuilder.AlterColumn<int>(
                name: "CompanyID",
                schema: "dbo",
                table: "PromotionCampaigns",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "CompanyID",
                schema: "dbo",
                table: "DiscountRules",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionCampaigns_CompanyID",
                schema: "dbo",
                table: "PromotionCampaigns",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_CompanyID",
                schema: "dbo",
                table: "DiscountRules",
                column: "CompanyID");

            migrationBuilder.AddForeignKey(
                name: "FK_DiscountRules_Companies_CompanyID",
                schema: "dbo",
                table: "DiscountRules",
                column: "CompanyID",
                principalSchema: "dbo",
                principalTable: "Companies",
                principalColumn: "CompanyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PromotionCampaigns_Companies_CompanyID",
                schema: "dbo",
                table: "PromotionCampaigns",
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
                name: "FK_DiscountRules_Companies_CompanyID",
                schema: "dbo",
                table: "DiscountRules");

            migrationBuilder.DropForeignKey(
                name: "FK_PromotionCampaigns_Companies_CompanyID",
                schema: "dbo",
                table: "PromotionCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_PromotionCampaigns_CompanyID",
                schema: "dbo",
                table: "PromotionCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_DiscountRules_CompanyID",
                schema: "dbo",
                table: "DiscountRules");

            migrationBuilder.DropColumn(
                name: "CompanyID",
                schema: "dbo",
                table: "PromotionCampaigns");

            migrationBuilder.DropColumn(
                name: "CompanyID",
                schema: "dbo",
                table: "DiscountRules");
        }
    }
}
