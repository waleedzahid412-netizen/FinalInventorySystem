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
}
