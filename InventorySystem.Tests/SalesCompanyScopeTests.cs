using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Data;
using InventorySystem.DTOs.Sales;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class SalesCompanyScopeTests
    {
        private static ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(SalesService Service, ApplicationDbContext Db)> SetupAsync(string dbName, int scopeCompanyId = 1)
        {
            var db = CreateContext(dbName);
            db.Roles.Add(new Role { RoleID = 1, RoleName = "Admin", IsActive = true });
            db.Users.Add(new User { UserID = 1, RoleID = 1, FullName = "Admin", Username = "admin", PasswordHash = "x", IsActive = true });
            db.Companies.AddRange(
                new Company { CompanyID = 1, CompanyName = "Company A" },
                new Company { CompanyID = 2, CompanyName = "Company B" });
            db.Bookers.AddRange(
                new Booker { BookerID = 1, CompanyID = 1, Name = "Booker A", CNIC = "35202-1111111", IsActive = true },
                new Booker { BookerID = 2, CompanyID = 2, Name = "Booker B", CNIC = "35202-2222222", IsActive = true });
            db.Customers.Add(new Customer { CustomerID = 1, ShopName = "Shop", IsActive = true });
            db.Warehouses.Add(new Warehouse { WarehouseID = 1, Name = "Main", IsActive = true });
            db.Units.Add(new Unit { UnitID = 1, UnitName = "Piece" });
            db.Categories.AddRange(
                new Category { CategoryID = 1, CompanyID = 1, Name = "Cat A" },
                new Category { CategoryID = 2, CompanyID = 2, Name = "Cat B" });
            db.Products.AddRange(
                new Product { ProductID = 1, ProductName = "Prod A", SKU = "A", CompanyID = 1, CategoryID = 1, BaseUnitID = 1, BaseSellingPrice = 10m, IsActive = true },
                new Product { ProductID = 2, ProductName = "Prod B", SKU = "B", CompanyID = 2, CategoryID = 2, BaseUnitID = 1, BaseSellingPrice = 20m, IsActive = true });
            db.ProductUnits.AddRange(
                new ProductUnit { ProductUnitID = 1, ProductID = 1, UnitID = 1, ConversionToBaseUnit = 1m, SellingPrice = 10m, IsDefaultSalesUnit = true, IsActive = true },
                new ProductUnit { ProductUnitID = 2, ProductID = 2, UnitID = 1, ConversionToBaseUnit = 1m, SellingPrice = 20m, IsDefaultSalesUnit = true, IsActive = true });
            db.InventoryStocks.AddRange(
                new InventoryStock { ProductID = 1, WarehouseID = 1, Quantity = 1000m, IsActive = true },
                new InventoryStock { ProductID = 2, WarehouseID = 1, Quantity = 1000m, IsActive = true });
            await db.SaveChangesAsync();

            var service = new SalesService(
                new SalesRepository(db),
                db,
                new PromotionDiscountService(db),
                new FakeCompanyContext(scopeCompanyId, scopeCompanyId == 1 ? "Company A" : "Company B"),
                NullLogger<SalesService>.Instance);

            return (service, db);
        }

        private static CreateSalesInvoiceDto BuildCreate(int bookerId, params CreateSalesItemDto[] items) =>
            new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = bookerId,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow.Date,
                ApplyPromotions = false,
                Items = items.ToList()
            };

        private static CreateSalesItemDto Line(int productId, int unitId, decimal qty = 1m, decimal price = 10m) =>
            new CreateSalesItemDto
            {
                ProductID = productId,
                ProductUnitID = unitId,
                Quantity = qty,
                UnitPrice = price,
                ItemType = "NORMAL"
            };

        [Fact]
        public async Task Create_RejectsCrossCompanyLineItem()
        {
            var (service, _) = await SetupAsync(nameof(Create_RejectsCrossCompanyLineItem), scopeCompanyId: 1);
            var result = await service.CreateAndFinalizeSalesInvoiceAsync(
                BuildCreate(1, Line(1, 1), Line(2, 2, price: 20m)), 1);

            Assert.False(result.Success);
            Assert.Contains("different company", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Create_RejectsBookerCompanyMismatch()
        {
            var (service, _) = await SetupAsync(nameof(Create_RejectsBookerCompanyMismatch), scopeCompanyId: 1);
            var result = await service.CreateAndFinalizeSalesInvoiceAsync(
                BuildCreate(2, Line(1, 1)), 1);

            Assert.False(result.Success);
            Assert.Contains("booker", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Create_AllowsCustomFreeItem_WithoutProductCompanyCheck()
        {
            var (service, db) = await SetupAsync(nameof(Create_AllowsCustomFreeItem_WithoutProductCompanyCheck), scopeCompanyId: 1);

            db.PromotionCampaigns.Add(new PromotionCampaign
            {
                PromotionID = 1,
                Name = "Custom Free Promo",
                CompanyID = 1,
                IsActive = true,
                StartDate = DateTime.UtcNow.Date.AddDays(-1),
                EndDate = DateTime.UtcNow.Date.AddDays(30),
                CreatedBy = 1,
                PromotionRules = new List<PromotionRule>
                {
                    new PromotionRule
                    {
                        RuleID = 1,
                        PromotionID = 1,
                        BuyProductID = 1,
                        BuyQuantity = 1,
                        FreeProductID = null,
                        FreeQuantity = 1,
                        IsCustomFreeItem = true,
                        CustomFreeItemName = "Promo Mug"
                    }
                }
            });
            await db.SaveChangesAsync();

            var dto = BuildCreate(1, Line(1, 1));
            dto.ApplyPromotions = true;
            var create = await service.CreateAndFinalizeSalesInvoiceAsync(dto, 1);
            Assert.True(create.Success, create.Message);

            var free = await db.SalesInvoiceItems.SingleOrDefaultAsync(i => i.InvoiceID == create.Data && i.ItemType == "FREE");
            Assert.NotNull(free);
            Assert.Null(free!.ProductID);
            Assert.Equal(1, (await db.SalesInvoices.SingleAsync(i => i.InvoiceID == create.Data)).CompanyID);
        }

        [Fact]
        public async Task Update_RejectsCrossCompanyLineItem()
        {
            var (service, _) = await SetupAsync(nameof(Update_RejectsCrossCompanyLineItem), scopeCompanyId: 1);
            var create = await service.CreateAndFinalizeSalesInvoiceAsync(BuildCreate(1, Line(1, 1)), 1);
            Assert.True(create.Success, create.Message);

            var update = await service.UpdateSalesInvoiceAsync(new UpdateSalesInvoiceDto
            {
                InvoiceID = create.Data,
                BookerID = 1,
                SalespersonID = 1,
                InvoiceDate = DateTime.UtcNow.Date,
                EditReason = "Try cross-company",
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto> { Line(1, 1), Line(2, 2, price: 20m) }
            }, 1);

            Assert.False(update.Success);
            Assert.Contains("different company", update.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Update_RejectsBookerFromOtherCompany()
        {
            var (service, _) = await SetupAsync(nameof(Update_RejectsBookerFromOtherCompany), scopeCompanyId: 1);
            var create = await service.CreateAndFinalizeSalesInvoiceAsync(BuildCreate(1, Line(1, 1)), 1);
            Assert.True(create.Success, create.Message);

            var update = await service.UpdateSalesInvoiceAsync(new UpdateSalesInvoiceDto
            {
                InvoiceID = create.Data,
                BookerID = 2,
                SalespersonID = 1,
                InvoiceDate = DateTime.UtcNow.Date,
                EditReason = "Wrong booker",
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto> { Line(1, 1) }
            }, 1);

            Assert.False(update.Success);
            Assert.Contains("booker", update.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Update_CompanyRemainsImmutable()
        {
            var (service, db) = await SetupAsync(nameof(Update_CompanyRemainsImmutable), scopeCompanyId: 1);
            var create = await service.CreateAndFinalizeSalesInvoiceAsync(BuildCreate(1, Line(1, 1)), 1);
            Assert.True(create.Success, create.Message);

            var update = await service.UpdateSalesInvoiceAsync(new UpdateSalesInvoiceDto
            {
                InvoiceID = create.Data,
                BookerID = 1,
                SalespersonID = 1,
                InvoiceDate = DateTime.UtcNow.Date,
                EditReason = "Normal edit",
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto> { Line(1, 1, qty: 2m) }
            }, 1);

            Assert.True(update.Success, update.Message);
            var invoice = await db.SalesInvoices.AsNoTracking().SingleAsync(i => i.InvoiceID == create.Data);
            Assert.Equal(1, invoice.CompanyID);
        }
    }
}
