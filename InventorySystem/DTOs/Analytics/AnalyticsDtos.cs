using System;
using System.Collections.Generic;

namespace InventorySystem.DTOs.Analytics
{
    public class AnalyticsFilterDto
    {
        public string Preset { get; set; } = "ThisMonth"; // Today | Last7Days | Last30Days | ThisMonth | LastMonth | ThisYear | Custom
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? WarehouseID { get; set; }
        public int? CustomerID { get; set; }
        public int? CategoryID { get; set; }
    }

    public class KpiMetricDto
    {
        public string Title { get; set; } = string.Empty;
        public decimal CurrentValue { get; set; }
        public decimal PreviousValue { get; set; }
        public decimal PercentageChange { get; set; }
        public bool IsIncrease => PercentageChange >= 0;
        public string FormattedCurrentValue => string.Format("PKR {0:N2}", CurrentValue);
        public string FormattedPreviousValue => string.Format("PKR {0:N2}", PreviousValue);
        public string ComparisonText => string.Format("{0:0.0}% vs previous period", Math.Abs(PercentageChange));
    }

    public class AnalyticsKpiSummaryDto
    {
        public KpiMetricDto TotalSales { get; set; } = new KpiMetricDto { Title = "Total Sales" };
        public KpiMetricDto TotalPurchases { get; set; } = new KpiMetricDto { Title = "Total Purchases" };
        public KpiMetricDto EstimatedGrossProfit { get; set; } = new KpiMetricDto { Title = "Estimated Gross Profit" };
        public KpiMetricDto TotalDiscounts { get; set; } = new KpiMetricDto { Title = "Total Discounts" };
        public KpiMetricDto TotalReturns { get; set; } = new KpiMetricDto { Title = "Total Returns" };
        public KpiMetricDto NetSales { get; set; } = new KpiMetricDto { Title = "Net Sales" };
        public KpiMetricDto CustomerReceivables { get; set; } = new KpiMetricDto { Title = "Outstanding Customer Receivables" };
        public KpiMetricDto CompanyPayables { get; set; } = new KpiMetricDto { Title = "Outstanding Company Payables" };
    }

    public class SalesTrendItemDto
    {
        public string Label { get; set; } = string.Empty;
        public decimal SalesAmount { get; set; }
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
    }

    public class CategorySalesDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal SalesAmount { get; set; }
        public decimal Percentage { get; set; }
    }

    // ===== WAVE 3 DTOs =====

    public class InvoiceStatusBreakdownDto
    {
        public string Status { get; set; } = string.Empty; // Paid | Partial | Unpaid
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
        public string RiskLevel { get; set; } = "LOW_STOCK"; // OUT_OF_STOCK | LOW_STOCK
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
        public string Type { get; set; } = "INFO"; // DANGER | WARNING | SUCCESS | INFO
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-info-circle";
    }
}
