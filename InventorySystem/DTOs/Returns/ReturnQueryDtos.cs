using System;

namespace InventorySystem.DTOs.Returns
{
    public class SalesReturnFilterDto
    {
        public string? ReturnNumber { get; set; }
        public string? InvoiceNumber { get; set; }
        public int? CustomerID { get; set; }
        /// <summary>Soft-scope: when set, filter invoice returns by SalesInvoice.CompanyID (manual returns by product company).</summary>
        public int? CompanyID { get; set; }
        public string? ReturnType { get; set; } // INVOICE | MANUAL
        public string? SettlementMethod { get; set; } // CASH | ACCOUNT_ADJUSTMENT
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class SalesReturnListDto
    {
        public int SalesReturnID { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public string ReturnType { get; set; } = "INVOICE"; // INVOICE | MANUAL
        public int? InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string SettlementMethod { get; set; } = "ACCOUNT_ADJUSTMENT"; // CASH | ACCOUNT_ADJUSTMENT
        public DateTime ReturnDate { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal ClawbackPenalty { get; set; }
        public decimal PromoPenalty { get; set; }
        public decimal NetRefundAmount { get; set; }
        public int ItemCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PurchaseReturnFilterDto
    {
        public string? ReturnNumber { get; set; }
        public string? InvoiceNumber { get; set; }
        public int? CompanyID { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class PurchaseReturnListDto
    {
        public int PurchaseReturnID { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public int PurchaseInvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public DateTime ReturnDate { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal NetRefundAmount { get; set; }
        public int ItemCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
