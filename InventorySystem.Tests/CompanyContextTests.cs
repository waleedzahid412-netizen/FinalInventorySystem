using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using InventorySystem.Data;
using InventorySystem.Filters;
using InventorySystem.Models.Entities;
using InventorySystem.Services.Implementations;
using InventorySystem.Services.Interfaces;
using Xunit;

namespace InventorySystem.Tests
{
    public class CompanyContextTests
    {
        private static ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private static (CompanyContext Context, DefaultHttpContext Http, ApplicationDbContext Db) CreateSut(
            string dbName,
            string? cookieValue = null)
        {
            var db = CreateContext(dbName);
            var http = new DefaultHttpContext();
            if (cookieValue != null)
            {
                http.Request.Headers.Cookie = $"{CompanyContext.CookieName}={cookieValue}";
            }

            var accessor = new HttpContextAccessor { HttpContext = http };
            var access = new FakeUserCompanyAccessService(userId: 1, unrestricted: true);
            var sut = new CompanyContext(accessor, db, access);
            return (sut, http, db);
        }

        [Fact]
        public async Task TryResolveAsync_NoCookie_ReturnsFalse()
        {
            var (sut, _, _) = CreateSut(nameof(TryResolveAsync_NoCookie_ReturnsFalse));

            Assert.False(await sut.TryResolveAsync());
            Assert.False(sut.HasCompany);
            Assert.False(sut.IsAllCompanies);
            Assert.False(sut.HasResolvedScope);
            Assert.Throws<InvalidOperationException>(() => _ = sut.CompanyID);
        }

        [Fact]
        public async Task TryResolveAsync_InvalidCookie_ReturnsFalse()
        {
            var (sut, _, _) = CreateSut(nameof(TryResolveAsync_InvalidCookie_ReturnsFalse), "not-a-number");

            Assert.False(await sut.TryResolveAsync());
            Assert.False(sut.HasCompany);
            Assert.False(sut.HasResolvedScope);
        }

        [Fact]
        public async Task TryResolveAsync_AllCompaniesSentinel_ResolvesWithoutCompany()
        {
            var (sut, http, _) = CreateSut(
                nameof(TryResolveAsync_AllCompaniesSentinel_ResolvesWithoutCompany),
                CompanyContext.AllCompaniesCookieValue);

            Assert.True(await sut.TryResolveAsync());
            Assert.True(sut.HasResolvedScope);
            Assert.True(sut.IsAllCompanies);
            Assert.False(sut.HasCompany);
            Assert.Throws<InvalidOperationException>(() => _ = sut.CompanyID);
            Assert.True(http.Items.ContainsKey("InventorySystem.CompanyContext.Resolved"));
        }

        [Fact]
        public async Task TryResolveAsync_NonexistentCompany_ReturnsFalse()
        {
            var (sut, _, _) = CreateSut(nameof(TryResolveAsync_NonexistentCompany_ReturnsFalse), "999");

            Assert.False(await sut.TryResolveAsync());
            Assert.False(sut.HasCompany);
        }

