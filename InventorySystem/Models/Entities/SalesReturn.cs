using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Sales Return header. Never modifies the original SalesInvoice directly (BR-018).
    /// Always references the original invoice (BR-032).
    /// Completing a return immediately increases InventoryStock (BR-035) and credits CustomerLedger (BR-036).
    /// </summary>
    [Table("SalesReturns", Schema = "dbo")]
    public class SalesReturn : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SalesReturnID { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReturnNumber { get; set; } = string.Empty;

        /// <summary>INVOICE = return against invoice, MANUAL = direct return without invoice.</summary>
        [Required]
        [MaxLength(20)]
        public string ReturnType { get; set; } = "INVOICE";

        /// <summary>Original invoice this return is against — nullable for MANUAL returns.</summary>
        [ForeignKey("SalesInvoice")]
        public int? InvoiceID { get; set; }
        public virtual SalesInvoice? SalesInvoice { get; set; }

        [ForeignKey("Customer")]
        public int CustomerID { get; set; }
        public virtual Customer Customer { get; set; } = null!;

        /// <summary>Warehouse restocked — nullable for invoice returns (derived from invoice), set for manual returns.</summary>
        [ForeignKey("Warehouse")]
        public int? WarehouseID { get; set; }
        public virtual Warehouse? Warehouse { get; set; }

        /// <summary>ACCOUNT_ADJUSTMENT = customer ledger credit, CASH = cash paid out (no ledger entry).</summary>
        [Required]
        [MaxLength(30)]
        public string SettlementMethod { get; set; } = "ACCOUNT_ADJUSTMENT";

        public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

        [MaxLength(255)]
        public string? Reason { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ClawbackPenalty { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PromoPenalty { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetRefundAmount { get; set; } = 0;

        /// <summary>When true, promotion/scheme benefits are adjusted proportionally on return.</summary>
        public bool IncludeSchemeCalculation { get; set; } = true;

        // ===== AUDIT FIELDS =====
        [ForeignKey("CreatedByUser")]
        public int CreatedBy { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ===== SOFT DELETE =====
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // ===== RELATIONSHIPS =====
        public virtual ICollection<SalesReturnItem> Items { get; set; } = new List<SalesReturnItem>();
        public virtual ICollection<CustomerLedger> LedgerEntries { get; set; } = new List<CustomerLedger>();
    }
}
