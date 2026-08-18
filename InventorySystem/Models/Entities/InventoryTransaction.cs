using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Immutable audit ledger for every inventory movement.
    /// Records are NEVER updated once created — only soft-deleted if correction is needed.
    /// BR-040: Every inventory movement must be recorded.
    /// </summary>
    [Table("InventoryTransactions", Schema = "dbo")]
    public class InventoryTransaction : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TransactionID { get; set; }

        [ForeignKey("Product")]
        public int ProductID { get; set; }
        public virtual Product Product { get; set; } = null!;

        [ForeignKey("Warehouse")]
        public int WarehouseID { get; set; }
        public virtual Warehouse Warehouse { get; set; } = null!;

        /// <summary>
        /// Movement type. Common values:
        /// PURCHASE | SALE | RETURN | RETURN_DAMAGED | PURCHASE_RETURN | ADJUSTMENT | REVERSAL_IN | REVERSAL_OUT | OUT.
        /// RETURN = sellable stock returned (increases InventoryStock.Quantity).
        /// RETURN_DAMAGED = damaged stock returned (increases InventoryStock.DamagedQuantity).
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string TransactionType { get; set; } = string.Empty;

        /// <summary>
        /// Quantity in BASE UNITS. Positive = stock in, Negative = stock out.
        /// For RETURN_DAMAGED, the signed quantity is movement into the damaged bucket
        /// (InventoryStock.DamagedQuantity), not sellable Quantity.
        /// </summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; }

        /// <summary>
        /// External or internal document reference for audit tracing (e.g. Invoice number).
        /// </summary>
        [MaxLength(100)]
        public string? ReferenceNumber { get; set; }

        // Optional source document references — only one is populated per transaction
        [ForeignKey("PurchaseInvoiceItem")]
        public int? PurchaseInvoiceItemID { get; set; }
        public virtual PurchaseInvoiceItem? PurchaseInvoiceItem { get; set; }

        [ForeignKey("SalesInvoiceItem")]
        public int? SalesInvoiceItemID { get; set; }
        public virtual SalesInvoiceItem? SalesInvoiceItem { get; set; }

        [ForeignKey("SalesReturnItem")]
        public int? SalesReturnItemID { get; set; }
        public virtual SalesReturnItem? SalesReturnItem { get; set; }

        [ForeignKey("PurchaseReturnItem")]
        public int? PurchaseReturnItemID { get; set; }
        public virtual PurchaseReturnItem? PurchaseReturnItem { get; set; }

        // ===== AUDIT FIELDS =====
        [ForeignKey("CreatedByUser")]
        public int CreatedBy { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("UpdatedByUser")]
        public int? UpdatedBy { get; set; }
        public virtual User? UpdatedByUser { get; set; }

        // ===== SOFT DELETE =====
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
