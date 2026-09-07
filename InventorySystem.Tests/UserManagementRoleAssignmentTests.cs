using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Constants;
using InventorySystem.Data;
using InventorySystem.DTOs.UserManagement;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class UserManagementRoleAssignmentTests
    {
        private const int AdminRoleId = 1;
        private const int UserRoleId = 2;
        private const int ManagerRoleId = 3;
        private const int AdminUserId = 1;
        private const int ManagerUserId = 2;
        private const int CompanyId = 10;

        [Fact]
        public async Task CreateUserAsync_AdminActor_CanAssignAdminRole()
        {
            await using var context = CreateDb(nameof(CreateUserAsync_AdminActor_CanAssignAdminRole));
            await SeedRolesAndUsersAsync(context);
            var service = CreateService(context);

            var result = await service.CreateUserAsync(new CreateUserDto
            {
                FullName = "New Admin",
                Username = "newadmin",
                Password = "SecurePass1!",
                RoleID = AdminRoleId,
                CompanyIds = new List<int>()
            }, AdminUserId);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task CreateUserAsync_NonAdminActor_CannotAssignAdminRole()
        {
            await using var context = CreateDb(nameof(CreateUserAsync_NonAdminActor_CannotAssignAdminRole));
            await SeedRolesAndUsersAsync(context);
            var service = CreateService(context);

            var result = await service.CreateUserAsync(new CreateUserDto
            {
                FullName = "Escalated Admin",
                Username = "escalated",
                Password = "SecurePass1!",
                RoleID = AdminRoleId,
                CompanyIds = new List<int>()
            }, ManagerUserId);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("Only administrators can assign the Admin role"));
        }

        [Fact]
        public async Task CreateUserAsync_NonAdminActor_CanAssignNonAdminRole()
        {
            await using var context = CreateDb(nameof(CreateUserAsync_NonAdminActor_CanAssignNonAdminRole));
            await SeedRolesAndUsersAsync(context);
            var service = CreateService(context);

            var result = await service.CreateUserAsync(new CreateUserDto
            {
                FullName = "Regular User",
                Username = "regular",
                Password = "SecurePass1!",
                RoleID = UserRoleId,
                CompanyIds = new List<int> { CompanyId }
            }, ManagerUserId);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task UpdateUserAsync_NonAdminActor_CannotSelfPromoteToAdmin()
        {
            await using var context = CreateDb(nameof(UpdateUserAsync_NonAdminActor_CannotSelfPromoteToAdmin));
            await SeedRolesAndUsersAsync(context);
            var service = CreateService(context);

            var result = await service.UpdateUserAsync(new UpdateUserDto
            {
                UserID = ManagerUserId,
                FullName = "Manager",
                Username = "manager",
                RoleID = AdminRoleId,
                IsActive = true,
                CompanyIds = new List<int> { CompanyId }
            }, ManagerUserId);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("Only administrators can assign the Admin role"));
        }

        [Fact]
        public async Task UpdateUserAsync_NonAdminActor_CannotPromoteOtherUserToAdmin()
        {
            await using var context = CreateDb(nameof(UpdateUserAsync_NonAdminActor_CannotPromoteOtherUserToAdmin));
            await SeedRolesAndUsersAsync(context);

            context.Users.Add(new User
            {
                UserID = 3,
                RoleID = UserRoleId,
                FullName = "Target User",
                Username = "target",
                PasswordHash = "x",
                IsActive = true
            });
            await context.SaveChangesAsync();

            var service = CreateService(context);

            var result = await service.UpdateUserAsync(new UpdateUserDto
            {
                UserID = 3,
                FullName = "Target User",
                Username = "target",
                RoleID = AdminRoleId,
                IsActive = true,
                CompanyIds = new List<int> { CompanyId }
            }, ManagerUserId);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("Only administrators can assign the Admin role"));
        }

        [Fact]
        public async Task GetAssignableRolesForActorAsync_Admin_IncludesAdminRole()
        {
            await using var context = CreateDb(nameof(GetAssignableRolesForActorAsync_Admin_IncludesAdminRole));
            await SeedRolesAndUsersAsync(context);
            var service = CreateService(context);

            var roles = await service.GetAssignableRolesForActorAsync(AdminUserId);

            Assert.Contains(roles, r => r.RoleName == RoleNames.Admin);
        }

        [Fact]
        public async Task GetAssignableRolesForActorAsync_NonAdmin_ExcludesAdminRole()
        {
            await using var context = CreateDb(nameof(GetAssignableRolesForActorAsync_NonAdmin_ExcludesAdminRole));
            await SeedRolesAndUsersAsync(context);
            var service = CreateService(context);

            var roles = await service.GetAssignableRolesForActorAsync(ManagerUserId);

            Assert.DoesNotContain(roles, r => r.RoleName == RoleNames.Admin);
            Assert.Contains(roles, r => r.RoleName == RoleNames.User);
        }

        private static ApplicationDbContext CreateDb(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private static UserManagementService CreateService(ApplicationDbContext context)
        {
            var userRepository = new UserRepository(context);
            var passwordPolicyValidator = new PasswordPolicyValidator(
                Microsoft.Extensions.Options.Options.Create(new InventorySystem.Configuration.SecuritySettings()));
            return new UserManagementService(
                userRepository,
                context,
                passwordPolicyValidator,
                NullLogger<UserManagementService>.Instance);
        }

        private static async Task SeedRolesAndUsersAsync(ApplicationDbContext context)
        {
            context.Roles.AddRange(
                new Role { RoleID = AdminRoleId, RoleName = RoleNames.Admin, IsActive = true, IsSystemRole = true },
                new Role { RoleID = UserRoleId, RoleName = RoleNames.User, IsActive = true },
                new Role { RoleID = ManagerRoleId, RoleName = "Manager", IsActive = true });

            context.Users.AddRange(
                new User
                {
                    UserID = AdminUserId,
                    RoleID = AdminRoleId,
                    FullName = "Admin",
                    Username = "admin",
                    PasswordHash = "x",
                    IsActive = true
                },
                new User
                {
                    UserID = ManagerUserId,
                    RoleID = ManagerRoleId,
                    FullName = "Manager",
                    Username = "manager",
                    PasswordHash = "x",
                    IsActive = true
                });

            context.Companies.Add(new Company
            {
                CompanyID = CompanyId,
                CompanyName = "Test Co"
            });

            await context.SaveChangesAsync();
        }
    }
}