        [Fact]
        public async Task TryResolveAsync_DeletedCompany_ReturnsFalse()
        {
            var (sut, _, db) = CreateSut(nameof(TryResolveAsync_DeletedCompany_ReturnsFalse), "1");
            db.Companies.Add(new Company
            {
                CompanyID = 1,
                CompanyName = "Gone Co",
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            Assert.False(await sut.TryResolveAsync());
            Assert.False(sut.HasCompany);
        }

        [Fact]
        public async Task TryResolveAsync_ValidCompany_ResolvesOncePerRequest()
        {
            var (sut, http, db) = CreateSut(nameof(TryResolveAsync_ValidCompany_ResolvesOncePerRequest), "5");
            db.Companies.Add(new Company
            {
                CompanyID = 5,
                CompanyName = "Pepsi",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            Assert.True(await sut.TryResolveAsync());
            Assert.True(sut.HasCompany);
            Assert.False(sut.IsAllCompanies);
            Assert.True(sut.HasResolvedScope);
            Assert.Equal(5, sut.CompanyID);
            Assert.Equal("Pepsi", sut.CompanyName);

            Assert.True(await sut.TryResolveAsync());
            Assert.True(http.Items.ContainsKey("InventorySystem.CompanyContext.Resolved"));
        }

        [Fact]
        public async Task RequireCompanyScope_RedirectsWhenUnscoped()
        {
            var services = new ServiceCollection();
            var db = CreateContext(nameof(RequireCompanyScope_RedirectsWhenUnscoped));
            var http = new DefaultHttpContext();
            http.Request.Path = "/Bookers";
            http.Request.QueryString = QueryString.Empty;

            var accessor = new HttpContextAccessor { HttpContext = http };
            services.AddSingleton<IHttpContextAccessor>(accessor);
            services.AddSingleton(db);
            services.AddSingleton<IUserCompanyAccessService>(new FakeUserCompanyAccessService(userId: 1));
            services.AddScoped<ICompanyContext, CompanyContext>();
            http.RequestServices = services.BuildServiceProvider();

            var filter = new RequireCompanyScopeAttribute();
            var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
            var executed = false;
            var executingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>(),
                controller: new object());

            await filter.OnActionExecutionAsync(executingContext, () =>
            {
                executed = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
            });

            Assert.False(executed);
            var redirect = Assert.IsType<RedirectToActionResult>(executingContext.Result);
            Assert.Equal("Select", redirect.ActionName);
            Assert.Equal("CompanyScope", redirect.ControllerName);
            Assert.Equal("/Bookers", redirect.RouteValues?["returnUrl"]);
        }

        [Fact]
        public async Task RequireCompanyScope_ContinuesWhenScoped()
        {
            var services = new ServiceCollection();
            var db = CreateContext(nameof(RequireCompanyScope_ContinuesWhenScoped));
            db.Companies.Add(new Company
            {
                CompanyID = 3,
                CompanyName = "Colgate",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var http = new DefaultHttpContext();
            http.Request.Path = "/Products";
            http.Request.Headers.Cookie = $"{CompanyContext.CookieName}=3";

            var accessor = new HttpContextAccessor { HttpContext = http };
            services.AddSingleton<IHttpContextAccessor>(accessor);
            services.AddSingleton(db);
            services.AddSingleton<IUserCompanyAccessService>(new FakeUserCompanyAccessService(userId: 1));
            services.AddScoped<ICompanyContext, CompanyContext>();
            http.RequestServices = services.BuildServiceProvider();

            var filter = new RequireCompanyScopeAttribute();
            var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
            var executed = false;
            var executingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>(),
                controller: new object());

            await filter.OnActionExecutionAsync(executingContext, () =>
            {
                executed = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
            });

            Assert.True(executed);
            Assert.Null(executingContext.Result);
        }

        [Fact]
        public async Task RequireCompanyScope_ContinuesWhenAllCompanies()
        {
            var services = new ServiceCollection();
            var db = CreateContext(nameof(RequireCompanyScope_ContinuesWhenAllCompanies));
            var http = new DefaultHttpContext();
            http.Request.Path = "/Sales";
            http.Request.Headers.Cookie = $"{CompanyContext.CookieName}={CompanyContext.AllCompaniesCookieValue}";

            var accessor = new HttpContextAccessor { HttpContext = http };
            services.AddSingleton<IHttpContextAccessor>(accessor);
            services.AddSingleton(db);
            services.AddSingleton<IUserCompanyAccessService>(new FakeUserCompanyAccessService(userId: 1));
            services.AddScoped<ICompanyContext, CompanyContext>();
            http.RequestServices = services.BuildServiceProvider();

            var filter = new RequireCompanyScopeAttribute();
            var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
            var executed = false;
            var executingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>(),
                controller: new object());

            await filter.OnActionExecutionAsync(executingContext, () =>
            {
                executed = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
            });

            Assert.True(executed);
            Assert.Null(executingContext.Result);
        }
    }
}
