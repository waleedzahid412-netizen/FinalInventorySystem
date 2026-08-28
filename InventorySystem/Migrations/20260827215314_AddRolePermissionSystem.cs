using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddRolePermissionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystemRole",
                schema: "dbo",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RoleDescription",
                schema: "dbo",
                table: "Roles",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationModules",
                schema: "dbo",
                columns: table => new
                {
                    ApplicationModuleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuleKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationModules", x => x.ApplicationModuleID);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationPages",
                schema: "dbo",
                columns: table => new
                {
                    ApplicationPageID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationModuleID = table.Column<int>(type: "int", nullable: false),
                    PageKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PageName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ControllerName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DefaultActionName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationPages", x => x.ApplicationPageID);
                    table.ForeignKey(
                        name: "FK_ApplicationPages_ApplicationModules_ApplicationModuleID",
                        column: x => x.ApplicationModuleID,
                        principalSchema: "dbo",
                        principalTable: "ApplicationModules",
                        principalColumn: "ApplicationModuleID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "dbo",
                columns: table => new
                {
                    RolePermissionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleID = table.Column<int>(type: "int", nullable: false),
                    ApplicationPageID = table.Column<int>(type: "int", nullable: false),
                    CanView = table.Column<bool>(type: "bit", nullable: false),
                    CanAdd = table.Column<bool>(type: "bit", nullable: false),
                    CanEdit = table.Column<bool>(type: "bit", nullable: false),
                    CanDelete = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.RolePermissionID);
                    table.ForeignKey(
                        name: "FK_RolePermissions_ApplicationPages_ApplicationPageID",
                        column: x => x.ApplicationPageID,
                        principalSchema: "dbo",
                        principalTable: "ApplicationPages",
                        principalColumn: "ApplicationPageID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleID",
                        column: x => x.RoleID,
                        principalSchema: "dbo",
                        principalTable: "Roles",
                        principalColumn: "RoleID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationModules_ModuleKey",
                schema: "dbo",
                table: "ApplicationModules",
                column: "ModuleKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPages_ApplicationModuleID",
                schema: "dbo",
                table: "ApplicationPages",
                column: "ApplicationModuleID");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPages_PageKey",
                schema: "dbo",
                table: "ApplicationPages",
                column: "PageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_ApplicationPageID",
                schema: "dbo",
                table: "RolePermissions",
                column: "ApplicationPageID");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleID_ApplicationPageID",
                schema: "dbo",
                table: "RolePermissions",
                columns: new[] { "RoleID", "ApplicationPageID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ApplicationPages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ApplicationModules",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "IsSystemRole",
                schema: "dbo",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "RoleDescription",
                schema: "dbo",
                table: "Roles");
        }
    }
}
