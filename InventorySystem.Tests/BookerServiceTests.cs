using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Bookers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using InventorySystem.Services.Interfaces;
using Xunit;

namespace InventorySystem.Tests
{
    public class BookerServiceTests
    {
        private static ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private static async Task<(ApplicationDbContext Db, Company CoA, Company CoB, User User)> SeedBaseAsync(string dbName)
        {
            var db = CreateContext(dbName);
            db.Roles.Add(new Role { RoleID = 1, RoleName = "Admin", IsActive = true });
            var user = new User { UserID = 1, RoleID = 1, FullName = "Admin", Username = "admin", PasswordHash = "x", IsActive = true };
            db.Users.Add(user);
            var coA = new Company { CompanyID = 1, CompanyName = "Company A" };
            var coB = new Company { CompanyID = 2, CompanyName = "Company B" };
            db.Companies.AddRange(coA, coB);
            await db.SaveChangesAsync();
            return (db, coA, coB, user);
        }

        private static BookerService CreateService(ApplicationDbContext db, ICompanyContext scope) =>
            new BookerService(new BookerRepository(db), scope);

        private static CreateBookerDto ValidDto(string name = "Nadeem", string cnic = "35202-1234567-1") =>
            new CreateBookerDto
            {
                Name = name,
                CNIC = cnic,
                Phone = "03001234567",
                Address = "Lahore",
                CreditLimit = 100000m,
                IsActive = true
            };

        [Fact]
        public async Task Create_Succeeds_WithScopedCompany()
        {
            var (db, coA, _, user) = await SeedBaseAsync(nameof(Create_Succeeds_WithScopedCompany));
            var service = CreateService(db, new FakeCompanyContext(coA.CompanyID, coA.CompanyName));

            var result = await service.CreateBookerAsync(ValidDto(), user.UserID);

            Assert.True(result.Success);
            var booker = await db.Bookers.SingleAsync();
            Assert.Equal(coA.CompanyID, booker.CompanyID);
            Assert.Equal("Nadeem", booker.Name);
            Assert.Equal("35202-1234567-1", booker.CNIC);
            Assert.Equal(user.UserID, booker.CreatedBy);
        }

        [Fact]
        public async Task Create_DuplicateName_SameCompany_Fails_OtherCompany_Succeeds()
        {
            var (db, coA, coB, user) = await SeedBaseAsync(nameof(Create_DuplicateName_SameCompany_Fails_OtherCompany_Succeeds));
            var scopeA = new FakeCompanyContext(coA.CompanyID, coA.CompanyName);
            var scopeB = new FakeCompanyContext(coB.CompanyID, coB.CompanyName);

            var first = await CreateService(db, scopeA).CreateBookerAsync(ValidDto("Nadeem", "35202-1111111-1"), user.UserID);
            Assert.True(first.Success);

            var dup = await CreateService(db, scopeA).CreateBookerAsync(ValidDto("Nadeem", "35202-2222222-2"), user.UserID);
            Assert.False(dup.Success);
            Assert.Contains(dup.Errors, e => e.Contains("already exists", StringComparison.OrdinalIgnoreCase));

            var other = await CreateService(db, scopeB).CreateBookerAsync(ValidDto("Nadeem", "35202-3333333-3"), user.UserID);
            Assert.True(other.Success);
            Assert.Equal(2, await db.Bookers.CountAsync());
        }

