using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Immutable financial audit trail for all customer account transactions.
    /// No IsDeleted — ledger entries are permanent records (BR-041).
    /// DebitAmount increases the customer's outstanding balance (they owe more).
    /// CreditAmount decreases the customer's outstanding balance (they owe less).
    /// Each entry references the source document via optional FKs.
    /// </summary>
    [Table("CustomerLedger", Schema = "dbo")]
    public class CustomerLedger
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CustomerLedgerID { get; set; }

        [ForeignKey("Customer")]
        public int CustomerID { get; set; }
        public virtual Customer Customer { get; set; } = null!;

        public DateTime TransactionDate { get; set; }

        /// <summary>SALE | PAYMENT | RETURN | ADJUSTMENT</summary>
        [Required]
        [MaxLength(30)]
        public string TransactionType { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DebitAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditAmount { get; set; } = 0;

        // Optional source document references (only one populated per entry)
        [ForeignKey("SalesInvoice")]
        public int? SalesInvoiceID { get; set; }
        public virtual SalesInvoice? SalesInvoice { get; set; }

        [ForeignKey("CustomerPayment")]
        public int? CustomerPaymentID { get; set; }
        public virtual CustomerPayment? CustomerPayment { get; set; }

        [ForeignKey("SalesReturn")]
        public int? SalesReturnID { get; set; }
        public virtual SalesReturn? SalesReturn { get; set; }

        [MaxLength(255)]
        public string? Description { get; set; }

        [ForeignKey("CreatedByUser")]
        public int CreatedBy { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
