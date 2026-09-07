using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.Models.Entities;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class FifoCostingService : IFifoCostingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FifoCostingService> _logger;

        public FifoCostingService(ApplicationDbContext context, ILogger<FifoCostingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task CreateLayerFromPurchaseAsync(
            PurchaseInvoiceItem item,
            int warehouseId,
            DateTime receivedAt,
            decimal conversionToBaseUnit,
            int userId,
            CancellationToken cancellationToken = default)
        {
            decimal unitCostInBase = conversionToBaseUnit > 0
                ? Math.Round(item.UnitCost / conversionToBaseUnit, 4, MidpointRounding.AwayFromZero)
                : item.UnitCost;

            var layer = new InventoryCostLayer
            {
                ProductID = item.ProductID,
                WarehouseID = warehouseId,
                PurchaseInvoiceItemID = item.PurchaseItemID,
                ReceivedAt = receivedAt,
                SourceType = "PURCHASE",
                OriginalQuantity = item.ConvertedQuantity,
                RemainingQuantity = item.ConvertedQuantity,
                UnitCostInBase = unitCostInBase,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                IsDeleted = false
            };

            await _context.InventoryCostLayers.AddAsync(layer, cancellationToken);
            await RefreshProductAverageCostAsync(item.ProductID, cancellationToken);
        }

        public async Task<FifoConsumptionResult> ConsumeForSaleAsync(
            int productId,
            int warehouseId,
            decimal quantityBase,
            int salesInvoiceItemId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (quantityBase <= 0)
            {
                return FifoConsumptionResult.Ok(0m, 0m);
            }

            var layers = await _context.InventoryCostLayers
                .Where(l => l.ProductID == productId
                    && l.WarehouseID == warehouseId
                    && l.RemainingQuantity > 0)
                .OrderBy(l => l.ReceivedAt)
                .ThenBy(l => l.CostLayerID)
                .ToListAsync(cancellationToken);

            decimal remainingToConsume = quantityBase;
            decimal totalCost = 0m;
            var now = DateTime.UtcNow;

            foreach (var layer in layers)
            {
                if (remainingToConsume <= 0)
                {
                    break;
                }

                decimal take = Math.Min(layer.RemainingQuantity, remainingToConsume);
                decimal lineCost = Math.Round(take * layer.UnitCostInBase, 2, MidpointRounding.AwayFromZero);

                layer.RemainingQuantity -= take;
                totalCost += lineCost;
                remainingToConsume -= take;

                var consumption = new InventoryCostConsumption
                {
                    CostLayerID = layer.CostLayerID,
                    SalesInvoiceItemID = salesInvoiceItemId,
                    TransactionType = "SALE",
                    Quantity = take,
                    UnitCostInBase = layer.UnitCostInBase,
                    TotalCost = lineCost,
                    CreatedAt = now,
                    CreatedBy = userId,
                    IsDeleted = false
                };
                await _context.InventoryCostConsumptions.AddAsync(consumption, cancellationToken);
            }

            if (remainingToConsume > 0)
            {
                var product = await _context.Products.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ProductID == productId, cancellationToken);
                decimal fallbackCost = product?.AveragePurchaseCost ?? 0m;

                decimal fallbackTotal = Math.Round(remainingToConsume * fallbackCost, 2, MidpointRounding.AwayFromZero);
                totalCost += fallbackTotal;

                if (fallbackCost <= 0)
                {
                    _logger.LogWarning(
                        "FIFO layer shortage for product #{ProductId}: no layers for {Qty} base units; COGS recorded as zero.",
                        productId, remainingToConsume);
                }
                else
                {
                    _logger.LogWarning(
                        "FIFO layer shortage for product #{ProductId}: used AveragePurchaseCost fallback for {Qty} base units",
                        productId, remainingToConsume);
                }
            }

            await RefreshProductAverageCostAsync(productId, cancellationToken);
            return FifoConsumptionResult.Ok(totalCost, quantityBase);
        }

        public async Task RestoreConsumptionsForSalesItemAsync(
            int salesInvoiceItemId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            var consumptions = await _context.InventoryCostConsumptions
                .Include(c => c.CostLayer)
                .Where(c => c.SalesInvoiceItemID == salesInvoiceItemId
                    && c.TransactionType == "SALE"
                    && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .ThenByDescending(c => c.ConsumptionID)
                .ToListAsync(cancellationToken);

            if (!consumptions.Any())
            {
                return;
            }

            var now = DateTime.UtcNow;
            int? productId = null;

            foreach (var consumption in consumptions)
            {
                if (consumption.CostLayer != null)
                {
                    consumption.CostLayer.RemainingQuantity += consumption.Quantity;
                    productId = consumption.CostLayer.ProductID;
                }

                consumption.IsDeleted = true;
                consumption.DeletedAt = now;

                var reversal = new InventoryCostConsumption
                {
                    CostLayerID = consumption.CostLayerID,
                    SalesInvoiceItemID = salesInvoiceItemId,
                    TransactionType = "REVERSAL",
                    Quantity = -consumption.Quantity,
                    UnitCostInBase = consumption.UnitCostInBase,
                    TotalCost = -consumption.TotalCost,
                    CreatedAt = now,
                    CreatedBy = userId,
                    IsDeleted = false
                };
                await _context.InventoryCostConsumptions.AddAsync(reversal, cancellationToken);
            }

            if (productId.HasValue)
            {
                await RefreshProductAverageCostAsync(productId.Value, cancellationToken);
            }
        }

        public async Task<OperationResult> ReversePurchaseLayerAsync(
            int purchaseInvoiceItemId,
            decimal quantityBase,
            int userId,
            CancellationToken cancellationToken = default)
        {
            var layer = await _context.InventoryCostLayers
                .FirstOrDefaultAsync(l => l.PurchaseInvoiceItemID == purchaseInvoiceItemId && !l.IsDeleted, cancellationToken);

            if (layer == null)
            {
                return OperationResult.Fail("Cannot reverse purchase line: no FIFO cost layer found.");
            }

            if (layer.RemainingQuantity < quantityBase)
            {
                return OperationResult.Fail(
                    $"Cannot reverse purchase line: {quantityBase - layer.RemainingQuantity:N3} base units from this batch were already sold.");
            }

            layer.RemainingQuantity -= quantityBase;
            if (layer.RemainingQuantity <= 0)
            {
                layer.IsDeleted = true;
                layer.DeletedAt = DateTime.UtcNow;
            }

            await RefreshProductAverageCostAsync(layer.ProductID, cancellationToken);
            return OperationResult.Ok("Purchase cost layer reversed.");
        }

        public async Task RestoreSellableReturnAsync(
            int productId,
            int warehouseId,
            decimal quantityBase,
            decimal unitCostInBase,
            int salesReturnItemId,
            DateTime receivedAt,
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (quantityBase <= 0)
            {
                return;
            }

            var layer = new InventoryCostLayer
            {
                ProductID = productId,
                WarehouseID = warehouseId,
                ReceivedAt = receivedAt,
                SourceType = "RETURN",
                OriginalQuantity = quantityBase,
                RemainingQuantity = quantityBase,
                UnitCostInBase = unitCostInBase,
                SalesReturnItemID = salesReturnItemId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                IsDeleted = false
            };

            await _context.InventoryCostLayers.AddAsync(layer, cancellationToken);
            await RefreshProductAverageCostAsync(productId, cancellationToken);
        }

        public async Task RestoreManualReturnAsync(
            int productId,
            int warehouseId,
            decimal quantityBase,
            int salesReturnItemId,
            DateTime receivedAt,
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (quantityBase <= 0)
            {
                return;
            }

            decimal unitCost = await GetWeightedAverageOpenLayerCostAsync(productId, warehouseId, cancellationToken);
            await RestoreSellableReturnAsync(
                productId, warehouseId, quantityBase, unitCost, salesReturnItemId, receivedAt, userId, cancellationToken);
        }

        public async Task<decimal> GetInventoryValueAsync(
            int? companyId,
            int? warehouseId,
            CancellationToken cancellationToken = default)
        {
            var query = _context.InventoryCostLayers.AsNoTracking()
                .Where(l => l.RemainingQuantity > 0);

            if (warehouseId.HasValue && warehouseId.Value > 0)
            {
                query = query.Where(l => l.WarehouseID == warehouseId.Value);
            }

            if (companyId.HasValue && companyId.Value > 0)
            {
                int cid = companyId.Value;
                query = query.Where(l => _context.Products.Any(p =>
                    p.ProductID == l.ProductID && p.CompanyID == cid && !p.IsDeleted));
            }

            return await query.SumAsync(
                l => (decimal?)(l.RemainingQuantity * l.UnitCostInBase), cancellationToken) ?? 0m;
        }

        public async Task<List<FifoCategoryInventoryValue>> GetInventoryValueByCategoryAsync(
            int? companyId,
            int? warehouseId,
            int? categoryId,
            CancellationToken cancellationToken = default)
        {
            var query = _context.InventoryCostLayers.AsNoTracking()
                .Where(l => l.RemainingQuantity > 0)
                .Join(
                    _context.Products.Where(p => !p.IsDeleted),
                    l => l.ProductID,
                    p => p.ProductID,
                    (l, p) => new { Layer = l, Product = p })
                .Join(
                    _context.Categories,
                    x => x.Product.CategoryID,
                    c => c.CategoryID,
                    (x, c) => new { x.Layer, x.Product, CategoryName = c.Name });

            if (warehouseId.HasValue && warehouseId.Value > 0)
            {
                query = query.Where(x => x.Layer.WarehouseID == warehouseId.Value);
            }

            if (companyId.HasValue && companyId.Value > 0)
            {
                int cid = companyId.Value;
                query = query.Where(x => x.Product.CompanyID == cid);
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                int catId = categoryId.Value;
                query = query.Where(x => x.Product.CategoryID == catId);
            }

            return await query
                .GroupBy(x => new { x.Product.CategoryID, x.CategoryName })
                .Select(g => new FifoCategoryInventoryValue
                {
                    CategoryName = g.Key.CategoryName ?? "Uncategorized",
                    ProductCount = g.Select(x => x.Product.ProductID).Distinct().Count(),
                    TotalValue = g.Sum(x => x.Layer.RemainingQuantity * x.Layer.UnitCostInBase)
                })
                .OrderByDescending(x => x.TotalValue)
                .ToListAsync(cancellationToken);
        }

        public async Task RefreshProductAverageCostAsync(int productId, CancellationToken cancellationToken = default)
        {
            var layers = await _context.InventoryCostLayers.AsNoTracking()
                .Where(l => l.ProductID == productId && l.RemainingQuantity > 0)
                .Select(l => new { l.RemainingQuantity, l.UnitCostInBase })
                .ToListAsync(cancellationToken);

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId, cancellationToken);
            if (product == null)
            {
                return;
            }

            if (!layers.Any())
            {
                return;
            }

            decimal totalQty = layers.Sum(l => l.RemainingQuantity);
            decimal totalValue = layers.Sum(l => l.RemainingQuantity * l.UnitCostInBase);
            product.AveragePurchaseCost = totalQty > 0
                ? Math.Round(totalValue / totalQty, 2, MidpointRounding.AwayFromZero)
                : product.AveragePurchaseCost;
        }

        public async Task BackfillHistoricalDataAsync(CancellationToken cancellationToken = default)
        {
            bool hasConsumptions = await _context.InventoryCostConsumptions.AnyAsync(cancellationToken);
            if (hasConsumptions)
            {
                _logger.LogInformation("FIFO backfill skipped — consumption records already exist.");
                return;
            }

            _logger.LogInformation("Starting FIFO historical backfill...");

            var purchaseItems = await _context.PurchaseInvoiceItems
                .Include(i => i.PurchaseInvoice)
                .Include(i => i.ProductUnit)
                .Where(i => !i.IsDeleted && !i.PurchaseInvoice.IsDeleted)
                .OrderBy(i => i.PurchaseInvoice.InvoiceDate)
                .ThenBy(i => i.PurchaseItemID)
                .ToListAsync(cancellationToken);

            int systemUserId = await _context.Users.Select(u => u.UserID).FirstOrDefaultAsync(cancellationToken);
            if (systemUserId <= 0)
            {
                systemUserId = 1;
            }

            foreach (var item in purchaseItems)
            {
                bool exists = await _context.InventoryCostLayers
                    .AnyAsync(l => l.PurchaseInvoiceItemID == item.PurchaseItemID, cancellationToken);
                if (exists)
                {
                    continue;
                }

                decimal conversion = item.ProductUnit?.ConversionToBaseUnit ?? 1m;
                await CreateLayerFromPurchaseAsync(
                    item,
                    item.PurchaseInvoice.WarehouseID,
                    item.PurchaseInvoice.InvoiceDate,
                    conversion,
                    systemUserId,
                    cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            var saleItems = await _context.SalesInvoiceItems
                .Include(i => i.SalesInvoice)
                .Where(i => !i.IsDeleted
                    && i.ProductID != null
                    && i.ConvertedQuantity > 0)
                .OrderBy(i => i.SalesInvoice.InvoiceDate)
                .ThenBy(i => i.InvoiceItemID)
                .ToListAsync(cancellationToken);

            foreach (var saleItem in saleItems)
            {
                var result = await ConsumeForSaleAsync(
                    saleItem.ProductID!.Value,
                    saleItem.SalesInvoice.WarehouseID,
                    saleItem.ConvertedQuantity,
                    saleItem.InvoiceItemID,
                    systemUserId,
                    cancellationToken);

                if (result.Success)
                {
                    saleItem.CostOfGoodsSold = result.TotalCost;
                    saleItem.UnitCostInBase = result.UnitCostInBase;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("FIFO historical backfill completed.");
        }

        private async Task<decimal> GetWeightedAverageOpenLayerCostAsync(
            int productId,
            int warehouseId,
            CancellationToken cancellationToken)
        {
            var layers = await _context.InventoryCostLayers.AsNoTracking()
                .Where(l => l.ProductID == productId
                    && l.WarehouseID == warehouseId
                    && l.RemainingQuantity > 0)
                .Select(l => new { l.RemainingQuantity, l.UnitCostInBase })
                .ToListAsync(cancellationToken);

            if (!layers.Any())
            {
                var product = await _context.Products.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ProductID == productId, cancellationToken);
                return product?.AveragePurchaseCost ?? 0m;
            }

            decimal totalQty = layers.Sum(l => l.RemainingQuantity);
            decimal totalValue = layers.Sum(l => l.RemainingQuantity * l.UnitCostInBase);
            return totalQty > 0 ? Math.Round(totalValue / totalQty, 4, MidpointRounding.AwayFromZero) : 0m;
        }
    }
}
