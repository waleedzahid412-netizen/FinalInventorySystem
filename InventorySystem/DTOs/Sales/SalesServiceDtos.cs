using System;
using System.Collections.Generic;

namespace InventorySystem.DTOs.Sales
{
    public class CreateSalesItemDto
    {
        public int ProductID { get; set; }
        public int ProductUnitID { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; } = 0;
        public string ItemType { get; set; } = "NORMAL";
        public int? PromotionID { get; set; }
    }

    public class CreateSalesInvoiceDto
    {
        public int CustomerID { get; set; }
        public int WarehouseID { get; set; }
        public int? DeliveryPersonID { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
        public string? Remarks { get; set; }
        public int? AppliedDiscountRuleID { get; set; }

        public List<CreateSalesItemDto> Items { get; set; } = new List<CreateSalesItemDto>();
    }

    public class UpdateSalesInvoiceDto
    {
        public int InvoiceID { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
        public int? DeliveryPersonID { get; set; }
        public string? Remarks { get; set; }
        public int? AppliedDiscountRuleID { get; set; }
        public string EditReason { get; set; } = string.Empty;

        public List<CreateSalesItemDto> Items { get; set; } = new List<CreateSalesItemDto>();
    }

    public class SalesHeaderDto
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? ShopName { get; set; }
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public int? DeliveryPersonID { get; set; }
        public string? DeliveryPersonName { get; set; }
        public int? AreaID { get; set; }
        public string? AreaName { get; set; }
        public int? SubAreaID { get; set; }
        public string? SubAreaName { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal ReturnedAmount { get; set; }
        public decimal OutstandingBalance => Math.Max(0m, GrandTotal - PaidAmount - ReturnedAmount);
        public string PaymentStatus { get; set; } = "UNPAID";
        public string DeliveryStatus { get; set; } = "Pending";
        public int? AppliedDiscountRuleID { get; set; }
        public bool IsLocked { get; set; }
        public string CreatedByUserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class SalesItemDto
    {
        public int InvoiceItemID { get; set; }
        public int InvoiceID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public int ProductUnitID { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ConvertedQuantity { get; set; }
        public string BaseUnitName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal LineTotal => (Quantity * UnitPrice) - DiscountAmount;
        public string ItemType { get; set; } = "NORMAL";
        public int? PromotionID { get; set; }
    }

    public class SalesFinancialSummaryDto
    {
        public decimal SubTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingBalance => GrandTotal - PaidAmount;
    }

    public class SalesDetailsDto
    {
        public SalesHeaderDto Header { get; set; } = new SalesHeaderDto();
        public List<SalesItemDto> Items { get; set; } = new List<SalesItemDto>();
        public SalesFinancialSummaryDto Financial { get; set; } = new SalesFinancialSummaryDto();
    }
}
