using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Records FIFO layer consumption for a sales line (or reversal/return restore).
    /// Immutable audit trail — never updated, only soft-deleted on correction.
    /// </summary>
    [Table("InventoryCostConsumptions", Schema = "dbo")]
    public class InventoryCostConsumption : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ConsumptionID { get; set; }

        [ForeignKey("CostLayer")]
        public int CostLayerID { get; set; }
        public virtual InventoryCostLayer CostLayer { get; set; } = null!;

        [ForeignKey("SalesInvoiceItem")]
        public int? SalesInvoiceItemID { get; set; }
        public virtual SalesInvoiceItem? SalesInvoiceItem { get; set; }

        /// <summary>SALE | REVERSAL | RETURN_RESTORE</summary>
        [Required]
        [MaxLength(20)]
        public string TransactionType { get; set; } = "SALE";

        /// <summary>Base units consumed (positive) or restored (negative on REVERSAL).</summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitCostInBase { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CreatedByUser")]
        public int CreatedBy { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
