using System;

namespace InventorySystem.DTOs.Companies
{
    public class CompanyFilterDto
    {
        public string? SearchTerm { get; set; }
        public string? Phone { get; set; }
        public bool? HasOutstanding { get; set; }
        public string SortBy { get; set; } = "CompanyName";
        public bool IsAscending { get; set; } = true;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class CompanyFinancialSummaryDto
    {
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public decimal OutstandingPayable { get; set; }
        public decimal TotalPurchases { get; set; }
        public decimal TotalPaid { get; set; }
        public int PendingInvoicesCount { get; set; }
        public DateTime? LastPurchaseDate { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }

    public class CompanyPurchaseHistoryDto
    {
        public int PurchaseInvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount => TotalAmount - PaidAmount;
        public string Status { get; set; } = string.Empty;
    }

    public class CompanyPaymentHistoryDto
    {
        public int CompanyPaymentID { get; set; }
        public int PurchaseInvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? ReferenceNumber { get; set; }
    }

    public class CompanyLedgerEntryDto
    {
        public int CompanyLedgerID { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal NetChange => CreditAmount - DebitAmount;
    }
}
