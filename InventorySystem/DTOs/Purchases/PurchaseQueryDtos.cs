using System;

namespace InventorySystem.DTOs.Purchases
{
    public class PurchaseFilterDto
    {
        public string? InvoiceNumber { get; set; }
        public int? CompanyID { get; set; }
        public int? WarehouseID { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? PaymentStatus { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class PurchaseListDto
    {
        public int PurchaseInvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string? SupplierInvoiceNumber { get; set; }
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal ReturnedAmount { get; set; }
        public decimal Outstanding => Math.Max(0m, GrandTotal - PaidAmount - ReturnedAmount);
        public string PaymentStatus { get; set; } = string.Empty;
    }

    public class PurchasePaymentHistoryDto
    {
        public int CompanyPaymentID { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string PaidByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class PurchaseLedgerEntryDto
    {
        public int CompanyLedgerID { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public string? Description { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
