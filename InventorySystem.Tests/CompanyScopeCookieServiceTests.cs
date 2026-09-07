using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models.Entities;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class CompanyScopeCookieServiceTests
    {
        private static ApplicationDbContext CreateDb(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task GetDefaultCompanyId_Admin_ReturnsFirstCompanyByName()
        {
            await using var db = CreateDb(nameof(GetDefaultCompanyId_Admin_ReturnsFirstCompanyByName));
            db.Roles.Add(new Role { RoleID = 1, RoleName = "Admin", IsActive = true });
            db.Users.Add(new User { UserID = 1, RoleID = 1, FullName = "Admin", Username = "admin", PasswordHash = "x", IsActive = true });
            db.Companies.AddRange(
                new Company { CompanyID = 2, CompanyName = "Zeta Co" },
                new Company { CompanyID = 1, CompanyName = "Alpha Co" });
            await db.SaveChangesAsync();

            var access = new UserCompanyAccessService(new Microsoft.AspNetCore.Http.HttpContextAccessor(), db);
            var service = new CompanyScopeCookieService(db, access, FakeCompanyContext.Unscoped());

            var defaultId = await service.GetDefaultCompanyIdForUserAsync(1);

            Assert.Equal(1, defaultId);
        }

        [Fact]
        public async Task GetDefaultCompanyId_RestrictedUser_ReturnsFirstAllowedCompany()
        {
            await using var db = CreateDb(nameof(GetDefaultCompanyId_RestrictedUser_ReturnsFirstAllowedCompany));
            db.Roles.Add(new Role { RoleID = 2, RoleName = "Sales", IsActive = true });
            db.Users.Add(new User { UserID = 5, RoleID = 2, FullName = "Sales User", Username = "sales", PasswordHash = "x", IsActive = true });
            db.Companies.AddRange(
                new Company { CompanyID = 1, CompanyName = "Alpha Co" },
                new Company { CompanyID = 2, CompanyName = "Beta Co" },
                new Company { CompanyID = 3, CompanyName = "Gamma Co" });
            db.UserCompanies.AddRange(
                new UserCompany { UserID = 5, CompanyID = 3 },
                new UserCompany { UserID = 5, CompanyID = 2 });
            await db.SaveChangesAsync();

            var access = new UserCompanyAccessService(new Microsoft.AspNetCore.Http.HttpContextAccessor(), db);
            var service = new CompanyScopeCookieService(db, access, FakeCompanyContext.Unscoped());

            var defaultId = await service.GetDefaultCompanyIdForUserAsync(5);

            Assert.Equal(2, defaultId);
        }

        [Fact]
        public async Task GetDefaultCompanyId_RestrictedUserWithNoCompanies_ReturnsNull()
        {
            await using var db = CreateDb(nameof(GetDefaultCompanyId_RestrictedUserWithNoCompanies_ReturnsNull));
            db.Roles.Add(new Role { RoleID = 2, RoleName = "Sales", IsActive = true });
            db.Users.Add(new User { UserID = 5, RoleID = 2, FullName = "Sales User", Username = "sales", PasswordHash = "x", IsActive = true });
            db.Companies.Add(new Company { CompanyID = 1, CompanyName = "Alpha Co" });
            await db.SaveChangesAsync();

            var access = new UserCompanyAccessService(new Microsoft.AspNetCore.Http.HttpContextAccessor(), db);
            var service = new CompanyScopeCookieService(db, access, FakeCompanyContext.Unscoped());

            var defaultId = await service.GetDefaultCompanyIdForUserAsync(5);

            Assert.Null(defaultId);
        }
    }
}
