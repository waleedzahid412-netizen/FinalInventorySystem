using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Data;
using InventorySystem.DTOs.Sales;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class LedgerAppendHelperTests
    {
        [Fact]
        public void AppendSalesInvoiceCorrection_AddsReversalAndNewSale_WhenTotalsDiffer()
        {
            var invoice = new SalesInvoice
            {
                InvoiceID = 10,
                CustomerID = 1,
                InvoiceNumber = "SINV-100",
                Version = 2
            };
            var entries = new List<CustomerLedger>();
            var now = DateTime.UtcNow;

            LedgerAppendHelper.AppendSalesInvoiceCorrection(invoice, 10000m, 8000m, now, userId: 5, now, entries);

            Assert.Equal(2, entries.Count);
            Assert.Equal("ADJUSTMENT", entries[0].TransactionType);
            Assert.Equal(0m, entries[0].DebitAmount);
            Assert.Equal(10000m, entries[0].CreditAmount);
            Assert.Equal("SALE", entries[1].TransactionType);
            Assert.Equal(8000m, entries[1].DebitAmount);
            Assert.Equal(0m, entries[1].CreditAmount);
        }

        [Fact]
        public void AppendSalesInvoiceCorrection_Skips_WhenTotalsUnchanged()
        {
            var entries = new List<CustomerLedger>();
            LedgerAppendHelper.AppendSalesInvoiceCorrection(
                new SalesInvoice { InvoiceID = 1, CustomerID = 1, InvoiceNumber = "X", Version = 2 },
                5000m, 5000m, DateTime.UtcNow, 1, DateTime.UtcNow, entries);
            Assert.Empty(entries);
        }

        [Fact]
        public void AppendPurchaseInvoiceCorrection_AddsReversalAndNewPurchase_WhenTotalsDiffer()
        {
            var invoice = new PurchaseInvoice
            {
                PurchaseInvoiceID = 20,
                CompanyID = 3,
                InvoiceNumber = "PINV-200",
                Version = 2
            };
            var entries = new List<CompanyLedger>();

            LedgerAppendHelper.AppendPurchaseInvoiceCorrection(
                invoice, 5000m, 4200m, DateTime.UtcNow, userId: 1, DateTime.UtcNow, entries);

            Assert.Equal(2, entries.Count);
            Assert.Equal("ADJUSTMENT", entries[0].TransactionType);
            Assert.Equal(5000m, entries[0].DebitAmount);
            Assert.Equal("PURCHASE", entries[1].TransactionType);
            Assert.Equal(4200m, entries[1].CreditAmount);
        }
    }

    public class SalesLedgerAppendIntegrationTests
    {
        [Fact]
        public async Task EditSalesInvoice_AppendsLedgerRows_OriginalSaleUnchanged_NetBalanceCorrect()
        {
            await using var context = CreateContext(nameof(EditSalesInvoice_AppendsLedgerRows_OriginalSaleUnchanged_NetBalanceCorrect));
            var fifo = TestFifoHelper.CreateFifo(context);
            var salesService = new SalesService(
                new SalesRepository(context),
                context,
                new PromotionDiscountService(context),
                new FakeCompanyContext(1, "Co"),
                fifo,
                NullLogger<SalesService>.Instance);

            await SeedMinimalSalesCatalogAsync(context);

            var create = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = 1, Quantity = 10m, UnitPrice = 100m, ItemType = "NORMAL" }
                }
            }, userId: 1);
            Assert.True(create.Success);

            var originalSaleLedger = await context.CustomerLedgers
                .AsNoTracking()
                .SingleAsync(l => l.SalesInvoiceID == create.Data && l.TransactionType == "SALE");
            Assert.Equal(1000m, originalSaleLedger.DebitAmount);

            var update = await salesService.UpdateSalesInvoiceAsync(new UpdateSalesInvoiceDto
            {
                InvoiceID = create.Data,
                InvoiceDate = DateTime.Today,
                BookerID = 1,
                SalespersonID = 1,
                EditReason = "Qty reduced",
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = 1, Quantity = 8m, UnitPrice = 100m, ItemType = "NORMAL" }
                }
            }, userId: 1);
            Assert.True(update.Success);

            var allLedger = await context.CustomerLedgers
                .AsNoTracking()
                .Where(l => l.SalesInvoiceID == create.Data)
                .OrderBy(l => l.CustomerLedgerID)
                .ToListAsync();

            Assert.Equal(3, allLedger.Count);
            Assert.Equal(1000m, allLedger[0].DebitAmount); // original untouched
            Assert.Equal("ADJUSTMENT", allLedger[1].TransactionType);
            Assert.Equal(1000m, allLedger[1].CreditAmount);
            Assert.Equal("SALE", allLedger[2].TransactionType);
            Assert.Equal(800m, allLedger[2].DebitAmount);

            decimal netBalance = allLedger.Sum(l => l.DebitAmount - l.CreditAmount);
            Assert.Equal(800m, netBalance);

            var invoice = await context.SalesInvoices.AsNoTracking().SingleAsync(i => i.InvoiceID == create.Data);
            Assert.Equal(800m, invoice.GrandTotal);
            Assert.Equal(2, invoice.Version);
        }

        private static ApplicationDbContext CreateContext(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private static async Task SeedMinimalSalesCatalogAsync(ApplicationDbContext context)
        {
            context.Companies.Add(new Company { CompanyID = 1, CompanyName = "Co" });
            context.Warehouses.Add(new Warehouse { WarehouseID = 1, Name = "Main", IsActive = true });
            context.Units.Add(new Unit { UnitID = 1, UnitName = "Piece" });
            context.Categories.Add(new Category { CategoryID = 1, CompanyID = 1, Name = "Cat" });
            context.Roles.Add(new Role { RoleID = 1, RoleName = "Admin", IsActive = true });
            context.Users.Add(new User { UserID = 1, RoleID = 1, FullName = "U", Username = "u", PasswordHash = "x", IsActive = true });
            context.Customers.Add(new Customer { CustomerID = 1, ShopName = "Shop", Address = "A", IsActive = true });
            context.Bookers.Add(new Booker { BookerID = 1, CompanyID = 1, Name = "B", CNIC = "35202-1111111-1", IsActive = true });
            context.Products.Add(new Product
            {
                ProductID = 1, ProductName = "P", SKU = "P1", CategoryID = 1, CompanyID = 1,
                BaseUnitID = 1, BaseSellingPrice = 100m, IsActive = true
            });
            context.ProductUnits.Add(new ProductUnit
            {
                ProductUnitID = 1, ProductID = 1, UnitID = 1, ConversionToBaseUnit = 1m,
                SellingPrice = 100m, IsDefaultSalesUnit = true, IsActive = true
            });
            context.InventoryStocks.Add(new InventoryStock { ProductID = 1, WarehouseID = 1, Quantity = 500m, IsActive = true });
            context.InventoryCostLayers.Add(new InventoryCostLayer
            {
                ProductID = 1, WarehouseID = 1, ReceivedAt = DateTime.UtcNow.AddDays(-1),
                SourceType = "PURCHASE", OriginalQuantity = 500m, RemainingQuantity = 500m,
                UnitCostInBase = 50m, IsDeleted = false
            });
            await context.SaveChangesAsync();
        }
    }
}
