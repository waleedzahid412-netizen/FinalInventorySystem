using System;

namespace InventorySystem.DTOs.Products
{
    public class ProductFilterDto
    {
        public string? SearchTerm { get; set; }
        public string? SKU { get; set; }
        public string? Barcode { get; set; }
        public int? CategoryID { get; set; }
        public int? CompanyID { get; set; }
        public bool? IsActive { get; set; }
        public string SortBy { get; set; } = "ProductName";
        public bool IsAscending { get; set; } = true;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class InventorySummaryDto
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal TotalStockQuantity { get; set; }
        public decimal TotalInventoryValue { get; set; }
        public int ReorderLevel { get; set; }
        public bool IsLowStock => TotalStockQuantity <= ReorderLevel;
        public decimal BaseSellingPrice { get; set; }
        public decimal AveragePurchaseCost { get; set; }
        public DateTime? LastPurchaseDate { get; set; }
        public DateTime? LastSaleDate { get; set; }
        public decimal TotalSalesQuantity { get; set; }
        public decimal TotalPurchaseQuantity { get; set; }
    }

    public class WarehouseStockSummaryDto
    {
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    public class ProductPurchaseHistoryDto
    {
        public int PurchaseInvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ConvertedQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class ProductSalesHistoryDto
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ConvertedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class ProductInventoryTransactionDto
    {
        public int TransactionID { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string? ReferenceNumber { get; set; }
    }
}
