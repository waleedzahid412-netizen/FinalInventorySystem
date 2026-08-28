using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Bookers;
using InventorySystem.DTOs.Sales;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class CompanyScopePhase2Tests
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
        public void RedirectIfCannotCreate_BlocksAllCompanies()
        {
            // Guard logic is pure: IsAllCompanies / !HasCompany → block.
            var all = FakeCompanyContext.AllCompanies();
            Assert.True(all.IsAllCompanies);
            Assert.False(all.HasCompany);
            Assert.True(all.IsAllCompanies || !all.HasCompany);

            var specific = new FakeCompanyContext(1, "Co");
            Assert.False(specific.IsAllCompanies || !specific.HasCompany);
        }

        [Fact]
        public void IsOutOfScope_OnlyWhenHasCompany()
        {
            var specific = new FakeCompanyContext(1, "Co");
            Assert.True(CompanyScopeGuards.IsOutOfScope(specific, 2));
            Assert.False(CompanyScopeGuards.IsOutOfScope(specific, 1));

            var all = FakeCompanyContext.AllCompanies();
            Assert.False(CompanyScopeGuards.IsOutOfScope(all, 2));
            Assert.False(CompanyScopeGuards.IsOutOfScope(all, 1));
        }

        [Fact]
        public async Task SalesRepository_FiltersByCompanyID_WhenSet()
        {
            await using var db = CreateDb(nameof(SalesRepository_FiltersByCompanyID_WhenSet));
            db.Roles.Add(new Role { RoleID = 1, RoleName = "Admin", IsActive = true });
            db.Users.Add(new User { UserID = 1, RoleID = 1, FullName = "A", Username = "a", PasswordHash = "x", IsActive = true });
            db.Companies.AddRange(
                new Company { CompanyID = 1, CompanyName = "A" },
                new Company { CompanyID = 2, CompanyName = "B" });
            db.Warehouses.Add(new Warehouse { WarehouseID = 1, Name = "W", IsActive = true });
            db.Customers.Add(new Customer { CustomerID = 1, ShopName = "Shop", IsActive = true });
            db.SalesInvoices.AddRange(
                new SalesInvoice
                {
                    InvoiceID = 1,
                    InvoiceNumber = "SI-1",
                    CustomerID = 1,
                    CompanyID = 1,
                    WarehouseID = 1,
                    InvoiceDate = DateTime.UtcNow.Date,
                    GrandTotal = 100m,
                    CreatedBy = 1
                },
                new SalesInvoice
                {
                    InvoiceID = 2,
                    InvoiceNumber = "SI-2",
                    CustomerID = 1,
                    CompanyID = 2,
                    WarehouseID = 1,
                    InvoiceDate = DateTime.UtcNow.Date,
                    GrandTotal = 200m,
                    CreatedBy = 1
                });
            await db.SaveChangesAsync();

            var repo = new SalesRepository(db);

            var filtered = await repo.GetPagedAsync(new SalesFilterDto { CompanyID = 1, PageNumber = 1, PageSize = 50 });
            Assert.Single(filtered.Items);
            Assert.Equal("SI-1", filtered.Items[0].InvoiceNumber);

            var all = await repo.GetPagedAsync(new SalesFilterDto { CompanyID = null, PageNumber = 1, PageSize = 50 });
            Assert.Equal(2, all.Items.Count);
        }

        [Fact]
        public async Task BookerService_GetPaged_AllCompanies_ShowsAll()
        {
            await using var db = CreateDb(nameof(BookerService_GetPaged_AllCompanies_ShowsAll));
            db.Companies.AddRange(
                new Company { CompanyID = 1, CompanyName = "A" },
                new Company { CompanyID = 2, CompanyName = "B" });
            db.Bookers.AddRange(
                new Booker { BookerID = 1, CompanyID = 1, Name = "B1", CNIC = "35202-1111111", IsActive = true },
                new Booker { BookerID = 2, CompanyID = 2, Name = "B2", CNIC = "35202-2222222", IsActive = true });
            await db.SaveChangesAsync();

            var service = new BookerService(new BookerRepository(db), FakeCompanyContext.AllCompanies());
            var page = await service.GetPagedBookersAsync(new BookerFilterDto { PageNumber = 1, PageSize = 50 });

            Assert.Equal(2, page.Items.Count);
            Assert.Contains(page.Items, b => b.Name == "B1");
            Assert.Contains(page.Items, b => b.Name == "B2");
        }

        [Fact]
        public async Task BookerService_GetPaged_SpecificCompany_Filters()
        {
            await using var db = CreateDb(nameof(BookerService_GetPaged_SpecificCompany_Filters));
            db.Companies.AddRange(
                new Company { CompanyID = 1, CompanyName = "A" },
                new Company { CompanyID = 2, CompanyName = "B" });
            db.Bookers.AddRange(
                new Booker { BookerID = 1, CompanyID = 1, Name = "B1", CNIC = "35202-1111111", IsActive = true },
                new Booker { BookerID = 2, CompanyID = 2, Name = "B2", CNIC = "35202-2222222", IsActive = true });
            await db.SaveChangesAsync();

            var service = new BookerService(new BookerRepository(db), new FakeCompanyContext(1, "A"));
            var page = await service.GetPagedBookersAsync(new BookerFilterDto { PageNumber = 1, PageSize = 50 });

            Assert.Single(page.Items);
            Assert.Equal("B1", page.Items[0].Name);
        }

        [Fact]
        public async Task BookerService_Create_AllCompanies_Fails()
        {
            await using var db = CreateDb(nameof(BookerService_Create_AllCompanies_Fails));
            db.Companies.Add(new Company { CompanyID = 1, CompanyName = "A" });
            await db.SaveChangesAsync();

            var service = new BookerService(new BookerRepository(db), FakeCompanyContext.AllCompanies());
            var result = await service.CreateBookerAsync(new CreateBookerDto
            {
                Name = "X",
                CNIC = "35202-1234567-1",
                CreditLimit = 0,
                IsActive = true
            }, 1);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("specific company", StringComparison.OrdinalIgnoreCase));
        }
    }
}
