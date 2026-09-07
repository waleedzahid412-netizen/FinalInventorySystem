using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Data;
using InventorySystem.DTOs.Analytics;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class AnalyticsServiceTests
    {
        private static ApplicationDbContext CreateContext(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private static AnalyticsService CreateService(ApplicationDbContext context) =>
            new AnalyticsService(
                new AnalyticsRepository(context),
                new FifoCostingService(context, NullLogger<FifoCostingService>.Instance),
                NullLogger<AnalyticsService>.Instance);

        private static async Task<SeedData> SeedAsync(ApplicationDbContext db)
        {
            var role = new Role { RoleName = "Admin", IsActive = true };
            db.Roles.Add(role);
            await db.SaveChangesAsync();

            var user = new User { RoleID = role.RoleID, FullName = "Admin", Username = "admin", PasswordHash = "x", IsActive = true };
            db.Users.Add(user);
            var warehouseA = new Warehouse { Name = "WH-A", IsActive = true };
            var warehouseB = new Warehouse { Name = "WH-B", IsActive = true };
            db.Warehouses.AddRange(warehouseA, warehouseB);
            var company = new Company { CompanyName = "Co", CreatedAt = DateTime.Today };
            db.Companies.Add(company);
            var area = new Area { AreaName = "North", IsActive = true };
            db.Areas.Add(area);
            await db.SaveChangesAsync();
            var sub = new SubArea { AreaID = area.AreaID, SubAreaName = "Block 1", IsActive = true };
            db.SubAreas.Add(sub);
            var customerA = new Customer { ShopName = "Shop A", IsActive = true, AreaID = area.AreaID, SubAreaID = sub.SubAreaID };
            var customerB = new Customer { ShopName = "Shop B", IsActive = true };
            db.Customers.AddRange(customerA, customerB);
            var catA = new Category { Name = "Drinks", CompanyID = company.CompanyID, IsActive = true };
            var catB = new Category { Name = "Snacks", CompanyID = company.CompanyID, IsActive = true };
            db.Categories.AddRange(catA, catB);
            var unit = new Unit { UnitName = "Piece", IsActive = true };
            db.Units.Add(unit);
            var booker = new Booker { CompanyID = company.CompanyID, Name = "Nadeem", CNIC = "35202-1111111", IsActive = true };
            db.Bookers.Add(booker);
            await db.SaveChangesAsync();

            var productA = new Product
            {
                ProductName = "Cola",
                CategoryID = catA.CategoryID,
                CompanyID = company.CompanyID,
                BaseUnitID = unit.UnitID,
                BaseSellingPrice = 100m,
                AveragePurchaseCost = 40m,
                ReorderLevel = 5,
                IsActive = true
            };
            var productB = new Product
            {
                ProductName = "Chips",
                CategoryID = catB.CategoryID,
                CompanyID = company.CompanyID,
                BaseUnitID = unit.UnitID,
                BaseSellingPrice = 50m,
                AveragePurchaseCost = 200m,
                ReorderLevel = 2,
                IsActive = true
            };
            db.Products.AddRange(productA, productB);
            await db.SaveChangesAsync();

            db.InventoryStocks.Add(new InventoryStock { ProductID = productA.ProductID, WarehouseID = warehouseA.WarehouseID, Quantity = 20m, IsActive = true });
            db.InventoryStocks.Add(new InventoryStock { ProductID = productB.ProductID, WarehouseID = warehouseA.WarehouseID, Quantity = 8m, IsActive = true });
            await db.SaveChangesAsync();

            return new SeedData(user.UserID, warehouseA.WarehouseID, warehouseB.WarehouseID, customerA.CustomerID, customerB.CustomerID,
                catA.CategoryID, catB.CategoryID, productA.ProductID, productB.ProductID, booker.BookerID, company.CompanyID, area.AreaID, sub.SubAreaID);
        }

        private static SalesInvoice CreateInvoice(SeedData s, int customerId, int warehouseId, DateTime date, decimal grand, int? bookerId, string number, int? companyId = null)
        {
            return new SalesInvoice
            {
                CustomerID = customerId,
                CompanyID = companyId ?? s.CompanyId,
                WarehouseID = warehouseId,
                InvoiceNumber = number,
                InvoiceDate = date,
                SubTotal = grand,
                GrandTotal = grand,
                DiscountTotal = 0m,
                PaymentStatus = "UNPAID",
                DiscountMode = "None",
                CreatedBy = s.UserId,
                BookerID = bookerId,
                AreaID = s.AreaId,
                SubAreaID = s.SubAreaId
            };
        }

        private static SalesInvoiceItem CreateItem(int invoiceId, int productId, decimal qty, decimal converted, decimal price, decimal discount = 0, decimal cogs = 0) =>
            new SalesInvoiceItem
            {
                InvoiceID = invoiceId,
                ProductID = productId,
                Quantity = qty,
                ConvertedQuantity = converted,
                UnitPrice = price,
                DiscountAmount = discount,
                CostOfGoodsSold = cogs,
                ItemType = "NORMAL"
            };

        private static PurchaseInvoice CreatePurchase(int companyId, int warehouseId, int userId, DateTime date, decimal grand, string number) =>
            new PurchaseInvoice
            {
                CompanyID = companyId,
                WarehouseID = warehouseId,
                InvoiceNumber = number,
                InvoiceDate = date,
                SubTotal = grand,
                GrandTotal = grand,
                PaymentStatus = "UNPAID",
                CreatedBy = userId
            };

        [Fact]
        public async Task SoftDeletedInvoice_IsExcluded()
        {
            var db = CreateContext(nameof(SoftDeletedInvoice_IsExcluded));
            var s = await SeedAsync(db);
            var live = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 100m, null, "S-1");
            var dead = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 500m, null, "S-2");
            dead.IsDeleted = true;
            db.SalesInvoices.AddRange(live, dead);
            await db.SaveChangesAsync();
            db.SalesInvoiceItems.Add(CreateItem(live.InvoiceID, s.ProductA, 1, 1, 100));
            db.SalesInvoiceItems.Add(CreateItem(dead.InvoiceID, s.ProductA, 1, 1, 500));
            await db.SaveChangesAsync();

            var kpi = await CreateService(db).GetKpiSummaryAsync(new AnalyticsFilterDto { Preset = "Today" });
            Assert.Equal(100m, kpi.TotalSales.CurrentValue);
        }

        [Fact]
        public async Task SoftDeletedProduct_DoesNotAppearInStockRisk()
        {
            var db = CreateContext(nameof(SoftDeletedProduct_DoesNotAppearInStockRisk));
            var s = await SeedAsync(db);
            var p = await db.Products.FindAsync(s.ProductA);
            p!.IsDeleted = true;
            await db.SaveChangesAsync();
            var risk = await CreateService(db).GetStockRiskItemsAsync(new AnalyticsFilterDto());
            Assert.DoesNotContain(risk, r => r.ProductID == s.ProductA);
        }

        [Fact]
        public async Task DateFilter_UsesCustomRange()
        {
            var db = CreateContext(nameof(DateFilter_UsesCustomRange));
            var s = await SeedAsync(db);
            var oldInv = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today.AddDays(-40), 100m, null, "S-old");
            var newInv = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 80m, null, "S-new");
            db.SalesInvoices.AddRange(oldInv, newInv);
            await db.SaveChangesAsync();
            db.SalesInvoiceItems.AddRange(CreateItem(oldInv.InvoiceID, s.ProductA, 1, 1, 100), CreateItem(newInv.InvoiceID, s.ProductA, 1, 1, 80));
            await db.SaveChangesAsync();

            var kpi = await CreateService(db).GetKpiSummaryAsync(new AnalyticsFilterDto
            {
                Preset = "Custom",
                StartDate = DateTime.Today.AddDays(-1),
                EndDate = DateTime.Today
            });
            Assert.Equal(80m, kpi.TotalSales.CurrentValue);
        }

        [Fact]
        public async Task WarehouseFilter_ExcludesOtherWarehouse()
        {
            var db = CreateContext(nameof(WarehouseFilter_ExcludesOtherWarehouse));
            var s = await SeedAsync(db);
            var a = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 10m, null, "WA");
            var b = CreateInvoice(s, s.CustomerA, s.WarehouseB, DateTime.Today, 90m, null, "WB");
            db.SalesInvoices.AddRange(a, b);
            await db.SaveChangesAsync();
            db.SalesInvoiceItems.AddRange(CreateItem(a.InvoiceID, s.ProductA, 1, 1, 10), CreateItem(b.InvoiceID, s.ProductA, 1, 1, 90));
            await db.SaveChangesAsync();
            var kpi = await CreateService(db).GetKpiSummaryAsync(new AnalyticsFilterDto { Preset = "Today", WarehouseID = s.WarehouseA });
            Assert.Equal(10m, kpi.TotalSales.CurrentValue);
        }

        [Fact]
        public async Task CustomerFilter_ExcludesOtherCustomer()
        {
            var db = CreateContext(nameof(CustomerFilter_ExcludesOtherCustomer));
            var s = await SeedAsync(db);
            var a = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 25m, null, "CA");
            var b = CreateInvoice(s, s.CustomerB, s.WarehouseA, DateTime.Today, 75m, null, "CB");
            db.SalesInvoices.AddRange(a, b);
            await db.SaveChangesAsync();
            db.SalesInvoiceItems.AddRange(CreateItem(a.InvoiceID, s.ProductA, 1, 1, 25), CreateItem(b.InvoiceID, s.ProductA, 1, 1, 75));
            await db.SaveChangesAsync();
            var kpi = await CreateService(db).GetKpiSummaryAsync(new AnalyticsFilterDto { Preset = "Today", CustomerID = s.CustomerA });
            Assert.Equal(25m, kpi.TotalSales.CurrentValue);
        }

        [Fact]
        public async Task CategoryFilter_UsesLineRevenueNotFullInvoice()
        {
            var db = CreateContext(nameof(CategoryFilter_UsesLineRevenueNotFullInvoice));
            var s = await SeedAsync(db);
            var inv = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 300m, null, "MIX");
            db.SalesInvoices.Add(inv);
            await db.SaveChangesAsync();
            db.SalesInvoiceItems.Add(CreateItem(inv.InvoiceID, s.ProductA, 1, 1, 100));
            db.SalesInvoiceItems.Add(CreateItem(inv.InvoiceID, s.ProductB, 1, 1, 200));
            await db.SaveChangesAsync();

            var kpi = await CreateService(db).GetKpiSummaryAsync(new AnalyticsFilterDto { Preset = "Today", CategoryID = s.CategoryA });
            Assert.Equal(100m, kpi.TotalSales.CurrentValue);
        }

        [Fact]
        public async Task BookerFilter_UsesBookerIdNotSalesperson()
        {
            var db = CreateContext(nameof(BookerFilter_UsesBookerIdNotSalesperson));
            var s = await SeedAsync(db);
            var withBooker = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 60m, s.BookerId, "BR");
            var unassigned = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 40m, null, "UN");
            db.SalesInvoices.AddRange(withBooker, unassigned);
            await db.SaveChangesAsync();
            db.SalesInvoiceItems.AddRange(CreateItem(withBooker.InvoiceID, s.ProductA, 1, 1, 60), CreateItem(unassigned.InvoiceID, s.ProductA, 1, 1, 40));
            await db.SaveChangesAsync();

            var kpi = await CreateService(db).GetKpiSummaryAsync(new AnalyticsFilterDto { Preset = "Today", BookerID = s.BookerId });
            Assert.Equal(60m, kpi.TotalSales.CurrentValue);
        }

        [Fact]
        public async Task BookerUnassigned_IsGroupedSeparately()
        {
            var db = CreateContext(nameof(BookerUnassigned_IsGroupedSeparately));
            var s = await SeedAsync(db);
            var withBooker = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 60m, s.BookerId, "BR");
            var unassigned = CreateInvoice(s, s.CustomerB, s.WarehouseA, DateTime.Today, 40m, null, "UN");
            db.SalesInvoices.AddRange(withBooker, unassigned);
            await db.SaveChangesAsync();

            var data = await CreateService(db).GetBookerAnalyticsAsync(new AnalyticsFilterDto { Preset = "Today" });
            Assert.Contains(data.Bookers, b => b.BookerName == "Unassigned" && b.Revenue == 40m);
            Assert.Contains(data.Bookers, b => b.BookerID == s.BookerId && b.Revenue == 60m);
        }

        [Fact]
        public async Task ProductQuantity_UsesConvertedQuantity()
        {
            var db = CreateContext(nameof(ProductQuantity_UsesConvertedQuantity));
            var s = await SeedAsync(db);
            var inv = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 100m, null, "Q");
            db.SalesInvoices.Add(inv);
            await db.SaveChangesAsync();
            db.SalesInvoiceItems.Add(CreateItem(inv.InvoiceID, s.ProductA, 1m, 12m, 100m));
            await db.SaveChangesAsync();

            var tops = await CreateService(db).GetTopProductsAsync(new AnalyticsFilterDto { Preset = "Today" }, "Quantity", 10);
            Assert.Equal(12m, tops.Single().QuantitySold);
        }

        [Fact]
        public async Task HistoricalMoney_UsesInvoiceUnitPrice()
        {
            var db = CreateContext(nameof(HistoricalMoney_UsesInvoiceUnitPrice));
            var s = await SeedAsync(db);
            var inv = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 70m, null, "P");
            db.SalesInvoices.Add(inv);
            await db.SaveChangesAsync();
            db.SalesInvoiceItems.Add(CreateItem(inv.InvoiceID, s.ProductA, 1m, 1m, 70m));
            var product = await db.Products.FindAsync(s.ProductA);
            product!.BaseSellingPrice = 999m;
            await db.SaveChangesAsync();

            var tops = await CreateService(db).GetTopProductsAsync(new AnalyticsFilterDto { Preset = "Today" }, "Revenue", 10);
            Assert.Equal(70m, tops.Single().Revenue);
        }

        [Fact]
        public async Task WeeklyGrouping_SeparatesYears()
        {
            var db = CreateContext(nameof(WeeklyGrouping_SeparatesYears));
            var s = await SeedAsync(db);
            var d2026 = new DateTime(2026, 1, 5);
            var d2027 = new DateTime(2027, 1, 4);
            var a = CreateInvoice(s, s.CustomerA, s.WarehouseA, d2026, 10m, null, "Y1");
            var b = CreateInvoice(s, s.CustomerA, s.WarehouseA, d2027, 20m, null, "Y2");
            db.SalesInvoices.AddRange(a, b);
            await db.SaveChangesAsync();

            var trend = await CreateService(db).GetSalesTrendAsync(new AnalyticsFilterDto
            {
                Preset = "Custom",
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2027, 1, 10)
            }, "Weekly");

            Assert.Equal(2, trend.Count);
            Assert.Contains(trend, t => t.Label.Contains("2026"));
            Assert.Contains(trend, t => t.Label.Contains("2027"));
        }

        [Fact]
        public async Task Returns_ReduceNetSales()
        {
            var db = CreateContext(nameof(Returns_ReduceNetSales));
            var s = await SeedAsync(db);
            var inv = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 100m, null, "R");
            db.SalesInvoices.Add(inv);
            await db.SaveChangesAsync();
            db.SalesReturns.Add(new SalesReturn
            {
                ReturnNumber = "RET-1",
                ReturnType = "INVOICE",
                InvoiceID = inv.InvoiceID,
                CustomerID = s.CustomerA,
                WarehouseID = s.WarehouseA,
                SettlementMethod = "ACCOUNT_ADJUSTMENT",
                ReturnDate = DateTime.Today,
                NetRefundAmount = 30m,
                CreatedBy = s.UserId
            });
            await db.SaveChangesAsync();

            var kpi = await CreateService(db).GetKpiSummaryAsync(new AnalyticsFilterDto { Preset = "Today" });
            Assert.Equal(70m, kpi.NetSales.CurrentValue);
        }

        [Fact]
        public async Task Returns_CompanyFilter_ExcludesOtherCompanyReturns()
        {
            var db = CreateContext(nameof(Returns_CompanyFilter_ExcludesOtherCompanyReturns));
            var s = await SeedAsync(db);

            var companyB = new Company { CompanyName = "Coca-Cola", CreatedAt = DateTime.Today };
            db.Companies.Add(companyB);
            await db.SaveChangesAsync();

            var invA = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 63m, null, "A-RET");
            db.SalesInvoices.Add(invA);
            await db.SaveChangesAsync();
            db.SalesReturns.Add(new SalesReturn
            {
                ReturnNumber = "RET-A",
                ReturnType = "INVOICE",
                InvoiceID = invA.InvoiceID,
                CustomerID = s.CustomerA,
                WarehouseID = s.WarehouseA,
                SettlementMethod = "ACCOUNT_ADJUSTMENT",
                ReturnDate = DateTime.Today,
                NetRefundAmount = 63m,
                CreatedBy = s.UserId
            });

            var invB = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 16.97m, null, "B-1", companyB.CompanyID);
            db.SalesInvoices.Add(invB);
            await db.SaveChangesAsync();

            var kpi = await CreateService(db).GetKpiSummaryAsync(new AnalyticsFilterDto
            {
                Preset = "Today",
                CompanyID = companyB.CompanyID
            });

            Assert.Equal(16.97m, kpi.TotalSales.CurrentValue);
            Assert.Equal(0m, kpi.TotalReturns.CurrentValue);
            Assert.Equal(16.97m, kpi.NetSales.CurrentValue);
        }

        [Fact]
        public async Task Payables_CompanyFilter_UsesInvoiceOutstandingOnly()
        {
            var db = CreateContext(nameof(Payables_CompanyFilter_UsesInvoiceOutstandingOnly));
            var s = await SeedAsync(db);

            var companyB = new Company { CompanyName = "Coca-Cola", CreatedAt = DateTime.Today };
            db.Companies.Add(companyB);
            await db.SaveChangesAsync();

            db.PurchaseInvoices.Add(CreatePurchase(s.CompanyId, s.WarehouseA, s.UserId, DateTime.Today, 33000m, "PINV-A"));
            db.PurchaseInvoices.Add(CreatePurchase(companyB.CompanyID, s.WarehouseA, s.UserId, DateTime.Today, 12000m, "PINV-B"));
            await db.SaveChangesAsync();

            var kpi = await CreateService(db).GetKpiSummaryAsync(new AnalyticsFilterDto
            {
                Preset = "Today",
                CompanyID = companyB.CompanyID
            });

            Assert.Equal(12000m, kpi.CompanyPayables.CurrentValue);
        }

        [Fact]
        public async Task Receivables_CompanyFilter_UsesInvoiceOutstandingOnly()
        {
            var db = CreateContext(nameof(Receivables_CompanyFilter_UsesInvoiceOutstandingOnly));
            var s = await SeedAsync(db);

            var companyB = new Company { CompanyName = "Coca-Cola", CreatedAt = DateTime.Today };
            db.Companies.Add(companyB);
            await db.SaveChangesAsync();

            db.SalesInvoices.Add(CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 62.87m, null, "A-RCV"));
            db.SalesInvoices.Add(CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 42.87m, null, "B-RCV", companyB.CompanyID));
            await db.SaveChangesAsync();

            var kpi = await CreateService(db).GetKpiSummaryAsync(new AnalyticsFilterDto
            {
                Preset = "Today",
                CompanyID = companyB.CompanyID
            });

            Assert.Equal(42.87m, kpi.CustomerReceivables.CurrentValue);
        }

        [Fact]
        public async Task GrossProfit_UsesFifoCogsOnSaleLine()
        {
            var db = CreateContext(nameof(GrossProfit_UsesFifoCogsOnSaleLine));
            var s = await SeedAsync(db);
            var inv = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 100m, null, "FIFO");
            db.SalesInvoices.Add(inv);
            await db.SaveChangesAsync();
            db.SalesInvoiceItems.Add(CreateItem(inv.InvoiceID, s.ProductA, 1m, 1m, 100m, cogs: 40m));
            await db.SaveChangesAsync();

            var kpi = await CreateService(db).GetKpiSummaryAsync(new AnalyticsFilterDto { Preset = "Today" });
            Assert.Equal(100m, kpi.NetSales.CurrentValue);
            Assert.Equal(60m, kpi.EstimatedGrossProfit.CurrentValue);
        }

        [Fact]
        public async Task NegativeProfit_IsPreserved()
        {
            var db = CreateContext(nameof(NegativeProfit_IsPreserved));
            var s = await SeedAsync(db);
            var inv = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 10m, null, "LOSS");
            db.SalesInvoices.Add(inv);
            await db.SaveChangesAsync();
            db.SalesInvoiceItems.Add(CreateItem(inv.InvoiceID, s.ProductB, 1m, 1m, 10m));
            await db.SaveChangesAsync();

            var kpi = await CreateService(db).GetKpiSummaryAsync(new AnalyticsFilterDto { Preset = "Today" });
            Assert.True(kpi.EstimatedGrossProfit.CurrentValue < 0);
        }

        [Fact]
        public async Task TopCustomers_SortByInvoiceCount()
        {
            var db = CreateContext(nameof(TopCustomers_SortByInvoiceCount));
            var s = await SeedAsync(db);
            var a1 = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 100m, null, "A1");
            var b1 = CreateInvoice(s, s.CustomerB, s.WarehouseA, DateTime.Today, 10m, null, "B1");
            var b2 = CreateInvoice(s, s.CustomerB, s.WarehouseA, DateTime.Today, 10m, null, "B2");
            db.SalesInvoices.AddRange(a1, b1, b2);
            await db.SaveChangesAsync();

            var tops = await CreateService(db).GetTopCustomersAsync(new AnalyticsFilterDto { Preset = "Today" }, "InvoiceCount", 10);
            Assert.Equal(s.CustomerB, tops.First().CustomerID);
            Assert.Equal(2, tops.First().InvoiceCount);
        }

        [Fact]
        public async Task TopProducts_SortByQuantity()
        {
            var db = CreateContext(nameof(TopProducts_SortByQuantity));
            var s = await SeedAsync(db);
            var inv = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 50m, null, "TP");
            db.SalesInvoices.Add(inv);
            await db.SaveChangesAsync();
            db.SalesInvoiceItems.Add(CreateItem(inv.InvoiceID, s.ProductA, 1m, 2m, 40m));
            db.SalesInvoiceItems.Add(CreateItem(inv.InvoiceID, s.ProductB, 1m, 9m, 10m));
            await db.SaveChangesAsync();

            var tops = await CreateService(db).GetTopProductsAsync(new AnalyticsFilterDto { Preset = "Today" }, "Quantity", 10);
            Assert.Equal(s.ProductB, tops.First().ProductID);
        }

        [Fact]
        public async Task CustomerDetail_ReturnsShopMetrics()
        {
            var db = CreateContext(nameof(CustomerDetail_ReturnsShopMetrics));
            var s = await SeedAsync(db);
            var inv = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 55m, null, "CD");
            db.SalesInvoices.Add(inv);
            await db.SaveChangesAsync();
            db.SalesInvoiceItems.Add(CreateItem(inv.InvoiceID, s.ProductA, 1m, 1m, 55m));
            await db.SaveChangesAsync();

            var detail = await CreateService(db).GetCustomerDetailAsync(s.CustomerA, new AnalyticsFilterDto { Preset = "Today" });
            Assert.NotNull(detail);
            Assert.Equal("Shop A", detail!.CustomerName);
            Assert.Equal(55m, detail.Billed);
            Assert.Equal("North", detail.AreaName);
        }

        [Fact]
        public async Task ProductDetail_ReturnsWarehouseStock()
        {
            var db = CreateContext(nameof(ProductDetail_ReturnsWarehouseStock));
            var s = await SeedAsync(db);
            var detail = await CreateService(db).GetProductDetailAsync(s.ProductA, new AnalyticsFilterDto { Preset = "ThisMonth" });
            Assert.NotNull(detail);
            Assert.Equal("Cola", detail!.ProductName);
            Assert.Equal(20m, detail.CurrentStock);
        }

        [Fact]
        public async Task BookerDetail_Unassigned_UsesNullBooker()
        {
            var db = CreateContext(nameof(BookerDetail_Unassigned_UsesNullBooker));
            var s = await SeedAsync(db);
            var inv = CreateInvoice(s, s.CustomerA, s.WarehouseA, DateTime.Today, 15m, null, "UN2");
            db.SalesInvoices.Add(inv);
            await db.SaveChangesAsync();

            var detail = await CreateService(db).GetBookerDetailAsync(null, new AnalyticsFilterDto { Preset = "Today" });
            Assert.NotNull(detail);
            Assert.Equal("Unassigned", detail!.BookerName);
            Assert.Equal(15m, detail.Revenue);
        }

        [Fact]
        public async Task InventoryInsights_UsesFifoLayerValue_NotAverageTimesQuantity()
        {
            var db = CreateContext(nameof(InventoryInsights_UsesFifoLayerValue_NotAverageTimesQuantity));
            var s = await SeedAsync(db);

            db.InventoryCostLayers.AddRange(
                new InventoryCostLayer
                {
                    ProductID = s.ProductA,
                    WarehouseID = s.WarehouseA,
                    ReceivedAt = DateTime.UtcNow,
                    SourceType = "PURCHASE",
                    OriginalQuantity = 20m,
                    RemainingQuantity = 20m,
                    UnitCostInBase = 12m,
                    IsDeleted = false
                },
                new InventoryCostLayer
                {
                    ProductID = s.ProductB,
                    WarehouseID = s.WarehouseA,
                    ReceivedAt = DateTime.UtcNow,
                    SourceType = "PURCHASE",
                    OriginalQuantity = 8m,
                    RemainingQuantity = 8m,
                    UnitCostInBase = 25m,
                    IsDeleted = false
                });
            await db.SaveChangesAsync();

            var insights = await CreateService(db).GetInventoryInsightsAsync(new AnalyticsFilterDto());

            // FIFO: 20×12 + 8×25 = 440 — not qty×AveragePurchaseCost (20×40 + 8×200 = 2400)
            Assert.Equal(440m, insights.TotalInventoryValue);
            Assert.Contains(insights.ValueByCategory, c => c.CategoryName == "Drinks" && c.TotalValue == 240m);
            Assert.Contains(insights.ValueByCategory, c => c.CategoryName == "Snacks" && c.TotalValue == 200m);
        }

        [Fact]
        public void WeeklyBucket_IncludesYear()
        {
            var first = AnalyticsFilterHelper.Bucket(new DateTime(2026, 1, 5), "Weekly");
            var second = AnalyticsFilterHelper.Bucket(new DateTime(2027, 1, 4), "Weekly");
            Assert.NotEqual(first.Label, second.Label);
        }

        private sealed record SeedData(
            int UserId, int WarehouseA, int WarehouseB, int CustomerA, int CustomerB,
            int CategoryA, int CategoryB, int ProductA, int ProductB, int BookerId, int CompanyId, int AreaId, int SubAreaId);
    }
}
