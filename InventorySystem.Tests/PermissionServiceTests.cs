using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using InventorySystem.Constants;
using InventorySystem.Data;
using InventorySystem.DTOs.Roles;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class PermissionServiceTests
    {
        private static ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task HasPermissionAsync_AdminRole_AlwaysBypasses()
        {
            await using var context = CreateContext(nameof(HasPermissionAsync_AdminRole_AlwaysBypasses));
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new PermissionService(context, cache);

            var allowed = await service.HasPermissionAsync(RoleNames.Admin, 1, PageKeys.Products, PermissionAction.Delete);

            Assert.True(allowed);
        }

        [Fact]
        public async Task HasPermissionAsync_CustomRole_RespectsRolePermissions()
        {
            await using var context = CreateContext(nameof(HasPermissionAsync_CustomRole_RespectsRolePermissions));

            var module = new ApplicationModule { ModuleKey = "Inventory", ModuleName = "Inventory", DisplayOrder = 1 };
            context.ApplicationModules.Add(module);
            await context.SaveChangesAsync();

            var page = new ApplicationPage
            {
                ApplicationModuleID = module.ApplicationModuleID,
                PageKey = PageKeys.Products,
                PageName = "Products",
                ControllerName = "Products",
                DefaultActionName = "Index"
            };
            context.ApplicationPages.Add(page);

            var role = new Role { RoleName = "Warehouse", IsActive = true };
            context.Roles.Add(role);
            await context.SaveChangesAsync();

            context.RolePermissions.Add(new RolePermission
            {
                RoleID = role.RoleID,
                ApplicationPageID = page.ApplicationPageID,
                CanView = true,
                CanAdd = false,
                CanEdit = false,
                CanDelete = false
            });

            var user = new User
            {
                RoleID = role.RoleID,
                Username = "wh1",
                PasswordHash = "x",
                FullName = "Warehouse User",
                IsActive = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new PermissionService(context, cache);

            Assert.True(await service.HasPermissionAsync(user.UserID, PageKeys.Products, PermissionAction.View));
            Assert.False(await service.HasPermissionAsync(user.UserID, PageKeys.Products, PermissionAction.Add));
        }

        [Fact]
        public void MapMvcActionToPermission_MapsCrudActions()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new PermissionService(CreateContext(nameof(MapMvcActionToPermission_MapsCrudActions)), cache);

            Assert.Equal(PermissionAction.Add, service.MapMvcActionToPermission("Create"));
            Assert.Equal(PermissionAction.Edit, service.MapMvcActionToPermission("Edit"));
            Assert.Equal(PermissionAction.Delete, service.MapMvcActionToPermission("Delete"));
            Assert.Equal(PermissionAction.View, service.MapMvcActionToPermission("Index"));
        }
    }

    public class RoleManagementServiceTests
    {
        private static ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task CreateRoleAsync_PersistsSelectedPermissions()
        {
            await using var context = CreateContext(nameof(CreateRoleAsync_PersistsSelectedPermissions));
            var module = new ApplicationModule { ModuleKey = "Administration", ModuleName = "Administration", DisplayOrder = 1 };
            context.ApplicationModules.Add(module);
            await context.SaveChangesAsync();

            var usersPage = new ApplicationPage
            {
                ApplicationModuleID = module.ApplicationModuleID,
                PageKey = PageKeys.Users,
                PageName = "Users",
                ControllerName = "UserManagement",
                DefaultActionName = "Index"
            };
            context.ApplicationPages.Add(usersPage);
            await context.SaveChangesAsync();

            var roleRepo = new RoleRepository(context);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var permissionService = new PermissionService(context, cache);
            var service = new RoleManagementService(roleRepo, permissionService, Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleManagementService>.Instance);

            var result = await service.CreateRoleAsync(new CreateRoleDto
            {
                RoleName = "Salesperson",
                RoleDescription = "Sales team",
                IsActive = true,
                Permissions =
                {
                    new RolePermissionInputDto
                    {
                        ApplicationPageID = usersPage.ApplicationPageID,
                        CanView = true,
                        CanAdd = true,
                        CanEdit = false,
                        CanDelete = false
                    }
                }
            }, userId: 1);

            Assert.True(result.Success);

            var saved = await context.RolePermissions
                .Include(rp => rp.Role)
                .FirstOrDefaultAsync(rp => rp.Role.RoleName == "Salesperson");

            Assert.NotNull(saved);
            Assert.True(saved!.CanView);
            Assert.True(saved.CanAdd);
            Assert.False(saved.CanDelete);
        }

        [Fact]
        public async Task SoftDeleteRoleAsync_BlocksSystemRole()
        {
            await using var context = CreateContext(nameof(SoftDeleteRoleAsync_BlocksSystemRole));
            var role = new Role { RoleName = RoleNames.Admin, IsSystemRole = true, IsActive = true };
            context.Roles.Add(role);
            await context.SaveChangesAsync();

            var roleRepo = new RoleRepository(context);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var permissionService = new PermissionService(context, cache);
            var service = new RoleManagementService(roleRepo, permissionService, Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleManagementService>.Instance);

            var result = await service.SoftDeleteRoleAsync(role.RoleID, 1);

            Assert.False(result.Success);
        }
    }
}
