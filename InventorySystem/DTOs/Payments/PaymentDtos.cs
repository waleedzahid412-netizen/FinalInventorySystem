using System;
using System.Collections.Generic;
using InventorySystem.DTOs.Common;

namespace InventorySystem.DTOs.Payments
{
    public class RecordCustomerPaymentRequest
    {
        public int? InvoiceID { get; set; }
        public int CustomerID { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cash"; // Cash | Bank | Cheque
        public string? ReferenceNumber { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public string? Remarks { get; set; }

        // Cheque Details
        public string? ChequeNumber { get; set; }
        public string? BankName { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ChequeDate { get; set; }
    }

    public class RecordCompanyPaymentRequest
    {
        public int? PurchaseInvoiceID { get; set; }
        public int CompanyID { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cash"; // Cash | Bank | Cheque
        public string? ReferenceNumber { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public string? Remarks { get; set; }

        // Cheque Details
        public string? ChequeNumber { get; set; }
        public string? BankName { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ChequeDate { get; set; }
    }

    public class UnpaidInvoiceLookupDto
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingBalance => GrandTotal - PaidAmount;
    }

    public class CustomerPaymentDto
    {
        public int CustomerPaymentID { get; set; }
        public string PaymentNumber { get; set; } = string.Empty;
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        public int ReceivedBy { get; set; }
        public string ReceivedByUserName { get; set; } = string.Empty;

        // Cheque Lifecycle
        public string? ChequeNumber { get; set; }
        public string? BankName { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ChequeDate { get; set; }
        public string ChequeStatus { get; set; } = "Cleared";
        public DateTime? ClearedAt { get; set; }
        public DateTime? BouncedAt { get; set; }
        public string? BounceRemarks { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class CompanyPaymentDto
    {
        public int CompanyPaymentID { get; set; }
        public string PaymentNumber { get; set; } = string.Empty;
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int PurchaseInvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public int PaidBy { get; set; }
        public string PaidByUserName { get; set; } = string.Empty;

        // Cheque Lifecycle
        public string? ChequeNumber { get; set; }
        public string? BankName { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ChequeDate { get; set; }
        public string ChequeStatus { get; set; } = "Cleared";
        public DateTime? ClearedAt { get; set; }
        public DateTime? BouncedAt { get; set; }
        public string? BounceRemarks { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class PaymentFilterDto
    {
        public int? CustomerID { get; set; }
        public int? CompanyID { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class CustomerStatementEntryDto
    {
        public int CustomerLedgerID { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public int? SalesInvoiceID { get; set; }
        public string? InvoiceNumber { get; set; }
        public int? CustomerPaymentID { get; set; }
        public string? Description { get; set; }
        public decimal RunningBalance { get; set; }
    }

    public class CompanyStatementEntryDto
    {
        public int CompanyLedgerID { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public int? PurchaseInvoiceID { get; set; }
        public string? InvoiceNumber { get; set; }
        public int? CompanyPaymentID { get; set; }
        public string? Description { get; set; }
        public decimal RunningBalance { get; set; }
    }

    // ===== CHEQUE LIFECYCLE MANAGEMENT DTOS =====
    public class UpdateChequeStatusRequest
    {
        public int PaymentID { get; set; }
        public string PaymentType { get; set; } = "Customer"; // "Customer" or "Company"
        public string NewStatus { get; set; } = "Cleared"; // Cleared | Bounced | Cancelled
        public string? Remarks { get; set; }
    }

    public class ChequeDetailDto
    {
        public int PaymentID { get; set; }
        public string PaymentType { get; set; } = "Customer";
        public string PaymentNumber { get; set; } = string.Empty;
        public string PartyName { get; set; } = string.Empty;
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cheque";
        public string? ChequeNumber { get; set; }
        public string? BankName { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ChequeDate { get; set; }
        public string ChequeStatus { get; set; } = "Received";
        public DateTime? ClearedAt { get; set; }
        public DateTime? BouncedAt { get; set; }
        public string? BounceRemarks { get; set; }
        public string RecordedByUserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ChequeFilterDto
    {
        public string? PaymentType { get; set; } // Customer | Company | All
        public string? Status { get; set; } // Received | Cleared | Bounced | Cancelled
        public string? SearchTerm { get; set; } // Cheque # or Party or Invoice #
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
