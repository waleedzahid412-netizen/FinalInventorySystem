using System;
using System.Collections.Generic;

namespace InventorySystem.DTOs.Analytics
{
    public class AnalyticsFilterDto
    {
        public string Preset { get; set; } = "ThisMonth";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? WarehouseID { get; set; }
        public int? CustomerID { get; set; }
        public int? CategoryID { get; set; }
        /// <summary>Null/0 = all brokers. -1 = unassigned only. Positive = specific broker.</summary>
        public int? BrokerID { get; set; }
    }

    public class KpiMetricDto
    {
        public string Title { get; set; } = string.Empty;
        public decimal CurrentValue { get; set; }
        public decimal PreviousValue { get; set; }
        public decimal PercentageChange { get; set; }
        public bool IsSnapshot { get; set; }
        public bool IsIncrease => PercentageChange >= 0;
        public string FormattedCurrentValue => string.Format("PKR {0:N2}", CurrentValue);
        public string FormattedPreviousValue => string.Format("PKR {0:N2}", PreviousValue);
        public string ComparisonText => IsSnapshot
            ? "Cumulative ledger balance"
            : string.Format("{0:0.0}% vs previous period", Math.Abs(PercentageChange));
    }

    public class AnalyticsKpiSummaryDto
    {
        public KpiMetricDto TotalSales { get; set; } = new KpiMetricDto { Title = "Total Sales" };
        public KpiMetricDto TotalPurchases { get; set; } = new KpiMetricDto { Title = "Total Purchases" };
        public KpiMetricDto EstimatedGrossProfit { get; set; } = new KpiMetricDto { Title = "Estimated Gross Profit (Avg Cost)" };
        public KpiMetricDto TotalDiscounts { get; set; } = new KpiMetricDto { Title = "Total Discounts" };
        public KpiMetricDto TotalReturns { get; set; } = new KpiMetricDto { Title = "Total Returns" };
        public KpiMetricDto NetSales { get; set; } = new KpiMetricDto { Title = "Net Sales" };
        public KpiMetricDto CustomerReceivables { get; set; } = new KpiMetricDto { Title = "Outstanding Customer Receivables", IsSnapshot = true };
        public KpiMetricDto CompanyPayables { get; set; } = new KpiMetricDto { Title = "Outstanding Company Payables", IsSnapshot = true };
    }

    public class SalesTrendItemDto
    {
        public string Label { get; set; } = string.Empty;
        public DateTime BucketStart { get; set; }
        public DateTime BucketEnd { get; set; }
        public decimal SalesAmount { get; set; }
        public decimal NetSalesAmount { get; set; }
        public decimal ReturnsAmount { get; set; }
        public decimal PurchaseAmount { get; set; }
        public int InvoiceCount { get; set; }
    }

