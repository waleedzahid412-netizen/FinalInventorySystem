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
    /// <summary>
    /// CREATE must ignore client FREE lines and rebuild promotions via
    /// PromotionDiscountService.EvaluatePromotionsAndDiscountsAsync (same as UPDATE).
    /// </summary>
    public class SalesCreatePromotionIntegrityTests
    {
        private ApplicationDbContext GetInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(
            SalesService SalesService,
            ApplicationDbContext Context,
            Product BuyProduct,
            Product FreeProduct,
            ProductUnit BuyUnit,
            ProductUnit FreeUnit,
            InventoryStock BuyStock,
            InventoryStock FreeStock,
            PromotionCampaign Campaign)> SetupPromoCatalogAsync(
            string dbName,
            int buyQuantity = 10,
            int freeQuantity = 2,
            decimal buyStockQty = 500m,
            decimal freeStockQty = 500m)
        {
            var context = GetInMemoryDbContext(dbName);
            var salesRepo = new SalesRepository(context);
            var promoService = new PromotionDiscountService(context);
            var salesService = new SalesService(salesRepo, context, promoService, new FakeCompanyContext(1, "Co"), NullLogger<SalesService>.Instance);

            var customer = new Customer { CustomerID = 1, ShopName = "Promo Shop", Address = "1 St", IsActive = true };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "Main Warehouse", IsActive = true };
            var unit = new Unit { UnitID = 1, UnitName = "PCS" };
            var category = new Category { CategoryID = 1, CompanyID = 1, Name = "Cat" };
            var company = new Company { CompanyID = 1, CompanyName = "Co" };
            var role = new Role { RoleID = 1, RoleName = "Admin", IsActive = true };
            var user = new User { UserID = 1, RoleID = 1, FullName = "Test User", Username = "test", PasswordHash = "x", IsActive = true };
            var booker = new Booker { BookerID = 1, CompanyID = 1, Name = "Test Booker", CNIC = "35202-1111111", IsActive = true };

            var buyProduct = new Product
            {
                ProductID = 1,
                ProductName = "Buy Product",
                SKU = "BUY-001",
                BaseSellingPrice = 10m,
                CategoryID = 1,
                CompanyID = 1,
                BaseUnitID = 1,
                IsActive = true
            };
            var freeProduct = new Product
            {
                ProductID = 2,
                ProductName = "Free Product",
                SKU = "FREE-001",
                BaseSellingPrice = 5m,
                CategoryID = 1,
                CompanyID = 1,
                BaseUnitID = 1,
                IsActive = true
            };

            var buyUnit = new ProductUnit
            {
                ProductUnitID = 1,
                ProductID = 1,
                UnitID = 1,
                ConversionToBaseUnit = 1,
                SellingPrice = 10m,
                IsDefaultSalesUnit = true,
                IsActive = true
            };
            var freeUnit = new ProductUnit
            {
                ProductUnitID = 2,
                ProductID = 2,
                UnitID = 1,
                ConversionToBaseUnit = 1,
                SellingPrice = 5m,
                IsDefaultSalesUnit = true,
                IsActive = true
            };

            var buyStock = new InventoryStock
            {
                ProductID = 1,
                WarehouseID = 1,
                Quantity = buyStockQty,
                DamagedQuantity = 0m,
                IsActive = true
            };
            var freeStock = new InventoryStock
            {
                ProductID = 2,
                WarehouseID = 1,
                Quantity = freeStockQty,
                DamagedQuantity = 0m,
                IsActive = true
            };

            var campaign = new PromotionCampaign
            {
                PromotionID = 1,
                Name = $"Buy {buyQuantity} Get {freeQuantity} Free",
                CompanyID = 1,
                StartDate = DateTime.UtcNow.AddDays(-30),
                EndDate = DateTime.UtcNow.AddDays(30),
                IsActive = true,
                CreatedBy = 1,
                PromotionRules = new List<PromotionRule>
                {
                    new PromotionRule
                    {
                        RuleID = 1,
                        PromotionID = 1,
                        BuyProductID = 1,
                        BuyQuantity = buyQuantity,
                        FreeProductID = 2,
                        FreeQuantity = freeQuantity
                    }
                }
            };

            context.Customers.Add(customer);
            context.Warehouses.Add(warehouse);
            context.Units.Add(unit);
            context.Categories.Add(category);
            context.Companies.Add(company);
            context.Roles.Add(role);
            context.Users.Add(user);
            context.Bookers.Add(booker);
            context.Products.AddRange(buyProduct, freeProduct);
            context.ProductUnits.AddRange(buyUnit, freeUnit);
            context.InventoryStocks.AddRange(buyStock, freeStock);
            context.PromotionCampaigns.Add(campaign);
            await context.SaveChangesAsync();

            return (salesService, context, buyProduct, freeProduct, buyUnit, freeUnit, buyStock, freeStock, campaign);
        }

        [Fact]
        public async Task A_UnauthorizedClientFreeLine_IsIgnored_AndDoesNotDeductInventory()
        {
            var (salesService, context, buyProduct, freeProduct, buyUnit, freeUnit, _, _, _) =
                await SetupPromoCatalogAsync(nameof(A_UnauthorizedClientFreeLine_IsIgnored_AndDoesNotDeductInventory),
                    buyQuantity: 10, freeQuantity: 2, buyStockQty: 100m, freeStockQty: 100m);

            // Cart does NOT qualify (only 1 paid). Client still injects 100 FREE.
            var result = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = buyProduct.ProductID,
                        ProductUnitID = buyUnit.ProductUnitID,
                        Quantity = 1m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    },
                    new CreateSalesItemDto
                    {
                        ProductID = freeProduct.ProductID,
                        ProductUnitID = freeUnit.ProductUnitID,
                        Quantity = 100m,
                        UnitPrice = 0m,
                        ItemType = "FREE",
                        PromotionID = 999
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var items = await context.SalesInvoiceItems.AsNoTracking()
                .Where(i => i.InvoiceID == result.Data && !i.IsDeleted)
                .ToListAsync();

            Assert.Single(items);
            Assert.Equal("NORMAL", items[0].ItemType);
            Assert.DoesNotContain(items, i => string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase));

            var freeStock = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == freeProduct.ProductID);
            Assert.Equal(100m, freeStock.Quantity);

            var buyStock = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == buyProduct.ProductID);
            Assert.Equal(99m, buyStock.Quantity);

            Assert.False(await context.InvoicePromotions.AnyAsync(p => p.InvoiceID == result.Data));
        }

        [Fact]
        public async Task B_FakeFreeConversion_DoesNotGrantFreeTreatment()
        {
            var (salesService, context, buyProduct, _, buyUnit, _, _, _, _) =
                await SetupPromoCatalogAsync(nameof(B_FakeFreeConversion_DoesNotGrantFreeTreatment),
                    buyQuantity: 10, freeQuantity: 2, buyStockQty: 100m, freeStockQty: 100m);

            // Paid line manipulated to ItemType=FREE — must not be treated as promotional free inventory.
            var result = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = buyProduct.ProductID,
                        ProductUnitID = buyUnit.ProductUnitID,
                        Quantity = 100m,
                        UnitPrice = 10m,
                        ItemType = "FREE",
                        PromotionID = 1
                    }
                }
            }, userId: 1);

            Assert.False(result.Success);
            Assert.Contains("paid product", result.Message, StringComparison.OrdinalIgnoreCase);

            Assert.False(await context.SalesInvoices.AnyAsync());
            Assert.False(await context.SalesInvoiceItems.AnyAsync());

            var buyStock = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == buyProduct.ProductID);
            Assert.Equal(100m, buyStock.Quantity);
        }

        [Fact]
        public async Task C_ValidPromotion_GeneratesAuthoritativeFreeItem()
        {
            var (salesService, context, buyProduct, freeProduct, buyUnit, freeUnit, _, _, campaign) =
                await SetupPromoCatalogAsync(nameof(C_ValidPromotion_GeneratesAuthoritativeFreeItem),
                    buyQuantity: 10, freeQuantity: 2, buyStockQty: 100m, freeStockQty: 100m);

            // Qualifying NORMAL cart only — no client FREE lines.
            var result = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow,
                ApplyPromotions = true,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = buyProduct.ProductID,
                        ProductUnitID = buyUnit.ProductUnitID,
                        Quantity = 10m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var items = await context.SalesInvoiceItems.AsNoTracking()
                .Where(i => i.InvoiceID == result.Data && !i.IsDeleted)
                .ToListAsync();

            Assert.Equal(2, items.Count);
            var normal = Assert.Single(items, i => i.ItemType == "NORMAL");
            var free = Assert.Single(items, i => i.ItemType == "FREE");

            Assert.Equal(10m, normal.Quantity);
            Assert.Equal(10m, normal.UnitPrice);
            Assert.Null(normal.PromotionID);

            Assert.Equal(freeProduct.ProductID, free.ProductID);
            Assert.Equal(freeUnit.ProductUnitID, free.ProductUnitID);
            Assert.Equal(2m, free.Quantity);
            Assert.Equal(0m, free.UnitPrice);
            Assert.Equal(campaign.PromotionID, free.PromotionID);

            var invoice = await context.SalesInvoices.AsNoTracking()
                .FirstAsync(i => i.InvoiceID == result.Data);
            Assert.Equal(100m, invoice.SubTotal);
            Assert.Equal(100m, invoice.GrandTotal);

            var buyStock = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == buyProduct.ProductID);
            var freeStock = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == freeProduct.ProductID);
            Assert.Equal(90m, buyStock.Quantity);
            Assert.Equal(98m, freeStock.Quantity);

            var promoSnap = Assert.Single(await context.InvoicePromotions.AsNoTracking()
                .Where(p => p.InvoiceID == result.Data).ToListAsync());
            Assert.Equal(campaign.PromotionID, promoSnap.PromotionID);
        }

        [Fact]
        public async Task D_ExcessFreeQuantity_UsesAuthoritativeRewardQuantity()
        {
            var (salesService, context, buyProduct, freeProduct, buyUnit, freeUnit, _, _, campaign) =
                await SetupPromoCatalogAsync(nameof(D_ExcessFreeQuantity_UsesAuthoritativeRewardQuantity),
                    buyQuantity: 10, freeQuantity: 2, buyStockQty: 100m, freeStockQty: 100m);

            var result = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = buyProduct.ProductID,
                        ProductUnitID = buyUnit.ProductUnitID,
                        Quantity = 10m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    },
                    new CreateSalesItemDto
                    {
                        ProductID = freeProduct.ProductID,
                        ProductUnitID = freeUnit.ProductUnitID,
                        Quantity = 50m, // client tries to inflate FREE qty
                        UnitPrice = 0m,
                        ItemType = "FREE",
                        PromotionID = campaign.PromotionID
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var free = Assert.Single(await context.SalesInvoiceItems.AsNoTracking()
                .Where(i => i.InvoiceID == result.Data && !i.IsDeleted && i.ItemType == "FREE")
                .ToListAsync());

            Assert.Equal(2m, free.Quantity);
            Assert.Equal(campaign.PromotionID, free.PromotionID);

            var freeStock = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == freeProduct.ProductID);
            Assert.Equal(98m, freeStock.Quantity); // only authoritative 2 deducted
        }

        [Fact]
        public async Task E_FakePromotionId_OnClientFreeLine_IsNotPersisted()
        {
            var (salesService, context, buyProduct, freeProduct, buyUnit, freeUnit, _, _, campaign) =
                await SetupPromoCatalogAsync(nameof(E_FakePromotionId_OnClientFreeLine_IsNotPersisted),
                    buyQuantity: 10, freeQuantity: 2, buyStockQty: 100m, freeStockQty: 100m);

            const int fakePromotionId = 99999;

            var result = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = buyProduct.ProductID,
                        ProductUnitID = buyUnit.ProductUnitID,
                        Quantity = 10m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    },
                    new CreateSalesItemDto
                    {
                        ProductID = freeProduct.ProductID,
                        ProductUnitID = freeUnit.ProductUnitID,
                        Quantity = 2m,
                        UnitPrice = 0m,
                        ItemType = "FREE",
                        PromotionID = fakePromotionId
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var free = Assert.Single(await context.SalesInvoiceItems.AsNoTracking()
                .Where(i => i.InvoiceID == result.Data && !i.IsDeleted && i.ItemType == "FREE")
                .ToListAsync());

            Assert.Equal(campaign.PromotionID, free.PromotionID);
            Assert.NotEqual(fakePromotionId, free.PromotionID!.Value);

            Assert.False(await context.InvoicePromotions.AnyAsync(p =>
                p.InvoiceID == result.Data && p.PromotionID == fakePromotionId));

            var promoSnap = Assert.Single(await context.InvoicePromotions.AsNoTracking()
                .Where(p => p.InvoiceID == result.Data).ToListAsync());
            Assert.Equal(campaign.PromotionID, promoSnap.PromotionID);
        }

        [Fact]
        public async Task F_ApplyPromotionsOn_QualifyingCart_GetsFreeLines()
        {
            var (salesService, context, buyProduct, freeProduct, buyUnit, _, _, _, campaign) =
                await SetupPromoCatalogAsync(nameof(F_ApplyPromotionsOn_QualifyingCart_GetsFreeLines),
                    buyQuantity: 10, freeQuantity: 2, buyStockQty: 100m, freeStockQty: 100m);

            var result = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow,
                ApplyPromotions = true,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = buyProduct.ProductID,
                        ProductUnitID = buyUnit.ProductUnitID,
                        Quantity = 10m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var free = Assert.Single(await context.SalesInvoiceItems.AsNoTracking()
                .Where(i => i.InvoiceID == result.Data && !i.IsDeleted && i.ItemType == "FREE")
                .ToListAsync());
            Assert.Equal(2m, free.Quantity);
            Assert.Equal(freeProduct.ProductID, free.ProductID);
            Assert.Equal(campaign.PromotionID, free.PromotionID);
            Assert.True(await context.InvoicePromotions.AnyAsync(p =>
                p.InvoiceID == result.Data && p.PromotionID == campaign.PromotionID));
        }

        [Fact]
        public async Task G_ApplyPromotionsOff_QualifyingCart_GetsNoFreeLinesOrSnapshots()
        {
            var (salesService, context, buyProduct, freeProduct, buyUnit, _, _, _, _) =
                await SetupPromoCatalogAsync(nameof(G_ApplyPromotionsOff_QualifyingCart_GetsNoFreeLinesOrSnapshots),
                    buyQuantity: 10, freeQuantity: 2, buyStockQty: 100m, freeStockQty: 100m);

            var result = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow,
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = buyProduct.ProductID,
                        ProductUnitID = buyUnit.ProductUnitID,
                        Quantity = 10m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var items = await context.SalesInvoiceItems.AsNoTracking()
                .Where(i => i.InvoiceID == result.Data && !i.IsDeleted)
                .ToListAsync();

            Assert.Single(items);
            Assert.Equal("NORMAL", items[0].ItemType);
            Assert.DoesNotContain(items, i => string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase));
            Assert.False(await context.InvoicePromotions.AnyAsync(p => p.InvoiceID == result.Data));

            var freeStock = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == freeProduct.ProductID);
            Assert.Equal(100m, freeStock.Quantity);

            var buyStock = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == buyProduct.ProductID);
            Assert.Equal(90m, buyStock.Quantity);

            var invoice = await context.SalesInvoices.AsNoTracking()
                .FirstAsync(i => i.InvoiceID == result.Data);
            Assert.Equal(100m, invoice.GrandTotal);
        }

        [Fact]
        public async Task H_ApplyPromotionsOff_WithClientFreeLines_StillDiscardsFree()
        {
            var (salesService, context, buyProduct, freeProduct, buyUnit, freeUnit, _, _, campaign) =
                await SetupPromoCatalogAsync(nameof(H_ApplyPromotionsOff_WithClientFreeLines_StillDiscardsFree),
                    buyQuantity: 10, freeQuantity: 2, buyStockQty: 100m, freeStockQty: 100m);

            var result = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow,
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = buyProduct.ProductID,
                        ProductUnitID = buyUnit.ProductUnitID,
                        Quantity = 10m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    },
                    new CreateSalesItemDto
                    {
                        ProductID = freeProduct.ProductID,
                        ProductUnitID = freeUnit.ProductUnitID,
                        Quantity = 50m,
                        UnitPrice = 0m,
                        ItemType = "FREE",
                        PromotionID = campaign.PromotionID
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var items = await context.SalesInvoiceItems.AsNoTracking()
                .Where(i => i.InvoiceID == result.Data && !i.IsDeleted)
                .ToListAsync();

            Assert.Single(items);
            Assert.Equal("NORMAL", items[0].ItemType);
            Assert.False(await context.InvoicePromotions.AnyAsync(p => p.InvoiceID == result.Data));

            var freeStock = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == freeProduct.ProductID);
            Assert.Equal(100m, freeStock.Quantity);
        }
    }
}
