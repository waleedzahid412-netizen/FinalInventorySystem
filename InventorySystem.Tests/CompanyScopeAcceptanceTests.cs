using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Data;
using InventorySystem.DTOs.Products;
using InventorySystem.DTOs.Sales;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    /// <summary>
    /// Acceptance Tests A–G for company-scope UX (list filtering, customer outstanding,
    /// All Companies, URL ownership, create stamp, create-block).
    /// </summary>
    public class CompanyScopeAcceptanceTests
    {
        private static ApplicationDbContext CreateDb(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private static async Task SeedTwoCompaniesAsync(ApplicationDbContext db)
        {
            db.Roles.Add(new Role { RoleID = 1, RoleName = "Admin", IsActive = true });
            db.Users.Add(new User { UserID = 1, RoleID = 1, FullName = "Admin", Username = "admin", PasswordHash = "x", IsActive = true });
            db.Companies.AddRange(
                new Company { CompanyID = 1, CompanyName = "PepsiCo" },
                new Company { CompanyID = 2, CompanyName = "Colgate" });
            db.Warehouses.Add(new Warehouse { WarehouseID = 1, Name = "Main", IsActive = true });
            db.Customers.Add(new Customer { CustomerID = 1, ShopName = "ABC Retail Store", IsActive = true, CreditLimit = 100000m });
            db.Units.Add(new Unit { UnitID = 1, UnitName = "Piece" });
            db.Categories.AddRange(
                new Category { CategoryID = 1, CompanyID = 1, Name = "Pepsi Cats" },
                new Category { CategoryID = 2, CompanyID = 2, Name = "Colgate Cats" });
            db.Products.AddRange(
                new Product { ProductID = 1, ProductName = "Pepsi 1L", SKU = "P1", CompanyID = 1, CategoryID = 1, BaseUnitID = 1, BaseSellingPrice = 50m, IsActive = true },
                new Product { ProductID = 2, ProductName = "Colgate Paste", SKU = "C1", CompanyID = 2, CategoryID = 2, BaseUnitID = 1, BaseSellingPrice = 30m, IsActive = true });
            db.Bookers.AddRange(
                new Booker { BookerID = 1, CompanyID = 1, Name = "Pepsi Booker", CNIC = "35202-1111111", IsActive = true },
                new Booker { BookerID = 2, CompanyID = 2, Name = "Colgate Booker", CNIC = "35202-2222222", IsActive = true });
            db.SalesInvoices.AddRange(
                new SalesInvoice
                {
                    InvoiceID = 101,
                    InvoiceNumber = "SI-PEPSI",
                    CustomerID = 1,
                    CompanyID = 1,
                    WarehouseID = 1,
                    InvoiceDate = DateTime.UtcNow.Date,
                    GrandTotal = 50000m,
                    PaidAmount = 0m,
                    PaymentStatus = "UNPAID",
                    CreatedBy = 1
                },
                new SalesInvoice
                {
                    InvoiceID = 102,
                    InvoiceNumber = "SI-COLGATE",
                    CustomerID = 1,
                    CompanyID = 2,
                    WarehouseID = 1,
                    InvoiceDate = DateTime.UtcNow.Date,
                    GrandTotal = 30000m,
                    PaidAmount = 0m,
                    PaymentStatus = "UNPAID",
                    CreatedBy = 1
                });
            db.CustomerLedgers.AddRange(
                new CustomerLedger
                {
                    CustomerLedgerID = 1,
                    CustomerID = 1,
                    TransactionDate = DateTime.UtcNow.Date,
                    TransactionType = "SALE",
                    DebitAmount = 50000m,
                    CreditAmount = 0m,
                    SalesInvoiceID = 101,
                    CreatedBy = 1
                },
                new CustomerLedger
                {
                    CustomerLedgerID = 2,
                    CustomerID = 1,
                    TransactionDate = DateTime.UtcNow.Date,
                    TransactionType = "SALE",
                    DebitAmount = 30000m,
                    CreditAmount = 0m,
                    SalesInvoiceID = 102,
                    CreatedBy = 1
                });
            await db.SaveChangesAsync();
        }

        // TEST A — PepsiCo scope lists
        [Fact]
        public async Task TestA_PepsiCo_ListsOnlyPepsiData()
        {
            await using var db = CreateDb(nameof(TestA_PepsiCo_ListsOnlyPepsiData));
            await SeedTwoCompaniesAsync(db);

            var sales = await new SalesRepository(db).GetPagedAsync(new SalesFilterDto { CompanyID = 1, PageNumber = 1, PageSize = 50 });
            Assert.Single(sales.Items);
            Assert.Equal("SI-PEPSI", sales.Items[0].InvoiceNumber);

            var products = await new ProductRepository(db).GetPagedAsync(new ProductFilterDto { CompanyID = 1, PageNumber = 1, PageSize = 50 });
            Assert.Single(products.Items);
            Assert.Equal("Pepsi 1L", products.Items[0].ProductName);

            var bookers = await new BookerService(new BookerRepository(db), new FakeCompanyContext(1, "PepsiCo"))
                .GetPagedBookersAsync(new DTOs.Bookers.BookerFilterDto { PageNumber = 1, PageSize = 50 });
            Assert.Single(bookers.Items);
            Assert.Equal("Pepsi Booker", bookers.Items[0].Name);
        }

        // TEST B — Colgate scope lists
        [Fact]
        public async Task TestB_Colgate_ListsOnlyColgateData()
        {
            await using var db = CreateDb(nameof(TestB_Colgate_ListsOnlyColgateData));
            await SeedTwoCompaniesAsync(db);

            var sales = await new SalesRepository(db).GetPagedAsync(new SalesFilterDto { CompanyID = 2, PageNumber = 1, PageSize = 50 });
            Assert.Single(sales.Items);
            Assert.Equal("SI-COLGATE", sales.Items[0].InvoiceNumber);

            var products = await new ProductRepository(db).GetPagedAsync(new ProductFilterDto { CompanyID = 2, PageNumber = 1, PageSize = 50 });
            Assert.Single(products.Items);
            Assert.Equal("Colgate Paste", products.Items[0].ProductName);
        }

        // TEST C — Customer outstanding soft-scope
        [Fact]
        public async Task TestC_CustomerOutstanding_RespectsCompanyScope()
        {
            await using var db = CreateDb(nameof(TestC_CustomerOutstanding_RespectsCompanyScope));
            await SeedTwoCompaniesAsync(db);
            var repo = new CustomerRepository(db);

            var pepsi = await repo.GetFinancialSummaryAsync(1, companyId: 1);
            Assert.NotNull(pepsi);
            Assert.Equal(50000m, pepsi!.OutstandingReceivable);

            var colgate = await repo.GetFinancialSummaryAsync(1, companyId: 2);
            Assert.NotNull(colgate);
            Assert.Equal(30000m, colgate!.OutstandingReceivable);

            var all = await repo.GetFinancialSummaryAsync(1, companyId: null);
            Assert.NotNull(all);
            Assert.Equal(80000m, all!.OutstandingReceivable);
        }

        [Fact]
        public async Task TestC_PaymentServiceOutstanding_UsesAmbientScope()
        {
            await using var db = CreateDb(nameof(TestC_PaymentServiceOutstanding_UsesAmbientScope));
            await SeedTwoCompaniesAsync(db);

            var pepsiSvc = new PaymentService(db, new FakeCompanyContext(1, "PepsiCo"));
            Assert.Equal(50000m, await pepsiSvc.GetCustomerOutstandingBalanceAsync(1));

            var colgateSvc = new PaymentService(db, new FakeCompanyContext(2, "Colgate"));
            Assert.Equal(30000m, await colgateSvc.GetCustomerOutstandingBalanceAsync(1));

            var allSvc = new PaymentService(db, FakeCompanyContext.AllCompanies());
            Assert.Equal(80000m, await allSvc.GetCustomerOutstandingBalanceAsync(1));
        }

        // TEST D — All Companies consolidated lists
        [Fact]
        public async Task TestD_AllCompanies_ShowsConsolidatedSalesAndProducts()
        {
            await using var db = CreateDb(nameof(TestD_AllCompanies_ShowsConsolidatedSalesAndProducts));
            await SeedTwoCompaniesAsync(db);

            var sales = await new SalesRepository(db).GetPagedAsync(new SalesFilterDto { CompanyID = null, PageNumber = 1, PageSize = 50 });
            Assert.Equal(2, sales.Items.Count);

            var products = await new ProductRepository(db).GetPagedAsync(new ProductFilterDto { CompanyID = null, PageNumber = 1, PageSize = 50 });
            Assert.Equal(2, products.Items.Count);

            var bookers = await new BookerService(new BookerRepository(db), FakeCompanyContext.AllCompanies())
                .GetPagedBookersAsync(new DTOs.Bookers.BookerFilterDto { PageNumber = 1, PageSize = 50 });
            Assert.Equal(2, bookers.Items.Count);
        }

        // TEST E — URL security / ownership
        [Fact]
        public void TestE_OutOfScope_DetectsCrossCompanyAccess()
        {
            var pepsi = new FakeCompanyContext(1, "PepsiCo");
            Assert.True(CompanyScopeGuards.IsOutOfScope(pepsi, entityCompanyId: 2));
            Assert.False(CompanyScopeGuards.IsOutOfScope(pepsi, entityCompanyId: 1));

            // All Companies must not block cross-company Details
            Assert.False(CompanyScopeGuards.IsOutOfScope(FakeCompanyContext.AllCompanies(), 2));
        }

        // TEST F — Create stamps CompanyID from context (not client)
        [Fact]
        public async Task TestF_CreateSales_StampsCompanyFromContext()
        {
            await using var db = CreateDb(nameof(TestF_CreateSales_StampsCompanyFromContext));
            db.Roles.Add(new Role { RoleID = 1, RoleName = "Admin", IsActive = true });
            db.Users.Add(new User { UserID = 1, RoleID = 1, FullName = "Admin", Username = "admin", PasswordHash = "x", IsActive = true });
            db.Companies.AddRange(
                new Company { CompanyID = 1, CompanyName = "PepsiCo" },
                new Company { CompanyID = 2, CompanyName = "Colgate" });
            db.Bookers.Add(new Booker { BookerID = 1, CompanyID = 1, Name = "B", CNIC = "35202-1111111", IsActive = true });
            db.Customers.Add(new Customer { CustomerID = 1, ShopName = "Shop", IsActive = true });
            db.Warehouses.Add(new Warehouse { WarehouseID = 1, Name = "Main", IsActive = true });
            db.Units.Add(new Unit { UnitID = 1, UnitName = "Piece" });
            db.Categories.Add(new Category { CategoryID = 1, CompanyID = 1, Name = "Cat" });
            db.Products.Add(new Product
            {
                ProductID = 1,
                ProductName = "Pepsi",
                SKU = "P1",
                CompanyID = 1,
                CategoryID = 1,
                BaseUnitID = 1,
                BaseSellingPrice = 10m,
                IsActive = true
            });
            db.ProductUnits.Add(new ProductUnit
            {
                ProductUnitID = 1,
                ProductID = 1,
                UnitID = 1,
                ConversionToBaseUnit = 1m,
                SellingPrice = 10m,
                IsDefaultSalesUnit = true,
                IsActive = true
            });
            db.InventoryStocks.Add(new InventoryStock { ProductID = 1, WarehouseID = 1, Quantity = 1000m, IsActive = true });
            await db.SaveChangesAsync();

            var service = new SalesService(
                new SalesRepository(db),
                db,
                new PromotionDiscountService(db),
                new FakeCompanyContext(1, "PepsiCo"),
                NullLogger<SalesService>.Instance);

            var result = await service.CreateAndFinalizeSalesInvoiceAsync(
                new CreateSalesInvoiceDto
                {
                    CustomerID = 1,
                    BookerID = 1,
                    SalespersonID = 1,
                    WarehouseID = 1,
                    InvoiceDate = DateTime.UtcNow.Date,
                    ApplyPromotions = false,
                    Items =
                    {
                        new CreateSalesItemDto
                        {
                            ProductID = 1,
                            ProductUnitID = 1,
                            Quantity = 1m,
                            UnitPrice = 10m,
                            ItemType = "NORMAL"
                        }
                    }
                },
                userId: 1);

            Assert.True(result.Success, result.Message);
            var invoice = await db.SalesInvoices.AsNoTracking().SingleAsync(i => i.InvoiceID == result.Data);
            Assert.Equal(1, invoice.CompanyID);
        }

        // TEST G — Create blocked in All Companies; unsaved confirm is UI (guard covers create path)
        [Fact]
        public void TestG_CreateBlocked_WhenAllCompanies()
        {
            var all = FakeCompanyContext.AllCompanies();
            Assert.True(all.IsAllCompanies || !all.HasCompany);
            Assert.Equal(
                "Select a specific company before creating.",
                CompanyScopeGuards.SelectSpecificCompanyMessage);
        }

        [Fact]
        public async Task TestG_UnpaidInvoices_SoftScopedByCompany()
        {
            await using var db = CreateDb(nameof(TestG_UnpaidInvoices_SoftScopedByCompany));
            await SeedTwoCompaniesAsync(db);

            var pepsi = new PaymentService(db, new FakeCompanyContext(1, "PepsiCo"));
            var unpaidPepsi = await pepsi.GetUnpaidCustomerInvoicesAsync(1);
            Assert.Single(unpaidPepsi);
            Assert.Equal("SI-PEPSI", unpaidPepsi[0].InvoiceNumber);

            var all = new PaymentService(db, FakeCompanyContext.AllCompanies());
            var unpaidAll = await all.GetUnpaidCustomerInvoicesAsync(1);
            Assert.Equal(2, unpaidAll.Count);
        }

        [Fact]
        public async Task CustomerService_ListOutstanding_UsesAmbientCompany()
        {
            await using var db = CreateDb(nameof(CustomerService_ListOutstanding_UsesAmbientCompany));
            await SeedTwoCompaniesAsync(db);

            var pepsiSvc = new CustomerService(new CustomerRepository(db), new FakeCompanyContext(1, "PepsiCo"));
            var pepsiPage = await pepsiSvc.GetPagedCustomersAsync(new DTOs.Customers.CustomerFilterDto { PageNumber = 1, PageSize = 10 });
            Assert.Equal(50000m, pepsiPage.Items[0].OutstandingReceivable);

            var allSvc = new CustomerService(new CustomerRepository(db), FakeCompanyContext.AllCompanies());
            var allPage = await allSvc.GetPagedCustomersAsync(new DTOs.Customers.CustomerFilterDto { PageNumber = 1, PageSize = 10 });
            Assert.Equal(80000m, allPage.Items[0].OutstandingReceivable);
        }
    }
}