    public class TopProductDto
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal EstimatedProfit { get; set; }
    }

    public class TopCustomerDto
    {
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int InvoiceCount { get; set; }
        public decimal OutstandingBalance { get; set; }
        public decimal ReturnsAmount { get; set; }
        public DateTime? LastSaleDate { get; set; }
    }

    public class CategorySalesDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal SalesAmount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class InvoiceStatusBreakdownDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class PaymentAnalyticsDto
    {
        public decimal CustomerPaymentsCollected { get; set; }
        public decimal CompanyPaymentsMade { get; set; }
        public decimal TotalOutstandingReceivables { get; set; }
        public decimal TotalOutstandingPayables { get; set; }
        public List<InvoiceStatusBreakdownDto> InvoiceStatuses { get; set; } = new List<InvoiceStatusBreakdownDto>();
    }

    public class CategoryInventoryValueDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal TotalValue { get; set; }
        public decimal Percentage { get; set; }
        public int ProductCount { get; set; }
    }

    public class InventoryInsightsDto
    {
        public decimal TotalInventoryValue { get; set; }
        public int TotalProductCount { get; set; }
        public int LowStockProductCount { get; set; }
        public int OutOfStockProductCount { get; set; }
        public List<CategoryInventoryValueDto> ValueByCategory { get; set; } = new List<CategoryInventoryValueDto>();
    }

    public class StockRiskItemDto
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal CurrentStock { get; set; }
        public decimal ReorderLevel { get; set; }
        public string BaseUnit { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = "LOW_STOCK";
    }

    public class InventoryMovementDto
    {
        public decimal PurchasedQuantity { get; set; }
        public decimal SoldQuantity { get; set; }
        public decimal SalesReturnQuantity { get; set; }
        public decimal AdjustmentQuantity { get; set; }
    }

    public class PromotionPerformanceDto
    {
        public decimal TotalDiscountAmount { get; set; }
        public int PromoInvoicesCount { get; set; }
        public int RegularDiscountsCount { get; set; }
        public decimal TotalInvoicePromotionsAmount { get; set; }
        public decimal TotalRegularDiscountsAmount { get; set; }
    }

    public class TopCompanyDto
    {
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public decimal TotalPurchases { get; set; }
        public int InvoiceCount { get; set; }
        public decimal OutstandingPayable { get; set; }
    }

    public class BusinessInsightDto
    {
        public string Type { get; set; } = "INFO";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-info-circle";
    }

    public class OverviewAnalyticsDto
    {
        public AnalyticsKpiSummaryDto Kpi { get; set; } = new AnalyticsKpiSummaryDto();
        public List<SalesTrendItemDto> SalesTrend { get; set; } = new List<SalesTrendItemDto>();
        public List<BusinessInsightDto> Insights { get; set; } = new List<BusinessInsightDto>();
        public InventoryInsightsDto Inventory { get; set; } = new InventoryInsightsDto();
    }

    public class NamedAmountDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    public class AgingBucketDto
    {
        public string Bucket { get; set; } = string.Empty;
        public int InvoiceCount { get; set; }
        public decimal OutstandingAmount { get; set; }
    }

    public class InvoiceDrilldownDto
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
        public decimal Outstanding { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
    }

    public class SalesAnalyticsDto
    {
        public decimal TotalSales { get; set; }
        public decimal NetSales { get; set; }
        public decimal Returns { get; set; }
        public decimal AverageInvoiceValue { get; set; }
        public int InvoiceCount { get; set; }
        public decimal DiscountRate { get; set; }
        public decimal TotalDiscounts { get; set; }
        public List<SalesTrendItemDto> Trends { get; set; } = new List<SalesTrendItemDto>();
        public List<NamedAmountDto> SalesByArea { get; set; } = new List<NamedAmountDto>();
        public List<NamedAmountDto> SalesBySubArea { get; set; } = new List<NamedAmountDto>();
        public List<InvoiceStatusBreakdownDto> PaymentStatuses { get; set; } = new List<InvoiceStatusBreakdownDto>();
        public List<AgingBucketDto> Aging { get; set; } = new List<AgingBucketDto>();
        public PaymentAnalyticsDto Payments { get; set; } = new PaymentAnalyticsDto();
    }

    public class CustomerRankedDto
    {
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? AreaName { get; set; }
        public decimal Revenue { get; set; }
        public int InvoiceCount { get; set; }
        public decimal Returns { get; set; }
        public decimal Outstanding { get; set; }
        public DateTime? LastSaleDate { get; set; }
        public bool HasSalesInPeriod { get; set; }
    }

    public class CustomerAnalyticsDto
    {
        public List<CustomerRankedDto> Customers { get; set; } = new List<CustomerRankedDto>();
        public List<CustomerRankedDto> InactiveCustomers { get; set; } = new List<CustomerRankedDto>();
    }

    public class CustomerDetailDto
    {
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? AreaName { get; set; }
        public string? SubAreaName { get; set; }
        public decimal Revenue { get; set; }
        public decimal Returns { get; set; }
        public decimal Outstanding { get; set; }
        public decimal PaymentsCollected { get; set; }
        public decimal Billed { get; set; }
        public int InvoiceCount { get; set; }
        public List<SalesTrendItemDto> SalesTrend { get; set; } = new List<SalesTrendItemDto>();
        public List<TopProductDto> ProductMix { get; set; } = new List<TopProductDto>();
    }

    public class ProductRankedDto
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal QuantitySoldBase { get; set; }
        public decimal Revenue { get; set; }
        public decimal EstimatedProfit { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal? Velocity { get; set; }
        public string MoverClass { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty;
    }

    public class TopPurchasedProductDto
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal QuantityPurchasedBase { get; set; }
        public decimal PurchaseCost { get; set; }
    }

    public class ProductAnalyticsDto
    {
        public List<ProductRankedDto> Products { get; set; } = new List<ProductRankedDto>();
        public List<ProductRankedDto> FastMovers { get; set; } = new List<ProductRankedDto>();
        public List<ProductRankedDto> SlowMovers { get; set; } = new List<ProductRankedDto>();
        public List<ProductRankedDto> DeadMovers { get; set; } = new List<ProductRankedDto>();
        public List<CategorySalesDto> CategoryMix { get; set; } = new List<CategorySalesDto>();
        public List<StockRiskItemDto> StockRisk { get; set; } = new List<StockRiskItemDto>();
        public List<TopPurchasedProductDto> TopPurchased { get; set; } = new List<TopPurchasedProductDto>();
        public InventoryMovementDto Movement { get; set; } = new InventoryMovementDto();
        public InventoryInsightsDto Inventory { get; set; } = new InventoryInsightsDto();
    }

    public class WarehouseStockDto
    {
        public string WarehouseName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    public class ProductCustomerDto
    {
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal QuantitySoldBase { get; set; }
        public decimal Revenue { get; set; }
    }

    public class ProductDetailDto
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string BaseUnit { get; set; } = string.Empty;
        public decimal QuantitySoldBase { get; set; }
        public decimal Revenue { get; set; }
        public decimal EstimatedProfit { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal ReorderLevel { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public List<SalesTrendItemDto> SalesTrend { get; set; } = new List<SalesTrendItemDto>();
        public List<ProductCustomerDto> Customers { get; set; } = new List<ProductCustomerDto>();
        public List<WarehouseStockDto> WarehouseStock { get; set; } = new List<WarehouseStockDto>();
    }

    public class BrokerRankedDto
    {
        public int? BrokerID { get; set; }
        public string BrokerName { get; set; } = "Unassigned";
        public int InvoiceCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal Discounts { get; set; }
        public decimal Returns { get; set; }
        public decimal Outstanding { get; set; }
        public int UniqueCustomers { get; set; }
    }

    public class BrokerAnalyticsDto
    {
        public decimal Revenue { get; set; }
        public int InvoiceCount { get; set; }
        public int UniqueCustomers { get; set; }
        public decimal Outstanding { get; set; }
        public List<BrokerRankedDto> Brokers { get; set; } = new List<BrokerRankedDto>();
        public List<TopCustomerDto> TopCustomers { get; set; } = new List<TopCustomerDto>();
        public List<TopProductDto> TopProducts { get; set; } = new List<TopProductDto>();
    }

    public class BrokerDetailDto
    {
        public int? BrokerID { get; set; }
        public string BrokerName { get; set; } = "Unassigned";
        public decimal Revenue { get; set; }
        public decimal Returns { get; set; }
        public decimal Discounts { get; set; }
        public decimal Outstanding { get; set; }
        public int InvoiceCount { get; set; }
        public List<SalesTrendItemDto> SalesTrend { get; set; } = new List<SalesTrendItemDto>();
        public List<CustomerRankedDto> Customers { get; set; } = new List<CustomerRankedDto>();
        public List<InvoiceStatusBreakdownDto> PaymentMix { get; set; } = new List<InvoiceStatusBreakdownDto>();
        public List<TopProductDto> TopProducts { get; set; } = new List<TopProductDto>();
    }
}
