using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.Models.Entities;

namespace InventorySystem.Services.Interfaces
{
    public class FifoConsumptionResult
    {
        public decimal TotalCost { get; set; }
        public decimal UnitCostInBase { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        public static FifoConsumptionResult Ok(decimal totalCost, decimal quantityBase) => new()
        {
            Success = true,
            TotalCost = totalCost,
            UnitCostInBase = quantityBase > 0 ? totalCost / quantityBase : 0m
        };

        public static FifoConsumptionResult Fail(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };
    }

    public class FifoCategoryInventoryValue
    {
        public string CategoryName { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public decimal TotalValue { get; set; }
    }

    /// <summary>
    /// FIFO inventory costing (BR-004). Manages cost layers and consumption.
    /// </summary>
    public interface IFifoCostingService
    {
        /// <summary>Create a cost layer from a finalized purchase line.</summary>
        Task CreateLayerFromPurchaseAsync(
            PurchaseInvoiceItem item,
            int warehouseId,
            DateTime receivedAt,
            decimal conversionToBaseUnit,
            int userId,
            CancellationToken cancellationToken = default);

        /// <summary>Consume FIFO layers for a sale line. Must run inside caller's transaction.</summary>
        Task<FifoConsumptionResult> ConsumeForSaleAsync(
            int productId,
            int warehouseId,
            decimal quantityBase,
            int salesInvoiceItemId,
            int userId,
            CancellationToken cancellationToken = default);

        /// <summary>Reverse consumptions for a sales line (invoice edit reversal). Restores layer quantities.</summary>
        Task RestoreConsumptionsForSalesItemAsync(
            int salesInvoiceItemId,
            int userId,
            CancellationToken cancellationToken = default);

        /// <summary>Reverse a purchase layer on invoice edit. Fails if stock from batch was already sold.</summary>
        Task<OperationResult> ReversePurchaseLayerAsync(
            int purchaseInvoiceItemId,
            decimal quantityBase,
            int userId,
            CancellationToken cancellationToken = default);

        /// <summary>Restore sellable returned stock as a new FIFO layer at original sale cost.</summary>
        Task RestoreSellableReturnAsync(
            int productId,
            int warehouseId,
            decimal quantityBase,
            decimal unitCostInBase,
            int salesReturnItemId,
            DateTime receivedAt,
            int userId,
            CancellationToken cancellationToken = default);

        /// <summary>Restock from manual return using weighted avg of open layers (or product fallback).</summary>
        Task RestoreManualReturnAsync(
            int productId,
            int warehouseId,
            decimal quantityBase,
            int salesReturnItemId,
            DateTime receivedAt,
            int userId,
            CancellationToken cancellationToken = default);

        /// <summary>Sum of RemainingQuantity × UnitCostInBase for inventory valuation.</summary>
        Task<decimal> GetInventoryValueAsync(
            int? companyId,
            int? warehouseId,
            CancellationToken cancellationToken = default);

        /// <summary>FIFO inventory value grouped by product category.</summary>
        Task<List<FifoCategoryInventoryValue>> GetInventoryValueByCategoryAsync(
            int? companyId,
            int? warehouseId,
            int? categoryId,
            CancellationToken cancellationToken = default);

        /// <summary>Refresh Product.AveragePurchaseCost from open layers (display cache).</summary>
        Task RefreshProductAverageCostAsync(int productId, CancellationToken cancellationToken = default);

        /// <summary>One-time backfill: layers from purchases + replay historical sales.</summary>
        Task BackfillHistoricalDataAsync(CancellationToken cancellationToken = default);
    }
}
