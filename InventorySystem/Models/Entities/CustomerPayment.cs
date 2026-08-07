using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Direct 1-to-1 payment received from a customer against a specific SalesInvoice.
    /// BR-019: One payment belongs to exactly one invoice.
    /// BR-020: One invoice may receive multiple payments.
    /// InvoiceID is NOT NULL — enforced at the database level (corrected in Final Schema).
    /// No PaymentAllocations table exists in this system.
    /// Saving a payment immediately updates Invoice.PaidAmount, CustomerLedger, and outstanding balance (BR-021).
    /// </summary>
    [Table("CustomerPayments", Schema = "dbo")]
    public class CustomerPayment : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CustomerPaymentID { get; set; }

        [ForeignKey("Customer")]
        public int CustomerID { get; set; }
        public virtual Customer Customer { get; set; } = null!;

        /// <summary>
        /// The invoice this payment settles. NOT NULL — enforces BR-019 at the database level.
        /// </summary>
        [ForeignKey("SalesInvoice")]
        public int InvoiceID { get; set; }
        public virtual SalesInvoice SalesInvoice { get; set; } = null!;

        public DateTime PaymentDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>Cash | Bank | Cheque</summary>
        [Required]
        [MaxLength(30)]
        public string PaymentMethod { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ReferenceNumber { get; set; }

        [MaxLength(255)]
        public string? Notes { get; set; }

        [ForeignKey("ReceivedByUser")]
        public int ReceivedBy { get; set; }
        public virtual User ReceivedByUser { get; set; } = null!;

        // ===== CHEQUE LIFECYCLE FIELDS =====
        [MaxLength(50)]
        public string? ChequeNumber { get; set; }

        [MaxLength(100)]
        public string? BankName { get; set; }

        public DateTime? IssueDate { get; set; }
        public DateTime? ChequeDate { get; set; }

        /// <summary>Received | Cleared | Bounced | Cancelled</summary>
        [MaxLength(20)]
        public string ChequeStatus { get; set; } = "Cleared";

        public DateTime? ClearedAt { get; set; }
        public int? ClearedBy { get; set; }

        public DateTime? BouncedAt { get; set; }
        [MaxLength(500)]
        public string? BounceRemarks { get; set; }

        // ===== AUDIT FIELDS =====
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ===== SOFT DELETE =====
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // ===== RELATIONSHIPS =====
        public virtual CustomerLedger? CustomerLedgerEntry { get; set; }
    }
}
