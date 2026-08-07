using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Direct 1-to-1 outgoing payment made to a Company against a specific PurchaseInvoice.
    /// BR-023: One Company Payment belongs to one Purchase Invoice.
    /// PurchaseInvoiceID is NOT NULL — enforced at the database level (added in Final Schema).
    /// Saving a payment immediately updates PurchaseInvoice.PaidAmount and CompanyLedger.
    /// </summary>
    [Table("CompanyPayments", Schema = "dbo")]
    public class CompanyPayment : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CompanyPaymentID { get; set; }

        [ForeignKey("Company")]
        public int CompanyID { get; set; }
        public virtual Company Company { get; set; } = null!;

        /// <summary>
        /// The purchase invoice this payment settles. NOT NULL — enforces BR-023 at the database level.
        /// </summary>
        [ForeignKey("PurchaseInvoice")]
        public int PurchaseInvoiceID { get; set; }
        public virtual PurchaseInvoice PurchaseInvoice { get; set; } = null!;

        public DateTime PaymentDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>Cash | Bank | Cheque</summary>
        [Required]
        [MaxLength(30)]
        public string PaymentMethod { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ReferenceNumber { get; set; }

        [ForeignKey("PaidByUser")]
        public int PaidBy { get; set; }
        public virtual User PaidByUser { get; set; } = null!;

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
        public virtual CompanyLedger? CompanyLedgerEntry { get; set; }
    }
}
