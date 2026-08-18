using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    [Table("InventoryStock", Schema = "dbo")]
    public class InventoryStock : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InventoryStockID { get; set; }

        [ForeignKey("Product")]
        public int ProductID { get; set; }
        public virtual Product Product { get; set; } = null!;

        [ForeignKey("Warehouse")]
        public int WarehouseID { get; set; }
        public virtual Warehouse Warehouse { get; set; } = null!;

        /// <summary>
        /// Sellable on-hand quantity in BASE UNITS only.
        /// Never goes negative (BR-009). Used by sales availability and deductions.
        /// Updated immediately upon Purchase finalization (BR-006), Sales finalization (BR-007),
        /// and Sellable sales returns. Damaged returns must NOT increase this field.
        /// NOTE: Historical damaged returns before DamagedQuantity may have been added here; no backfill.
        /// </summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; } = 0;

        /// <summary>
        /// Damaged / non-sellable on-hand quantity in base units.
        /// This quantity must never be available for sale.
        /// </summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal DamagedQuantity { get; set; } = 0;

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
    }
}
