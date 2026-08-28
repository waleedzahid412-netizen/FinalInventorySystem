using System;

namespace InventorySystem.DTOs.Customers
{
    public class CustomerFilterDto
    {
        public string? SearchTerm { get; set; }
        public int? AreaID { get; set; }
        public int? SubAreaID { get; set; }
        public bool? IsActive { get; set; }
        public bool? HasOutstanding { get; set; }
        public string SortBy { get; set; } = "ShopName";
        public bool IsAscending { get; set; } = true;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class CustomerInfoDto
    {
        public int CustomerID { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string? OwnerName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int? AreaID { get; set; }
        public string? AreaName { get; set; }
        public int? SubAreaID { get; set; }
        public string? SubAreaName { get; set; }
        public string? TaxID { get; set; }
        public decimal CreditLimit { get; set; }
        public decimal? PreferredDiscountPercent { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CustomerFinancialSummaryDto
    {
        public int CustomerID { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal OutstandingReceivable { get; set; }
        public decimal CreditLimit { get; set; }
        public bool IsCreditLimitExceeded => CreditLimit > 0 && OutstandingReceivable > CreditLimit;
        public int PendingInvoicesCount { get; set; }
        public DateTime? LastSaleDate { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }

    public class CustomerSalesHistoryDto
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount => GrandTotal - PaidAmount;
        public string PaymentStatus { get; set; } = string.Empty;
    }

    public class CustomerPaymentHistoryDto
    {
        public int CustomerPaymentID { get; set; }
        public int? InvoiceID { get; set; }
        public string? InvoiceNumber { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? ReferenceNumber { get; set; }
    }

    public class CustomerLedgerEntryDto
    {
        public int CustomerLedgerID { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal NetChange => DebitAmount - CreditAmount;
    }
}
