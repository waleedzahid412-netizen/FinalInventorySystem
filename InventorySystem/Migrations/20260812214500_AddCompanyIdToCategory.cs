using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyIdToCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                schema: "dbo",
                table: "Categories");

            migrationBuilder.AddColumn<int>(
                name: "CompanyID",
                schema: "dbo",
                table: "Categories",
                type: "int",
                nullable: true);

            // Backfill CompanyID and clone shared categories used by multiple companies.
            migrationBuilder.Sql(@"
-- Working set of distinct (CategoryID, CompanyID) pairs from products
IF OBJECT_ID('tempdb..#CatCompanyPairs') IS NOT NULL DROP TABLE #CatCompanyPairs;
SELECT
    p.CategoryID,
    p.CompanyID,
    ROW_NUMBER() OVER (PARTITION BY p.CategoryID ORDER BY p.CompanyID) AS rn
INTO #CatCompanyPairs
FROM [dbo].[Products] p
GROUP BY p.CategoryID, p.CompanyID;

-- 1) Assign original category to the first company for every referenced category
UPDATE c
SET c.CompanyID = x.CompanyID
FROM [dbo].[Categories] c
INNER JOIN #CatCompanyPairs x ON x.CategoryID = c.CategoryID AND x.rn = 1
WHERE c.CompanyID IS NULL;

-- 2) Clone category for additional companies and remap products
DECLARE @OldCategoryID INT;
DECLARE @CompanyID INT;
DECLARE @Name NVARCHAR(100);
DECLARE @IsActive BIT;
DECLARE @CreatedAt DATETIME2;
DECLARE @CreatedBy INT;
DECLARE @UpdatedAt DATETIME2;
DECLARE @UpdatedBy INT;
DECLARE @IsDeleted BIT;
DECLARE @DeletedAt DATETIME2;
DECLARE @NewCategoryID INT;

DECLARE clone_cur CURSOR LOCAL FAST_FORWARD FOR
SELECT x.CategoryID, x.CompanyID, c.Name, c.IsActive, c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy, c.IsDeleted, c.DeletedAt
FROM #CatCompanyPairs x
INNER JOIN [dbo].[Categories] c ON c.CategoryID = x.CategoryID
WHERE x.rn > 1;

OPEN clone_cur;
FETCH NEXT FROM clone_cur INTO @OldCategoryID, @CompanyID, @Name, @IsActive, @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy, @IsDeleted, @DeletedAt;

WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO [dbo].[Categories]
        (Name, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, DeletedAt, CompanyID)
    VALUES
        (@Name, @IsActive, @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy, @IsDeleted, @DeletedAt, @CompanyID);

    SET @NewCategoryID = SCOPE_IDENTITY();

    UPDATE [dbo].[Products]
    SET CategoryID = @NewCategoryID
    WHERE CategoryID = @OldCategoryID AND CompanyID = @CompanyID;

    FETCH NEXT FROM clone_cur INTO @OldCategoryID, @CompanyID, @Name, @IsActive, @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy, @IsDeleted, @DeletedAt;
END

CLOSE clone_cur;
DEALLOCATE clone_cur;

DROP TABLE #CatCompanyPairs;

-- 3) Unused categories (no products): assign to first non-deleted company
UPDATE [dbo].[Categories]
SET CompanyID = (
    SELECT TOP 1 CompanyID
    FROM [dbo].[Companies]
    WHERE IsDeleted = 0
    ORDER BY CompanyID
)
WHERE CompanyID IS NULL;

-- Fallback if all companies are soft-deleted
UPDATE [dbo].[Categories]
SET CompanyID = (
    SELECT TOP 1 CompanyID
    FROM [dbo].[Companies]
    ORDER BY CompanyID
)
WHERE CompanyID IS NULL;

IF EXISTS (SELECT 1 FROM [dbo].[Categories] WHERE CompanyID IS NULL)
BEGIN
    THROW 50001, 'Cannot backfill Categories.CompanyID because no Companies exist.', 1;
END

-- Integrity check: every product category must match product company
IF EXISTS (
    SELECT 1
    FROM [dbo].[Products] p
    INNER JOIN [dbo].[Categories] c ON c.CategoryID = p.CategoryID
    WHERE c.CompanyID <> p.CompanyID
)
BEGIN
    THROW 50002, 'Category/Company remapping failed: Product.CategoryID does not match Product.CompanyID.', 1;
END
");

            migrationBuilder.AlterColumn<int>(
                name: "CompanyID",
                schema: "dbo",
                table: "Categories",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CompanyID",
                schema: "dbo",
                table: "Categories",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CompanyID_Name",
                schema: "dbo",
                table: "Categories",
                columns: new[] { "CompanyID", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Companies_CompanyID",
                schema: "dbo",
                table: "Categories",
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
                name: "FK_Categories_Companies_CompanyID",
                schema: "dbo",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CompanyID_Name",
                schema: "dbo",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CompanyID",
                schema: "dbo",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CompanyID",
                schema: "dbo",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                schema: "dbo",
                table: "Categories",
                column: "Name",
                unique: true);
        }
    }
}
