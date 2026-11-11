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
        /// <summary>Client-supplied value is ignored; server sets total line discount (product + invoice share).</summary>
        public decimal DiscountAmount { get; set; } = 0;
        /// <summary>Server-set invoice-level discount rate snapshot for this line (percentage) or 0 for fixed.</summary>
        public decimal DiscountRate { get; set; } = 0;
        /// <summary>Server-set product discount snapshot; client values ignored.</summary>
        public string? ProductDiscountType { get; set; }
        public decimal? ProductDiscountValue { get; set; }
        public decimal ProductDiscountAmount { get; set; } = 0;
        public string ItemType { get; set; } = "NORMAL";
        public int? PromotionID { get; set; }
        public bool IsCustomFreeItem { get; set; }
        public string? CustomFreeItemName { get; set; }
    }

    public class CreateSalesInvoiceDto
    {
        public int CustomerID { get; set; }
        public int BookerID { get; set; }
        public int SalespersonID { get; set; }
        public int WarehouseID { get; set; }
        public int? SupplierID { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
        public string? Remarks { get; set; }
        public int? AppliedDiscountRuleID { get; set; }

        /// <summary>None | Automatic | Manual. Empty/None with AppliedDiscountRuleID is treated as Automatic for backward compatibility.</summary>
        public string DiscountMode { get; set; } = "None";

        /// <summary>Percentage | FixedAmount — used when DiscountMode = Manual.</summary>
        public string? ManualDiscountType { get; set; }

        /// <summary>Percent or fixed PKR — used when DiscountMode = Manual.</summary>
        public decimal ManualDiscountValue { get; set; }

        /// <summary>
        /// When true, server evaluates promotions and rebuilds FREE lines.
        /// When false, FREE lines are omitted (client FREE lines are still discarded).
        /// </summary>
        public bool ApplyPromotions { get; set; } = true;

        /// <summary>When true (default), apply each product's catalog discount onto NORMAL lines.</summary>
        public bool ApplyProductDiscount { get; set; } = true;

        public List<CreateSalesItemDto> Items { get; set; } = new List<CreateSalesItemDto>();
    }

    public class UpdateSalesInvoiceDto
    {
        public int InvoiceID { get; set; }
        public int BookerID { get; set; }
        public int SalespersonID { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
        public int? SupplierID { get; set; }
        public string? Remarks { get; set; }
        public int? AppliedDiscountRuleID { get; set; }
        public string EditReason { get; set; } = string.Empty;

        /// <summary>None | Automatic | Manual</summary>
        public string DiscountMode { get; set; } = "None";

        /// <summary>Percentage | FixedAmount — used when DiscountMode = Manual.</summary>
        public string? ManualDiscountType { get; set; }

        /// <summary>Percent or fixed PKR — used when DiscountMode = Manual.</summary>
        public decimal ManualDiscountValue { get; set; }

        /// <summary>
        /// When true, server evaluates promotions and rebuilds FREE lines.
        /// When false, FREE lines are omitted (client FREE lines are still discarded).
        /// </summary>
        public bool ApplyPromotions { get; set; } = true;

        /// <summary>When true (default), apply each product's catalog discount onto NORMAL lines.</summary>
        public bool ApplyProductDiscount { get; set; } = true;

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
        public string? CustomerAddress { get; set; }
        public string? CustomerPhone { get; set; }
        public int? CompanyID { get; set; }
        public string? CompanyName { get; set; }
        public int? BookerID { get; set; }
        public string? BookerName { get; set; }
        /// <summary>First recorded payment method, else Cash/Credit inferred from PaidAmount.</summary>
        public string PaymentMode { get; set; } = string.Empty;
        public int? SalespersonID { get; set; }
        public string? SalespersonName { get; set; }
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public int? SupplierID { get; set; }
        public string? SupplierName { get; set; }
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
        /// <summary>None | Automatic | Manual</summary>
        public string DiscountMode { get; set; } = "None";
        public bool ApplyProductDiscount { get; set; } = true;
        public bool IsLocked { get; set; }
        public string CreatedByUserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class SalesItemDto
    {
        public int InvoiceItemID { get; set; }
        public int InvoiceID { get; set; }
        public int? ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public int? ProductUnitID { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ConvertedQuantity { get; set; }
        public string BaseUnitName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal DiscountRate { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? ProductDiscountType { get; set; }
        public decimal? ProductDiscountValue { get; set; }
        public decimal ProductDiscountAmount { get; set; }
        public decimal LineTotal => (Quantity * UnitPrice) - DiscountAmount;
        /// <summary>Line amount after product discount only (before invoice-level Manual/Automatic share).</summary>
        public decimal AmountAfterProductDiscount => (Quantity * UnitPrice) - ProductDiscountAmount;
        public string ItemType { get; set; } = "NORMAL";
        public int? PromotionID { get; set; }
        public string? CustomItemName { get; set; }
    }

    public class SalesFinancialSummaryDto
    {
        public decimal SubTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingBalance => GrandTotal - PaidAmount;

        /// <summary>Sum of line ProductDiscountAmount (catalog product discounts).</summary>
        public decimal ProductDiscountTotal { get; set; }

        /// <summary>Invoice-level Manual/Automatic/Customer amount (DiscountTotal − ProductDiscountTotal).</summary>
        public decimal AdditionalDiscountTotal { get; set; }

        /// <summary>None | Automatic | Manual | Customer — from invoice header.</summary>
        public string DiscountMode { get; set; } = "None";

        /// <summary>Snapshot from InvoiceDiscounts (Percentage / FixedAmount). Null when no invoice-level discount.</summary>
        public string? DiscountType { get; set; }

        /// <summary>Snapshot rate/value from InvoiceDiscounts. For Percentage this is the % to print on invoices.</summary>
        public decimal? DiscountValue { get; set; }
    }

    public class SalesDetailsDto
    {
        public SalesHeaderDto Header { get; set; } = new SalesHeaderDto();
        public List<SalesItemDto> Items { get; set; } = new List<SalesItemDto>();
        public SalesFinancialSummaryDto Financial { get; set; } = new SalesFinancialSummaryDto();
    }
}
