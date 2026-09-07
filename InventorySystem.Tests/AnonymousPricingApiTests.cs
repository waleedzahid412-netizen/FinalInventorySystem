using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    /// <summary>
    /// Guards for unauthenticated pricing APIs and cross-tenant product scope.
    /// </summary>
    public class AnonymousPricingApiTests
    {
        private static ApplicationDbContext CreateDb(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private static async Task SeedProductsAsync(ApplicationDbContext db)
        {
            db.Roles.Add(new Role { RoleID = 1, RoleName = "Booker", IsActive = true });
            db.Users.Add(new User { UserID = 2, RoleID = 1, FullName = "Booker User", Username = "booker", PasswordHash = "x", IsActive = true });
            db.Companies.AddRange(
                new Company { CompanyID = 1, CompanyName = "PepsiCo" },
                new Company { CompanyID = 2, CompanyName = "Colgate" });
            db.Units.Add(new Unit { UnitID = 1, UnitName = "Piece" });
            db.Categories.AddRange(
                new Category { CategoryID = 1, CompanyID = 1, Name = "Pepsi Cats" },
                new Category { CategoryID = 2, CompanyID = 2, Name = "Colgate Cats" });
            db.Products.AddRange(
                new Product { ProductID = 1, ProductName = "Pepsi 1L", SKU = "P1", CompanyID = 1, CategoryID = 1, BaseUnitID = 1, BaseSellingPrice = 50m, IsActive = true },
                new Product { ProductID = 2, ProductName = "Colgate Paste", SKU = "C1", CompanyID = 2, CategoryID = 2, BaseUnitID = 1, BaseSellingPrice = 30m, IsActive = true });
            db.UserCompanies.Add(new UserCompany { UserID = 2, CompanyID = 1 });
            await db.SaveChangesAsync();
        }

        private static CompanyContext CreateCompanyContext(ApplicationDbContext db, HttpContext httpContext)
        {
            var accessor = new HttpContextAccessor { HttpContext = httpContext };
            var companyAccess = new UserCompanyAccessService(accessor, db);
            return new CompanyContext(accessor, db, companyAccess);
        }

        private static HttpContext CreateHttpContextWithCompanyCookie(string cookieValue, int? userId = null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Cookie"] = $"{CompanyContext.CookieName}={cookieValue}";

            if (userId.HasValue)
            {
                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
                }, "TestAuth");
                httpContext.User = new ClaimsPrincipal(identity);
            }

            return httpContext;
        }

        [Fact]
        public async Task CompanyContext_RejectsSpecificCompanyCookie_WhenUserNotAuthenticated()
        {
            await using var db = CreateDb(nameof(CompanyContext_RejectsSpecificCompanyCookie_WhenUserNotAuthenticated));
            await SeedProductsAsync(db);

            var httpContext = CreateHttpContextWithCompanyCookie("1");
            var companyContext = CreateCompanyContext(db, httpContext);

            var resolved = await companyContext.TryResolveAsync();

            Assert.False(resolved);
            Assert.False(companyContext.HasResolvedScope);
        }

        [Fact]
        public async Task CompanyContext_AllowsSpecificCompanyCookie_WhenUserHasAccess()
        {
            await using var db = CreateDb(nameof(CompanyContext_AllowsSpecificCompanyCookie_WhenUserHasAccess));
            await SeedProductsAsync(db);

            var httpContext = CreateHttpContextWithCompanyCookie("1", userId: 2);
            var companyContext = CreateCompanyContext(db, httpContext);

            var resolved = await companyContext.TryResolveAsync();

            Assert.True(resolved);
            Assert.True(companyContext.HasCompany);
            Assert.Equal(1, companyContext.CompanyID);
        }

        [Fact]
        public async Task CompanyContext_RejectsSpecificCompanyCookie_WhenUserLacksAccess()
        {
            await using var db = CreateDb(nameof(CompanyContext_RejectsSpecificCompanyCookie_WhenUserLacksAccess));
            await SeedProductsAsync(db);

            var httpContext = CreateHttpContextWithCompanyCookie("2", userId: 2);
            var companyContext = CreateCompanyContext(db, httpContext);

            var resolved = await companyContext.TryResolveAsync();

            Assert.False(resolved);
            Assert.False(companyContext.HasResolvedScope);
        }

        [Fact]
        public async Task ProductScopeHelper_ReturnsTrue_ForSameCompanyProduct()
        {
            await using var db = CreateDb(nameof(ProductScopeHelper_ReturnsTrue_ForSameCompanyProduct));
            await SeedProductsAsync(db);

            var scope = new FakeCompanyContext(1, "PepsiCo");

            var matches = await ProductScopeHelper.ProductMatchesScopeAsync(db, scope, productId: 1);

            Assert.True(matches);
        }

        [Fact]
        public async Task ProductScopeHelper_ReturnsFalse_ForCrossCompanyProduct()
        {
            await using var db = CreateDb(nameof(ProductScopeHelper_ReturnsFalse_ForCrossCompanyProduct));
            await SeedProductsAsync(db);

            var scope = new FakeCompanyContext(1, "PepsiCo");

            var matches = await ProductScopeHelper.ProductMatchesScopeAsync(db, scope, productId: 2);

            Assert.False(matches);
        }

        [Fact]
        public async Task ProductScopeHelper_ReturnsTrue_InAllCompaniesMode_WhenProductExists()
        {
            await using var db = CreateDb(nameof(ProductScopeHelper_ReturnsTrue_InAllCompaniesMode_WhenProductExists));
            await SeedProductsAsync(db);

            var scope = FakeCompanyContext.AllCompanies();

            var matches = await ProductScopeHelper.ProductMatchesScopeAsync(db, scope, productId: 2);

            Assert.True(matches);
        }
    }
}
