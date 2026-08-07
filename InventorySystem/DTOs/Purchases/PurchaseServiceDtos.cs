using System;
using System.Collections.Generic;

namespace InventorySystem.DTOs.Purchases
{
    public class CreatePurchaseInvoiceDto
    {
        public int CompanyID { get; set; }
        public int WarehouseID { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.Today;
        public string? Notes { get; set; }
        public List<CreatePurchaseItemDto> Items { get; set; } = new List<CreatePurchaseItemDto>();
    }

    public class UpdatePurchaseInvoiceDto
    {
        public int PurchaseInvoiceID { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.Today;
        public string? Notes { get; set; }
        public string EditReason { get; set; } = string.Empty;
        public List<CreatePurchaseItemDto> Items { get; set; } = new List<CreatePurchaseItemDto>();
    }

    public class CreatePurchaseItemDto
    {
        public int ProductID { get; set; }
        public int ProductUnitID { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }

    public class PurchaseHeaderDto
    {
        public int PurchaseInvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal ReturnedAmount { get; set; }
        public decimal OutstandingBalance => Math.Max(0m, GrandTotal - PaidAmount - ReturnedAmount);
        public string PaymentStatus { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class PurchaseItemDto
    {
        public int PurchaseItemID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int ProductUnitID { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ConvertedQuantity { get; set; }
        public string BaseUnitName { get; set; } = string.Empty;
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class PurchaseFinancialSummaryDto
    {
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Outstanding => GrandTotal - PaidAmount;
    }

    public class PurchaseDetailsDto
    {
        public PurchaseHeaderDto Header { get; set; } = new PurchaseHeaderDto();
        public List<PurchaseItemDto> Items { get; set; } = new List<PurchaseItemDto>();
        public PurchaseFinancialSummaryDto Financial { get; set; } = new PurchaseFinancialSummaryDto();
    }
}
