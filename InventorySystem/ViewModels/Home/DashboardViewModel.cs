using System;
using System.Collections.Generic;

namespace InventorySystem.ViewModels.Home
{
    public class DashboardViewModel
    {
        // KPI Totals
        public decimal NetSalesToday { get; set; }
        public decimal NetSalesThisMonth { get; set; }
        public decimal TotalPurchasesToday { get; set; }
        public decimal TotalPurchasesThisMonth { get; set; }
        public decimal CustomerReceivables { get; set; }
        public decimal SupplierPayables { get; set; }
        public decimal InventoryValue { get; set; }
        public int LowStockCount { get; set; }
        public int TodayReturnsCount { get; set; }

        // Recent Activity Lists
        public List<DashboardSaleItem> RecentSales { get; set; } = new List<DashboardSaleItem>();
        public List<DashboardPurchaseItem> RecentPurchases { get; set; } = new List<DashboardPurchaseItem>();
        public List<DashboardReturnItem> RecentReturns { get; set; } = new List<DashboardReturnItem>();
        public List<DashboardLowStockProduct> LowStockProducts { get; set; } = new List<DashboardLowStockProduct>();

        /// <summary>True when dashboard shows consolidated figures (All Companies or unscoped).</summary>
        public bool ShowAllCompanies { get; set; }

        /// <summary>True when ambient scope is deliberate All Companies.</summary>
        public bool IsAllCompaniesMode { get; set; }

        /// <summary>True when no company or All Companies scope is resolved.</summary>
        public bool IsUnscoped { get; set; }

        public bool HasCompanyScope { get; set; }

        public string ScopedCompanyName { get; set; } = string.Empty;

        /// <summary>Set when a specific company is selected (not All Companies).</summary>
        public int? ScopedCompanyId { get; set; }
    }

    public class DashboardSaleItem
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
    }

    public class DashboardPurchaseItem
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
    }

    public class DashboardReturnItem
    {
        public int SalesReturnID { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public DateTime ReturnDate { get; set; }
        public decimal NetRefundAmount { get; set; }
        public string ReturnType { get; set; } = string.Empty;
    }

    public class DashboardLowStockProduct
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal CurrentStock { get; set; }
        public decimal MinStockLevel { get; set; }
    }
}
