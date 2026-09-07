using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Data;
using InventorySystem.Models.Entities;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class FifoCostingServiceTests
    {
        private static ApplicationDbContext CreateContext(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task ConsumeForSale_UsesOldestLayerFirst()
        {
            await using var db = CreateContext(nameof(ConsumeForSale_UsesOldestLayerFirst));
            var fifo = new FifoCostingService(db, NullLogger<FifoCostingService>.Instance);

            int productId = 1, warehouseId = 1, userId = 1;

            await db.InventoryCostLayers.AddRangeAsync(
                new InventoryCostLayer
                {
                    ProductID = productId,
                    WarehouseID = warehouseId,
                    ReceivedAt = DateTime.UtcNow.AddDays(-2),
                    SourceType = "PURCHASE",
                    OriginalQuantity = 50,
                    RemainingQuantity = 50,
                    UnitCostInBase = 15m,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                },
                new InventoryCostLayer
                {
                    ProductID = productId,
                    WarehouseID = warehouseId,
                    ReceivedAt = DateTime.UtcNow.AddDays(-1),
                    SourceType = "PURCHASE",
                    OriginalQuantity = 100,
                    RemainingQuantity = 100,
                    UnitCostInBase = 10m,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                });
            await db.SaveChangesAsync();

            var result = await fifo.ConsumeForSaleAsync(productId, warehouseId, 60, salesInvoiceItemId: 99, userId);

            Assert.True(result.Success);
            Assert.Equal(50m * 15m + 10m * 10m, result.TotalCost); // 850
            Assert.Equal(850m / 60m, result.UnitCostInBase);

            var layers = await db.InventoryCostLayers.OrderBy(l => l.ReceivedAt).ToListAsync();
            Assert.Equal(0m, layers[0].RemainingQuantity);
            Assert.Equal(90m, layers[1].RemainingQuantity);
        }

        [Fact]
        public async Task RestoreConsumptionsForSalesItem_RestoresLayerQuantities()
        {
            await using var db = CreateContext(nameof(RestoreConsumptionsForSalesItem_RestoresLayerQuantities));
            var fifo = new FifoCostingService(db, NullLogger<FifoCostingService>.Instance);

            var layer = new InventoryCostLayer
            {
                ProductID = 1,
                WarehouseID = 1,
                ReceivedAt = DateTime.UtcNow,
                SourceType = "PURCHASE",
                OriginalQuantity = 100,
                RemainingQuantity = 40,
                UnitCostInBase = 12m,
                IsDeleted = false
            };
            await db.InventoryCostLayers.AddAsync(layer);
            await db.InventoryCostConsumptions.AddAsync(new InventoryCostConsumption
            {
                CostLayer = layer,
                SalesInvoiceItemID = 5,
                TransactionType = "SALE",
                Quantity = 60,
                UnitCostInBase = 12m,
                TotalCost = 720m,
                CreatedBy = 1,
                IsDeleted = false
            });
            await db.SaveChangesAsync();

            await fifo.RestoreConsumptionsForSalesItemAsync(5, userId: 1);

            var updatedLayer = await db.InventoryCostLayers.SingleAsync();
            Assert.Equal(100m, updatedLayer.RemainingQuantity);
        }

        [Fact]
        public async Task ReversePurchaseLayer_FailsWhenCostLayerMissing()
        {
            await using var db = CreateContext(nameof(ReversePurchaseLayer_FailsWhenCostLayerMissing));
            var fifo = new FifoCostingService(db, NullLogger<FifoCostingService>.Instance);

            var result = await fifo.ReversePurchaseLayerAsync(
                purchaseInvoiceItemId: 999,
                quantityBase: 10m,
                userId: 1);

            Assert.False(result.Success);
            Assert.Contains("no FIFO cost layer", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
