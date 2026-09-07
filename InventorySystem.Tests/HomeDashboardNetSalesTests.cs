using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using Xunit;

namespace InventorySystem.Tests
{
    public class HomeDashboardNetSalesTests
    {
        private static ApplicationDbContext CreateDb(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private static async Task<(int CompanyId, int CustomerId, int WarehouseId, int UserId)> SeedBaseAsync(ApplicationDbContext db)
        {
            db.Roles.Add(new Role { RoleID = 1, RoleName = "Admin", IsActive = true });
            db.Users.Add(new User { UserID = 1, RoleID = 1, FullName = "Admin", Username = "admin", PasswordHash = "x", IsActive = true });
            db.Companies.Add(new Company { CompanyID = 1, CompanyName = "Test Co" });
            db.Warehouses.Add(new Warehouse { WarehouseID = 1, Name = "Main", IsActive = true });
            db.Customers.Add(new Customer { CustomerID = 1, ShopName = "Test Shop", IsActive = true });
            await db.SaveChangesAsync();
            return (1, 1, 1, 1);
        }

        private static SalesInvoice CreateInvoice(int invoiceId, decimal grandTotal, DateTime invoiceDate)
        {
            return new SalesInvoice
            {
                InvoiceID = invoiceId,
                InvoiceNumber = $"INV-{invoiceId:D4}",
                CustomerID = 1,
                CompanyID = 1,
                WarehouseID = 1,
                InvoiceDate = invoiceDate,
                SubTotal = grandTotal,
                GrandTotal = grandTotal,
                PaidAmount = 0m,
                PaymentStatus = "UNPAID",
                CreatedBy = 1
            };
        }

        [Fact]
        public async Task NetSales_NoReturn_EqualsGrossSales()
        {
            await using var db = CreateDb(nameof(NetSales_NoReturn_EqualsGrossSales));
            await SeedBaseAsync(db);
            var today = DateTime.UtcNow.Date;
            var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            db.SalesInvoices.Add(CreateInvoice(1, 63m, today));
            await db.SaveChangesAsync();

            var salesQuery = db.SalesInvoices.AsNoTracking().Where(i => !i.IsDeleted);
            var returnsQuery = db.SalesReturns.AsNoTracking().Where(r => !r.IsDeleted);

            decimal gross = await DashboardMetricsHelper.SumGrossSalesAsync(salesQuery, startOfMonth);
            decimal returns = await DashboardMetricsHelper.SumReturnsAsync(returnsQuery, startOfMonth);
            decimal net = DashboardMetricsHelper.ComputeNetSales(gross, returns);

            Assert.Equal(63m, gross);
            Assert.Equal(0m, returns);
            Assert.Equal(63m, net);
        }

        [Fact]
        public async Task NetSales_FullReturnSameMonth_IsZero()
        {
            await using var db = CreateDb(nameof(NetSales_FullReturnSameMonth_IsZero));
            await SeedBaseAsync(db);
            var today = DateTime.UtcNow.Date;
            var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            db.SalesInvoices.Add(CreateInvoice(1, 63m, today));
            await db.SaveChangesAsync();
            db.SalesReturns.Add(new SalesReturn
            {
                ReturnNumber = "SR-00001",
                ReturnType = "INVOICE",
                InvoiceID = 1,
                CustomerID = 1,
                WarehouseID = 1,
                SettlementMethod = "ACCOUNT_ADJUSTMENT",
                ReturnDate = today,
                NetRefundAmount = 63m,
                CreatedBy = 1
            });
            await db.SaveChangesAsync();

            var salesQuery = db.SalesInvoices.AsNoTracking().Where(i => !i.IsDeleted);
            var returnsQuery = db.SalesReturns.AsNoTracking().Where(r => !r.IsDeleted);

            decimal gross = await DashboardMetricsHelper.SumGrossSalesAsync(salesQuery, startOfMonth);
            decimal returns = await DashboardMetricsHelper.SumReturnsAsync(returnsQuery, startOfMonth);
            decimal net = DashboardMetricsHelper.ComputeNetSales(gross, returns);

            Assert.Equal(63m, gross);
            Assert.Equal(63m, returns);
            Assert.Equal(0m, net);
        }

        [Fact]
        public async Task NetSales_PartialReturnSameMonth_IsGrossMinusReturns()
        {
            await using var db = CreateDb(nameof(NetSales_PartialReturnSameMonth_IsGrossMinusReturns));
            await SeedBaseAsync(db);
            var today = DateTime.UtcNow.Date;
            var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            db.SalesInvoices.Add(CreateInvoice(1, 63m, today));
            await db.SaveChangesAsync();
            db.SalesReturns.Add(new SalesReturn
            {
                ReturnNumber = "SR-00002",
                ReturnType = "INVOICE",
                InvoiceID = 1,
                CustomerID = 1,
                WarehouseID = 1,
                SettlementMethod = "ACCOUNT_ADJUSTMENT",
                ReturnDate = today,
                NetRefundAmount = 30m,
                CreatedBy = 1
            });
            await db.SaveChangesAsync();

            var salesQuery = db.SalesInvoices.AsNoTracking().Where(i => !i.IsDeleted);
            var returnsQuery = db.SalesReturns.AsNoTracking().Where(r => !r.IsDeleted);

            decimal gross = await DashboardMetricsHelper.SumGrossSalesAsync(salesQuery, startOfMonth);
            decimal returns = await DashboardMetricsHelper.SumReturnsAsync(returnsQuery, startOfMonth);
            decimal net = DashboardMetricsHelper.ComputeNetSales(gross, returns);

            Assert.Equal(63m, gross);
            Assert.Equal(30m, returns);
            Assert.Equal(33m, net);
        }

        [Fact]
        public async Task CustomerReceivables_FullyReturnedInvoice_IsZero()
        {
            await using var db = CreateDb(nameof(CustomerReceivables_FullyReturnedInvoice_IsZero));
            await SeedBaseAsync(db);
            var today = DateTime.UtcNow.Date;

            var invoice = CreateInvoice(1, 63m, today);
            invoice.PaidAmount = 15m;
            invoice.PaymentStatus = "PARTIAL";
            db.SalesInvoices.Add(invoice);
            await db.SaveChangesAsync();
            db.SalesReturns.Add(new SalesReturn
            {
                ReturnNumber = "SR-00003",
                ReturnType = "INVOICE",
                InvoiceID = 1,
                CustomerID = 1,
                WarehouseID = 1,
                SettlementMethod = "ACCOUNT_ADJUSTMENT",
                ReturnDate = today,
                NetRefundAmount = 63m,
                CreatedBy = 1
            });
            await db.SaveChangesAsync();

            var salesQuery = db.SalesInvoices.AsNoTracking().Where(i => !i.IsDeleted);
            decimal receivables = await DashboardMetricsHelper.SumCustomerReceivablesAsync(salesQuery);

            Assert.Equal(0m, receivables);
        }
    }
}
