using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using InventorySystem.Configuration;
using InventorySystem.Data;
using InventorySystem.DTOs;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class AuthLockoutTests
    {
        private const string ValidPassword = "SecurePass1!";
        private const int AdminRoleId = 1;
        private const int AdminUserId = 1;

        [Fact]
        public async Task LoginAsync_LocksAccountAfterMaxFailedAttempts()
        {
            await using var context = CreateDb(nameof(LoginAsync_LocksAccountAfterMaxFailedAttempts));
            await SeedAdminUserAsync(context);
            var service = CreateAuthService(context);

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var result = await service.LoginAsync(new LoginRequestDTO
                {
                    Username = "admin",
                    Password = "WrongPassword1!"
                });

                Assert.False(result.Success);
            }

            var lockedResult = await service.LoginAsync(new LoginRequestDTO
            {
                Username = "admin",
                Password = ValidPassword
            });

            Assert.False(lockedResult.Success);
            Assert.Equal(UserFacingErrorMessages.AccountLocked, lockedResult.Message);
        }

        [Fact]
        public async Task LoginAsync_SuccessfulLoginResetsFailedAttempts()
        {
            await using var context = CreateDb(nameof(LoginAsync_SuccessfulLoginResetsFailedAttempts));
            await SeedAdminUserAsync(context);
            var service = CreateAuthService(context);

            await service.LoginAsync(new LoginRequestDTO
            {
                Username = "admin",
                Password = "WrongPassword1!"
            });

            var success = await service.LoginAsync(new LoginRequestDTO
            {
                Username = "admin",
                Password = ValidPassword
            });

            Assert.True(success.Success);

            var user = await context.Users.SingleAsync(u => u.UserID == AdminUserId);
            Assert.Equal(0, user.AccessFailedCount);
            Assert.Null(user.LockoutEnd);
        }

        [Fact]
        public async Task LoginAsync_UnknownUser_ReturnsGenericMessage()
        {
            await using var context = CreateDb(nameof(LoginAsync_UnknownUser_ReturnsGenericMessage));
            await SeedAdminUserAsync(context);
            var service = CreateAuthService(context);

            var result = await service.LoginAsync(new LoginRequestDTO
            {
                Username = "missing",
                Password = ValidPassword
            });

            Assert.False(result.Success);
            Assert.Equal(UserFacingErrorMessages.InvalidCredentials, result.Message);
        }

        private static ApplicationDbContext CreateDb(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private static AuthService CreateAuthService(ApplicationDbContext context)
        {
            var userRepository = new UserRepository(context);
            var jwtSettings = Options.Create(new JwtSettings
            {
                SecretKey = "WIMS_SuperSecretKeyForJWTTokenGeneration_MustBeMinimum32Chars!",
                Issuer = "WIMS",
                Audience = "WIMS_Users",
                DurationInMinutes = 60
            });
            var securitySettings = Options.Create(new SecuritySettings
            {
                AccountLockout = new AccountLockoutOptions
                {
                    MaxFailedAttempts = 5,
                    LockoutMinutes = 15
                }
            });

            return new AuthService(
                userRepository,
                jwtSettings,
                securitySettings,
                NullLogger<AuthService>.Instance);
        }

        private static async Task SeedAdminUserAsync(ApplicationDbContext context)
        {
            context.Roles.Add(new Role
            {
                RoleID = AdminRoleId,
                RoleName = "Admin",
                IsActive = true,
                IsSystemRole = true
            });

            context.Users.Add(new User
            {
                UserID = AdminUserId,
                RoleID = AdminRoleId,
                FullName = "Admin",
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(ValidPassword),
                IsActive = true
            });

            await context.SaveChangesAsync();
        }
    }
}
