using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Immutable financial audit trail for all company (supplier) account transactions.
    /// No IsDeleted — ledger entries are permanent records (BR-041).
    /// CreditAmount increases the company's payable balance (we owe more to the company).
    /// DebitAmount decreases the company's payable balance (payment reduces what we owe).
    /// </summary>
    [Table("CompanyLedger", Schema = "dbo")]
    public class CompanyLedger
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CompanyLedgerID { get; set; }

        [ForeignKey("Company")]
        public int CompanyID { get; set; }
        public virtual Company Company { get; set; } = null!;

        public DateTime TransactionDate { get; set; }

        /// <summary>PURCHASE | PAYMENT | ADJUSTMENT</summary>
        [Required]
        [MaxLength(30)]
        public string TransactionType { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DebitAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditAmount { get; set; } = 0;

        // Optional source document references
        [ForeignKey("PurchaseInvoice")]
        public int? PurchaseInvoiceID { get; set; }
        public virtual PurchaseInvoice? PurchaseInvoice { get; set; }

        [ForeignKey("CompanyPayment")]
        public int? CompanyPaymentID { get; set; }
        public virtual CompanyPayment? CompanyPayment { get; set; }

        [ForeignKey("PurchaseReturn")]
        public int? PurchaseReturnID { get; set; }
        public virtual PurchaseReturn? PurchaseReturn { get; set; }

        [MaxLength(255)]
        public string? Description { get; set; }

        [ForeignKey("CreatedByUser")]
        public int CreatedBy { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