        [Fact]
        public async Task Create_CnicRequired_AndBadFormat_Fails()
        {
            var (db, coA, _, user) = await SeedBaseAsync(nameof(Create_CnicRequired_AndBadFormat_Fails));
            var service = CreateService(db, new FakeCompanyContext(coA.CompanyID, coA.CompanyName));

            var missing = await service.CreateBookerAsync(ValidDto(cnic: "  "), user.UserID);
            Assert.False(missing.Success);
            Assert.Contains(missing.Errors, e => e.Contains("CNIC is required", StringComparison.OrdinalIgnoreCase));

            var bad = await service.CreateBookerAsync(ValidDto(cnic: "1234567890123"), user.UserID);
            Assert.False(bad.Success);
            Assert.Contains(bad.Errors, e => e.Contains("format", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Create_DuplicateCnic_Fails()
        {
            var (db, coA, coB, user) = await SeedBaseAsync(nameof(Create_DuplicateCnic_Fails));
            var scopeA = new FakeCompanyContext(coA.CompanyID, coA.CompanyName);
            var scopeB = new FakeCompanyContext(coB.CompanyID, coB.CompanyName);

            Assert.True((await CreateService(db, scopeA).CreateBookerAsync(ValidDto("A", "35202-5555555-5"), user.UserID)).Success);

            var dup = await CreateService(db, scopeB).CreateBookerAsync(ValidDto("B", "35202-5555555-5"), user.UserID);
            Assert.False(dup.Success);
            Assert.Contains(dup.Errors, e => e.Contains("CNIC", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Create_WithoutCompanyScope_Fails()
        {
            var (db, _, _, user) = await SeedBaseAsync(nameof(Create_WithoutCompanyScope_Fails));
            var service = CreateService(db, new FakeCompanyContext(1, "X", resolve: false));

            var result = await service.CreateBookerAsync(ValidDto(), user.UserID);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("company", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(db.Bookers);
        }

        [Fact]
        public async Task Update_CompanyRemainsImmutable_WhenHasSalesInvoices()
        {
            var (db, coA, _, user) = await SeedBaseAsync(nameof(Update_CompanyRemainsImmutable_WhenHasSalesInvoices));
            var service = CreateService(db, new FakeCompanyContext(coA.CompanyID, coA.CompanyName));
            var created = await service.CreateBookerAsync(ValidDto(), user.UserID);
            Assert.True(created.Success);
            int bookerId = created.Data;

            db.Customers.Add(new Customer { CustomerID = 1, ShopName = "Shop", IsActive = true });
            db.Warehouses.Add(new Warehouse { WarehouseID = 1, Name = "WH", IsActive = true });
            db.SalesInvoices.Add(new SalesInvoice
            {
                InvoiceID = 1,
                InvoiceNumber = "SI-1",
                CustomerID = 1,
                CompanyID = coA.CompanyID,
                WarehouseID = 1,
                BookerID = bookerId,
                InvoiceDate = DateTime.UtcNow.Date,
                GrandTotal = 500m,
                PaidAmount = 100m,
                CreatedBy = user.UserID
            });
            await db.SaveChangesAsync();

            var edit = await service.GetBookerForEditAsync(bookerId);
            Assert.NotNull(edit);
            Assert.False(edit!.CanChangeCompany);
            Assert.Equal(coA.CompanyName, edit.CompanyName);

            edit.Name = "Nadeem Updated";
            edit.Phone = "03009998877";
            var update = await service.UpdateBookerAsync(edit, user.UserID);
            Assert.True(update.Success);

            var reloaded = await db.Bookers.AsNoTracking().SingleAsync(b => b.BookerID == bookerId);
            Assert.Equal(coA.CompanyID, reloaded.CompanyID);
            Assert.Equal("Nadeem Updated", reloaded.Name);
        }

        [Fact]
        public async Task SoftDelete_Blocked_WhenHasInvoices()
        {
            var (db, coA, _, user) = await SeedBaseAsync(nameof(SoftDelete_Blocked_WhenHasInvoices));
            var service = CreateService(db, new FakeCompanyContext(coA.CompanyID, coA.CompanyName));
            var created = await service.CreateBookerAsync(ValidDto(), user.UserID);
            int bookerId = created.Data;

            db.Customers.Add(new Customer { CustomerID = 1, ShopName = "Shop", IsActive = true });
            db.Warehouses.Add(new Warehouse { WarehouseID = 1, Name = "WH", IsActive = true });
            db.SalesInvoices.Add(new SalesInvoice
            {
                InvoiceID = 1,
                InvoiceNumber = "SI-1",
                CustomerID = 1,
                CompanyID = coA.CompanyID,
                WarehouseID = 1,
                BookerID = bookerId,
                InvoiceDate = DateTime.UtcNow.Date,
                GrandTotal = 100m,
                CreatedBy = user.UserID
            });
            await db.SaveChangesAsync();

            var result = await service.SoftDeleteBookerAsync(bookerId, user.UserID);
            Assert.False(result.Success);
            Assert.False((await db.Bookers.SingleAsync()).IsDeleted);
        }

        [Fact]
        public async Task OutstandingBalance_SumsGrandTotalMinusPaid()
        {
            var (db, coA, _, user) = await SeedBaseAsync(nameof(OutstandingBalance_SumsGrandTotalMinusPaid));
            var service = CreateService(db, new FakeCompanyContext(coA.CompanyID, coA.CompanyName));
            var created = await service.CreateBookerAsync(ValidDto(), user.UserID);
            int bookerId = created.Data;

            db.Customers.Add(new Customer { CustomerID = 1, ShopName = "Shop", IsActive = true });
            db.Warehouses.Add(new Warehouse { WarehouseID = 1, Name = "WH", IsActive = true });
            db.SalesInvoices.AddRange(
                new SalesInvoice
                {
                    InvoiceNumber = "SI-1",
                    CustomerID = 1,
                    CompanyID = coA.CompanyID,
                    WarehouseID = 1,
                    BookerID = bookerId,
                    InvoiceDate = DateTime.UtcNow.Date,
                    GrandTotal = 1000m,
                    PaidAmount = 200m,
                    CreatedBy = user.UserID
                },
                new SalesInvoice
                {
                    InvoiceNumber = "SI-2",
                    CustomerID = 1,
                    CompanyID = coA.CompanyID,
                    WarehouseID = 1,
                    BookerID = bookerId,
                    InvoiceDate = DateTime.UtcNow.Date,
                    GrandTotal = 500m,
                    PaidAmount = 500m,
                    CreatedBy = user.UserID
                });
            await db.SaveChangesAsync();

            var edit = await service.GetBookerForEditAsync(bookerId);
            Assert.Equal(800m, edit!.OutstandingBalance);

            var list = await service.GetPagedBookersAsync(new BookerFilterDto());
            Assert.Equal(800m, list.Items.Single().OutstandingBalance);
        }
    }
}
