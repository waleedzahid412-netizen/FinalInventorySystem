using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    [Table("PurchaseInvoiceItems", Schema = "dbo")]
    public class PurchaseInvoiceItem : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PurchaseItemID { get; set; }

        [ForeignKey("PurchaseInvoice")]
        public int PurchaseInvoiceID { get; set; }
        public virtual PurchaseInvoice PurchaseInvoice { get; set; } = null!;

        [ForeignKey("Product")]
        public int ProductID { get; set; }
        public virtual Product Product { get; set; } = null!;

        [ForeignKey("ProductUnit")]
        public int ProductUnitID { get; set; }
        public virtual ProductUnit ProductUnit { get; set; } = null!;

        /// <summary>Quantity purchased in the invoice packaging unit (e.g. Cartons).</summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; }

        /// <summary>
        /// Quantity converted to base inventory units (e.g. Pieces).
        /// = Quantity × ProductUnit.ConversionToBaseUnit.
        /// </summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal ConvertedQuantity { get; set; }

        /// <summary>Cost per invoice unit at time of purchase — historical snapshot.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        /// <summary>Line total = Quantity × UnitCost — historical snapshot.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        public bool IsActive { get; set; } = true;

        // ===== AUDIT FIELDS =====
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CreatedByUser")]
        public int? CreatedBy { get; set; }
        public virtual User? CreatedByUser { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("UpdatedByUser")]
        public int? UpdatedBy { get; set; }
        public virtual User? UpdatedByUser { get; set; }

        // ===== SOFT DELETE =====
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // ===== RELATIONSHIPS =====
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    }
}
