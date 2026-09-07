using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Controllers;
using InventorySystem.Data;
using InventorySystem.DTOs.Analytics;
using InventorySystem.Models.Entities;
using InventorySystem.Services.Implementations;
using InventorySystem.Services.Interfaces;
using Xunit;

namespace InventorySystem.Tests
{
    public class AnalyticsScopeTests
    {
        private static ApplicationDbContext CreateDb(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private static async Task SeedAsync(ApplicationDbContext db)
        {
            db.Roles.AddRange(
                new Role { RoleID = 1, RoleName = "Admin", IsActive = true },
                new Role { RoleID = 2, RoleName = "Booker", IsActive = true });
            db.Users.AddRange(
                new User { UserID = 1, RoleID = 1, FullName = "Admin", Username = "admin", PasswordHash = "x", IsActive = true },
                new User { UserID = 2, RoleID = 2, FullName = "Booker", Username = "booker", PasswordHash = "x", IsActive = true });
            db.Companies.AddRange(
                new Company { CompanyID = 1, CompanyName = "PepsiCo" },
                new Company { CompanyID = 2, CompanyName = "Colgate" });
            db.UserCompanies.Add(new UserCompany { UserID = 2, CompanyID = 1 });
            await db.SaveChangesAsync();
        }

        private static HttpContext CreateHttpContext(int userId, string? companyCookie = null)
        {
            var httpContext = new DefaultHttpContext();
            if (!string.IsNullOrEmpty(companyCookie))
            {
                httpContext.Request.Headers["Cookie"] = $"{CompanyContext.CookieName}={companyCookie}";
            }

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            }, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
            return httpContext;
        }

        private static async Task<IActionResult?> InvokeApplyAmbientCompanyScopeAsync(
            ApplicationDbContext db,
            HttpContext httpContext,
            AnalyticsFilterDto filter)
        {
            var accessor = new HttpContextAccessor { HttpContext = httpContext };
            var companyAccess = new UserCompanyAccessService(accessor, db);
            var companyContext = new CompanyContext(accessor, db, companyAccess);
            var controller = new AnalyticsController(
                null!,
                null!,
                null!,
                companyContext,
                companyAccess,
                NullLogger<AnalyticsController>.Instance,
                null!);
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var method = typeof(AnalyticsController).GetMethod(
                "ApplyAmbientCompanyScopeAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var task = (Task<IActionResult?>)method.Invoke(controller, new object[] { filter, CancellationToken.None })!;
            return await task;
        }

        [Fact]
        public async Task ApplyAmbientCompanyScope_RejectsForgedCompanyId_WhenNonAdminHasNoResolvedScope()
        {
            await using var db = CreateDb(nameof(ApplyAmbientCompanyScope_RejectsForgedCompanyId_WhenNonAdminHasNoResolvedScope));
            await SeedAsync(db);

            var filter = new AnalyticsFilterDto { CompanyID = 2 };
            var httpContext = CreateHttpContext(userId: 2);

            var result = await InvokeApplyAmbientCompanyScopeAsync(db, httpContext, filter);

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
            Assert.Equal(2, filter.CompanyID);
        }

        [Fact]
        public async Task ApplyAmbientCompanyScope_PinsCompanyId_FromCookie_ForNonAdmin()
        {
            await using var db = CreateDb(nameof(ApplyAmbientCompanyScope_PinsCompanyId_FromCookie_ForNonAdmin));
            await SeedAsync(db);

            var filter = new AnalyticsFilterDto { CompanyID = 2 };
            var httpContext = CreateHttpContext(userId: 2, companyCookie: "1");

            var result = await InvokeApplyAmbientCompanyScopeAsync(db, httpContext, filter);

            Assert.Null(result);
            Assert.Equal(1, filter.CompanyID);
        }

        [Fact]
        public async Task ApplyAmbientCompanyScope_AllowsUnscopedFilter_ForAdmin()
        {
            await using var db = CreateDb(nameof(ApplyAmbientCompanyScope_AllowsUnscopedFilter_ForAdmin));
            await SeedAsync(db);

            var filter = new AnalyticsFilterDto { CompanyID = 2 };
            var httpContext = CreateHttpContext(userId: 1);

            var result = await InvokeApplyAmbientCompanyScopeAsync(db, httpContext, filter);

            Assert.Null(result);
            Assert.Equal(2, filter.CompanyID);
        }

        [Fact]
        public async Task ApplyAmbientCompanyScope_ForcesNullCompanyId_InAllCompaniesMode()
        {
            await using var db = CreateDb(nameof(ApplyAmbientCompanyScope_ForcesNullCompanyId_InAllCompaniesMode));
            await SeedAsync(db);

            var filter = new AnalyticsFilterDto { CompanyID = 2 };
            var httpContext = CreateHttpContext(userId: 1, companyCookie: CompanyContext.AllCompaniesCookieValue);

            var result = await InvokeApplyAmbientCompanyScopeAsync(db, httpContext, filter);

            Assert.Null(result);
            Assert.Null(filter.CompanyID);
        }
    }
}
