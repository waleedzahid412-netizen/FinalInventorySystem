using System;
using InventorySystem.DTOs.Common;

namespace InventorySystem.DTOs.Sales
{
    public class SalesFilterDto
    {
        public string? InvoiceNumber { get; set; }
        public int? CustomerID { get; set; }
        public int? WarehouseID { get; set; }
        public int? SupplierID { get; set; }
        /// <summary>When set (&gt; 0), restricts results to that company. Null/0 = all companies.</summary>
        public int? CompanyID { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? PaymentStatus { get; set; }
        public string? DeliveryStatus { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class SalesListDto
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? SupplierName { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal ReturnedAmount { get; set; }
        public decimal OutstandingBalance => Math.Max(0m, GrandTotal - PaidAmount - ReturnedAmount);
        public string PaymentStatus { get; set; } = "UNPAID";
        public string DeliveryStatus { get; set; } = "Pending";
        public bool IsLocked { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SalesPaymentHistoryDto
    {
        public int CustomerPaymentID { get; set; }
        public string PaymentNumber { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string CreatedByUserName { get; set; } = string.Empty;
    }

    public class SalesLedgerEntryDto
    {
        public int CustomerLedgerID { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public string? Description { get; set; }
        public string CreatedByUserName { get; set; } = string.Empty;
    }
}
