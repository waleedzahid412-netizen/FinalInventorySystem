using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Data;
using InventorySystem.DTOs.Purchases;
using InventorySystem.DTOs.Sales;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    /// <summary>
    /// End-to-end FIFO flows through PurchaseService and SalesService (BR-004).
    /// </summary>
    public class FifoIntegrationTests
    {
        private static ApplicationDbContext CreateContext(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private sealed class FifoTestHarness
        {
            public ApplicationDbContext Context { get; }
            public PurchaseService Purchase { get; }
            public SalesService Sales { get; }
            public ProductUnit PieceUnit { get; }
            public ProductUnit CartonUnit { get; }

            private FifoTestHarness(
                ApplicationDbContext context,
                PurchaseService purchase,
                SalesService sales,
                ProductUnit pieceUnit,
                ProductUnit cartonUnit)
            {
                Context = context;
                Purchase = purchase;
                Sales = sales;
                PieceUnit = pieceUnit;
                CartonUnit = cartonUnit;
            }

            public static async Task<FifoTestHarness> CreateAsync(string dbName, decimal cartonConversion = 24m)
            {
                var context = CreateContext(dbName);
                var fifo = TestFifoHelper.CreateFifo(context);
                var purchaseService = new PurchaseService(new PurchaseRepository(context), context, fifo);
                var salesService = new SalesService(
                    new SalesRepository(context),
                    context,
                    new PromotionDiscountService(context),
                    new FakeCompanyContext(1, "Co"),
                    fifo,
                    NullLogger<SalesService>.Instance);

                context.Companies.Add(new Company { CompanyID = 1, CompanyName = "Co" });
                context.Warehouses.Add(new Warehouse { WarehouseID = 1, Name = "Main", IsActive = true });
                context.Units.AddRange(
                    new Unit { UnitID = 1, UnitName = "Piece" },
                    new Unit { UnitID = 2, UnitName = "Carton" });
                context.Categories.Add(new Category { CategoryID = 1, CompanyID = 1, Name = "Cat" });
                context.Roles.Add(new Role { RoleID = 1, RoleName = "Admin", IsActive = true });
                context.Users.Add(new User { UserID = 1, RoleID = 1, FullName = "U", Username = "u", PasswordHash = "x", IsActive = true });
                context.Customers.Add(new Customer { CustomerID = 1, ShopName = "Shop", Address = "A", IsActive = true });
                context.Bookers.Add(new Booker { BookerID = 1, CompanyID = 1, Name = "B", CNIC = "35202-1111111-1", IsActive = true });
                context.Products.Add(new Product
                {
                    ProductID = 1,
                    ProductName = "Shampoo",
                    SKU = "SH-1",
                    CategoryID = 1,
                    CompanyID = 1,
                    BaseUnitID = 1,
                    BaseSellingPrice = 20m,
                    IsActive = true
                });

                var pieceUnit = new ProductUnit
                {
                    ProductUnitID = 1,
                    ProductID = 1,
                    UnitID = 1,
                    ConversionToBaseUnit = 1m,
                    SellingPrice = 20m,
                    PurchasePrice = 15m,
                    IsDefaultSalesUnit = true,
                    IsActive = true
                };
                var cartonUnit = new ProductUnit
                {
                    ProductUnitID = 2,
                    ProductID = 1,
                    UnitID = 2,
                    ConversionToBaseUnit = cartonConversion,
                    SellingPrice = 480m,
                    PurchasePrice = 360m,
                    IsDefaultPurchaseUnit = true,
                    IsActive = true
                };
                context.ProductUnits.AddRange(pieceUnit, cartonUnit);
                await context.SaveChangesAsync();

                return new FifoTestHarness(context, purchaseService, salesService, pieceUnit, cartonUnit);
            }
        }

        [Fact]
        public async Task PurchaseFinalize_CreatesCostLayer_WithCorrectBaseUnitCost()
        {
            var h = await FifoTestHarness.CreateAsync(nameof(PurchaseFinalize_CreatesCostLayer_WithCorrectBaseUnitCost));

            var result = await h.Purchase.CreateAndFinalizePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto
            {
                CompanyID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.Today.AddDays(-2),
                Items = new List<CreatePurchaseItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.CartonUnit.ProductUnitID, Quantity = 2m, UnitCost = 360m }
                }
            }, userId: 1);

            Assert.True(result.Success);

            var layer = await h.Context.InventoryCostLayers.SingleAsync();
            Assert.Equal(48m, layer.OriginalQuantity); // 2 cartons × 24 pieces
            Assert.Equal(48m, layer.RemainingQuantity);
            Assert.Equal(15m, layer.UnitCostInBase); // 360 / 24
            Assert.Equal("PURCHASE", layer.SourceType);
        }

        [Fact]
        public async Task TwoPurchasesThenSale_UsesFifoOrder_For850Cogs()
        {
            var h = await FifoTestHarness.CreateAsync(nameof(TwoPurchasesThenSale_UsesFifoOrder_For850Cogs));

            // Batch 1: 50 pieces @ Rs 15 (buy as 50 pieces)
            Assert.True((await h.Purchase.CreateAndFinalizePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto
            {
                CompanyID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.Today.AddDays(-10),
                Items = new List<CreatePurchaseItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 50m, UnitCost = 15m }
                }
            }, 1)).Success);

            // Batch 2: 100 pieces @ Rs 10
            Assert.True((await h.Purchase.CreateAndFinalizePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto
            {
                CompanyID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.Today.AddDays(-5),
                Items = new List<CreatePurchaseItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 100m, UnitCost = 10m }
                }
            }, 1)).Success);

            // Sell 60 pieces
            var saleResult = await h.Sales.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.Today,
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 60m, UnitPrice = 20m, ItemType = "NORMAL" }
                }
            }, userId: 1);

            Assert.True(saleResult.Success);

            var saleLine = await h.Context.SalesInvoiceItems.SingleAsync();
            Assert.Equal(850m, saleLine.CostOfGoodsSold); // 50×15 + 10×10
            Assert.Equal(850m / 60m, saleLine.UnitCostInBase);

            var layers = await h.Context.InventoryCostLayers.OrderBy(l => l.ReceivedAt).ToListAsync();
            Assert.Equal(0m, layers[0].RemainingQuantity);
            Assert.Equal(90m, layers[1].RemainingQuantity);

            var consumptions = await h.Context.InventoryCostConsumptions.Where(c => c.TransactionType == "SALE").ToListAsync();
            Assert.Equal(2, consumptions.Count);
            Assert.Contains(consumptions, c => c.Quantity == 50m && c.UnitCostInBase == 15m);
            Assert.Contains(consumptions, c => c.Quantity == 10m && c.UnitCostInBase == 10m);
        }

        [Fact]
        public async Task SaleEdit_ReversesAndReappliesFifoConsumptions()
        {
            var h = await FifoTestHarness.CreateAsync(nameof(SaleEdit_ReversesAndReappliesFifoConsumptions));

            Assert.True((await h.Purchase.CreateAndFinalizePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto
            {
                CompanyID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.Today.AddDays(-3),
                Items = new List<CreatePurchaseItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 100m, UnitCost = 12m }
                }
            }, 1)).Success);

            var create = await h.Sales.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 30m, UnitPrice = 20m, ItemType = "NORMAL" }
                }
            }, 1);
            Assert.True(create.Success);

            var layerBeforeEdit = await h.Context.InventoryCostLayers.SingleAsync();
            Assert.Equal(70m, layerBeforeEdit.RemainingQuantity);

            var update = await h.Sales.UpdateSalesInvoiceAsync(new UpdateSalesInvoiceDto
            {
                InvoiceID = create.Data,
                InvoiceDate = DateTime.Today,
                BookerID = 1,
                SalespersonID = 1,
                EditReason = "Qty correction",
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 10m, UnitPrice = 20m, ItemType = "NORMAL" }
                }
            }, userId: 1);
            Assert.True(update.Success);

            var activeLine = await h.Context.SalesInvoiceItems.Where(i => !i.IsDeleted).SingleAsync();
            Assert.Equal(120m, activeLine.CostOfGoodsSold); // 10 × 12

            var layerAfterEdit = await h.Context.InventoryCostLayers.SingleAsync();
            Assert.Equal(90m, layerAfterEdit.RemainingQuantity);
        }

        [Fact]
        public async Task PurchaseEdit_BlockedWhenBatchAlreadySold()
        {
            var h = await FifoTestHarness.CreateAsync(nameof(PurchaseEdit_BlockedWhenBatchAlreadySold));

            var purchase = await h.Purchase.CreateAndFinalizePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto
            {
                CompanyID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.Today.AddDays(-2),
                Items = new List<CreatePurchaseItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 50m, UnitCost = 15m }
                }
            }, 1);
            Assert.True(purchase.Success);

            Assert.True((await h.Sales.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 20m, UnitPrice = 20m, ItemType = "NORMAL" }
                }
            }, 1)).Success);

            var edit = await h.Purchase.UpdatePurchaseInvoiceAsync(new UpdatePurchaseInvoiceDto
            {
                PurchaseInvoiceID = purchase.Data,
                InvoiceDate = DateTime.Today,
                EditReason = "Cost fix",
                Items = new List<CreatePurchaseItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 50m, UnitCost = 14m }
                }
            }, userId: 1);

            Assert.False(edit.Success);
            Assert.Contains("already sold", edit.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PurchaseEdit_SucceedsWhenNothingSold()
        {
            var h = await FifoTestHarness.CreateAsync(nameof(PurchaseEdit_SucceedsWhenNothingSold));

            var purchase = await h.Purchase.CreateAndFinalizePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto
            {
                CompanyID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.Today.AddDays(-2),
                Items = new List<CreatePurchaseItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 50m, UnitCost = 15m }
                }
            }, 1);
            Assert.True(purchase.Success);

            var edit = await h.Purchase.UpdatePurchaseInvoiceAsync(new UpdatePurchaseInvoiceDto
            {
                PurchaseInvoiceID = purchase.Data,
                InvoiceDate = DateTime.Today,
                EditReason = "Cost correction",
                Items = new List<CreatePurchaseItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 50m, UnitCost = 14m }
                }
            }, userId: 1);

            Assert.True(edit.Success);

            var stock = await h.Context.InventoryStocks.SingleAsync();
            Assert.Equal(50m, stock.Quantity);

            var layer = await h.Context.InventoryCostLayers.SingleAsync(l => !l.IsDeleted);
            Assert.Equal(50m, layer.RemainingQuantity);
            Assert.Equal(14m, layer.UnitCostInBase);

            var activeLine = await h.Context.PurchaseInvoiceItems.SingleAsync(i => !i.IsDeleted);
            Assert.Equal(14m, activeLine.UnitCost);
        }

        [Fact]
        public async Task PurchaseEdit_BlockedWhenStockBelowReversalQty()
        {
            var h = await FifoTestHarness.CreateAsync(nameof(PurchaseEdit_BlockedWhenStockBelowReversalQty));

            var purchase = await h.Purchase.CreateAndFinalizePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto
            {
                CompanyID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.Today.AddDays(-2),
                Items = new List<CreatePurchaseItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 50m, UnitCost = 15m }
                }
            }, 1);
            Assert.True(purchase.Success);

            var stock = await h.Context.InventoryStocks.SingleAsync();
            stock.Quantity = 20m;
            await h.Context.SaveChangesAsync();

            var edit = await h.Purchase.UpdatePurchaseInvoiceAsync(new UpdatePurchaseInvoiceDto
            {
                PurchaseInvoiceID = purchase.Data,
                InvoiceDate = DateTime.Today,
                EditReason = "Cost fix",
                Items = new List<CreatePurchaseItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 50m, UnitCost = 14m }
                }
            }, userId: 1);

            Assert.False(edit.Success);
            Assert.Contains("Insufficient warehouse stock", edit.Message, StringComparison.OrdinalIgnoreCase);

            var stockAfter = await h.Context.InventoryStocks.SingleAsync();
            Assert.Equal(20m, stockAfter.Quantity);
        }

        [Fact]
        public async Task PurchaseEdit_BlockedWhenCostLayerMissing()
        {
            var h = await FifoTestHarness.CreateAsync(nameof(PurchaseEdit_BlockedWhenCostLayerMissing));

            var purchaseItem = new PurchaseInvoiceItem
            {
                ProductID = 1,
                ProductUnitID = h.PieceUnit.ProductUnitID,
                Quantity = 50m,
                ConvertedQuantity = 50m,
                UnitCost = 15m,
                TotalCost = 750m,
                IsDeleted = false
            };
            var purchaseInv = new PurchaseInvoice
            {
                CompanyID = 1,
                WarehouseID = 1,
                InvoiceNumber = "PINV-NO-LAYER",
                InvoiceDate = DateTime.Today.AddDays(-2),
                SubTotal = 750m,
                GrandTotal = 750m,
                CreatedBy = 1,
                IsDeleted = false,
                Items = new List<PurchaseInvoiceItem> { purchaseItem }
            };
            h.Context.PurchaseInvoices.Add(purchaseInv);
            h.Context.InventoryStocks.Add(new InventoryStock
            {
                ProductID = 1,
                WarehouseID = 1,
                Quantity = 30m,
                IsActive = true
            });
            await h.Context.SaveChangesAsync();

            var edit = await h.Purchase.UpdatePurchaseInvoiceAsync(new UpdatePurchaseInvoiceDto
            {
                PurchaseInvoiceID = purchaseInv.PurchaseInvoiceID,
                InvoiceDate = DateTime.Today,
                EditReason = "Cost fix",
                Items = new List<CreatePurchaseItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = h.PieceUnit.ProductUnitID, Quantity = 50m, UnitCost = 14m }
                }
            }, userId: 1);

            Assert.False(edit.Success);
            Assert.Contains("no FIFO cost layer", edit.Message, StringComparison.OrdinalIgnoreCase);

            var stockAfter = await h.Context.InventoryStocks.SingleAsync();
            Assert.Equal(30m, stockAfter.Quantity);
        }

        [Fact]
        public async Task Backfill_ReplaysHistoricalSales_AndSetsCogs()
        {
            var h = await FifoTestHarness.CreateAsync(nameof(Backfill_ReplaysHistoricalSales_AndSetsCogs));

            // Simulate legacy data: purchase + sale without FIFO run
            var purchaseItem = new PurchaseInvoiceItem
            {
                ProductID = 1,
                ProductUnitID = h.PieceUnit.ProductUnitID,
                Quantity = 100m,
                ConvertedQuantity = 100m,
                UnitCost = 8m,
                TotalCost = 800m,
                IsDeleted = false
            };
            var purchaseInv = new PurchaseInvoice
            {
                CompanyID = 1,
                WarehouseID = 1,
                InvoiceNumber = "PINV-LEGACY-1",
                InvoiceDate = DateTime.Today.AddDays(-7),
                SubTotal = 800m,
                GrandTotal = 800m,
                CreatedBy = 1,
                IsDeleted = false,
                Items = new List<PurchaseInvoiceItem> { purchaseItem }
            };
            h.Context.PurchaseInvoices.Add(purchaseInv);
            await h.Context.SaveChangesAsync();

            var saleItem = new SalesInvoiceItem
            {
                ProductID = 1,
                ProductUnitID = h.PieceUnit.ProductUnitID,
                Quantity = 25m,
                ConvertedQuantity = 25m,
                UnitPrice = 20m,
                ItemType = "NORMAL",
                IsDeleted = false
            };
            var saleInv = new SalesInvoice
            {
                CustomerID = 1,
                CompanyID = 1,
                BookerID = 1,
                WarehouseID = 1,
                CreatedBy = 1,
                InvoiceNumber = "SINV-LEGACY-1",
                InvoiceDate = DateTime.Today.AddDays(-1),
                SubTotal = 500m,
                GrandTotal = 500m,
                IsDeleted = false,
                Items = new List<SalesInvoiceItem> { saleItem }
            };
            h.Context.SalesInvoices.Add(saleInv);
            h.Context.InventoryStocks.Add(new InventoryStock { ProductID = 1, WarehouseID = 1, Quantity = 75m, IsActive = true });
            await h.Context.SaveChangesAsync();

            var fifo = TestFifoHelper.CreateFifo(h.Context);
            await fifo.BackfillHistoricalDataAsync();

            var updatedSaleLine = await h.Context.SalesInvoiceItems.SingleAsync();
            Assert.Equal(200m, updatedSaleLine.CostOfGoodsSold); // 25 × 8

            var layer = await h.Context.InventoryCostLayers.SingleAsync();
            Assert.Equal(75m, layer.RemainingQuantity);
        }

        [Fact]
        public async Task GetInventoryValue_SumsOpenLayers()
        {
            var h = await FifoTestHarness.CreateAsync(nameof(GetInventoryValue_SumsOpenLayers));
            var fifo = TestFifoHelper.CreateFifo(h.Context);

            await h.Context.InventoryCostLayers.AddRangeAsync(
                new InventoryCostLayer
                {
                    ProductID = 1, WarehouseID = 1, ReceivedAt = DateTime.UtcNow,
                    SourceType = "PURCHASE", OriginalQuantity = 10, RemainingQuantity = 10,
                    UnitCostInBase = 15m, IsDeleted = false
                },
                new InventoryCostLayer
                {
                    ProductID = 1, WarehouseID = 1, ReceivedAt = DateTime.UtcNow.AddMinutes(1),
                    SourceType = "PURCHASE", OriginalQuantity = 20, RemainingQuantity = 20,
                    UnitCostInBase = 10m, IsDeleted = false
                });
            await h.Context.SaveChangesAsync();

            decimal value = await fifo.GetInventoryValueAsync(companyId: 1, warehouseId: 1);
            Assert.Equal(10m * 15m + 20m * 10m, value); // 350
        }
    }
}
