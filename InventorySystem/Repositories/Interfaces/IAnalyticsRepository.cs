using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Analytics;

namespace InventorySystem.Repositories.Interfaces
{
    public interface IAnalyticsRepository
    {
        Task<List<AnalyticsSalesInvoiceRow>> GetSalesInvoiceRowsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        Task<List<AnalyticsSalesItemRow>> GetSalesItemRowsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        Task<List<AnalyticsReturnRow>> GetReturnRowsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        Task<List<AnalyticsPurchaseInvoiceRow>> GetPurchaseInvoiceRowsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        Task<List<AnalyticsPurchaseItemRow>> GetPurchaseItemRowsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        Task<decimal> GetCustomerPaymentsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        Task<decimal> GetCompanyPaymentsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        Task<decimal> GetCustomerReceivablesAsync(int? customerId, int? companyId, CancellationToken cancellationToken = default);

        Task<decimal> GetCompanyPayablesAsync(int? companyId, CancellationToken cancellationToken = default);

        Task<Dictionary<int, decimal>> GetCustomerLedgerBalancesAsync(IEnumerable<int> customerIds, CancellationToken cancellationToken = default);

        Task<Dictionary<int, decimal>> GetCompanyLedgerBalancesAsync(IEnumerable<int> companyIds, CancellationToken cancellationToken = default);

        Task<List<AnalyticsStockRow>> GetStockRowsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);

        Task<List<AnalyticsMovementRow>> GetMovementRowsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        Task<decimal> GetInvoicePromotionAmountAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        Task<List<AnalyticsLookupItem>> GetWarehousesAsync(CancellationToken cancellationToken = default);
        Task<List<AnalyticsLookupItem>> GetCustomersAsync(CancellationToken cancellationToken = default);
        Task<List<AnalyticsLookupItem>> GetCategoriesAsync(CancellationToken cancellationToken = default);
        Task<List<AnalyticsLookupItem>> GetBookersAsync(CancellationToken cancellationToken = default);
        Task<string?> GetCustomerNameAsync(int customerId, CancellationToken cancellationToken = default);
        Task<string?> GetProductNameAsync(int productId, CancellationToken cancellationToken = default);
        Task<string?> GetBookerNameAsync(int bookerId, CancellationToken cancellationToken = default);
    }

    public class AnalyticsSalesInvoiceRow
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int WarehouseID { get; set; }
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int? BookerID { get; set; }
        public string? BookerName { get; set; }
        public int? AreaID { get; set; }
        public string? AreaName { get; set; }
        public int? SubAreaID { get; set; }
        public string? SubAreaName { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public string PaymentStatus { get; set; } = "UNPAID";
        public decimal ReturnedAmount { get; set; }
    }

    public class AnalyticsSalesItemRow
    {
        public int InvoiceID { get; set; }
        public DateTime InvoiceDate { get; set; }
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int? ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int? CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ConvertedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal AveragePurchaseCost { get; set; }
        /// <summary>FIFO COGS stored on line at sale; 0 for pre-FIFO historical rows.</summary>
        public decimal CostOfGoodsSold { get; set; }
    }

    public class AnalyticsReturnRow
    {
        public int SalesReturnID { get; set; }
        public DateTime ReturnDate { get; set; }
        public int CustomerID { get; set; }
        public int? InvoiceID { get; set; }
        public int? WarehouseID { get; set; }
        public int? BookerID { get; set; }
        public decimal NetRefundAmount { get; set; }
        public int? ProductID { get; set; }
        public int? CategoryID { get; set; }
        public decimal LineRefundAmount { get; set; }
        public bool HasLineSplit { get; set; }
    }

    public class AnalyticsPurchaseInvoiceRow
    {
        public int PurchaseInvoiceID { get; set; }
        public DateTime InvoiceDate { get; set; }
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int WarehouseID { get; set; }
        public decimal GrandTotal { get; set; }
    }

    public class AnalyticsPurchaseItemRow
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CategoryID { get; set; }
        public DateTime InvoiceDate { get; set; }
        public int WarehouseID { get; set; }
        public decimal ConvertedQuantity { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class AnalyticsStockRow
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string BaseUnit { get; set; } = "Units";
        public decimal ReorderLevel { get; set; }
        public decimal AveragePurchaseCost { get; set; }
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    public class AnalyticsMovementRow
    {
        public string TransactionType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    public class AnalyticsLookupItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
