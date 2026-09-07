using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// FIFO cost layer — one batch of stock received at a specific unit cost (base units).
    /// BR-004: Inventory valuation uses FIFO; oldest layers are consumed first on sale.
    /// </summary>
    [Table("InventoryCostLayers", Schema = "dbo")]
    public class InventoryCostLayer : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CostLayerID { get; set; }

        [ForeignKey("Product")]
        public int ProductID { get; set; }
        public virtual Product Product { get; set; } = null!;

        [ForeignKey("Warehouse")]
        public int WarehouseID { get; set; }
        public virtual Warehouse Warehouse { get; set; } = null!;

        /// <summary>Source purchase line — null for opening-balance or return-restock layers.</summary>
        [ForeignKey("PurchaseInvoiceItem")]
        public int? PurchaseInvoiceItemID { get; set; }
        public virtual PurchaseInvoiceItem? PurchaseInvoiceItem { get; set; }

        /// <summary>When stock was received — drives FIFO ordering.</summary>
        public DateTime ReceivedAt { get; set; }

        /// <summary>PURCHASE | OPENING_BALANCE | RETURN</summary>
        [Required]
        [MaxLength(30)]
        public string SourceType { get; set; } = "PURCHASE";

        [Column(TypeName = "decimal(18,3)")]
        public decimal OriginalQuantity { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal RemainingQuantity { get; set; }

        /// <summary>Cost per base unit for this batch.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitCostInBase { get; set; }

        [ForeignKey("SalesReturnItem")]
        public int? SalesReturnItemID { get; set; }
        public virtual SalesReturnItem? SalesReturnItem { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CreatedByUser")]
        public int? CreatedBy { get; set; }
        public virtual User? CreatedByUser { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public virtual ICollection<InventoryCostConsumption> Consumptions { get; set; } = new List<InventoryCostConsumption>();
    }
}
