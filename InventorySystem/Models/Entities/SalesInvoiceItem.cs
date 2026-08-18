using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Sales invoice line item. Stores historical price/discount snapshot (BR-003).
    /// ItemType = NORMAL for regular sold items, FREE for promotional free items.
    /// ConvertedQuantity drives the base-unit inventory deduction.
    /// </summary>
    [Table("SalesInvoiceItems", Schema = "dbo")]
    public class SalesInvoiceItem : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InvoiceItemID { get; set; }

        [ForeignKey("SalesInvoice")]
        public int InvoiceID { get; set; }
        public virtual SalesInvoice SalesInvoice { get; set; } = null!;

        /// <summary>Null for non-inventory custom FREE promotional items.</summary>
        [ForeignKey("Product")]
        public int? ProductID { get; set; }
        public virtual Product? Product { get; set; }

        /// <summary>Null for non-inventory custom FREE promotional items.</summary>
        [ForeignKey("ProductUnit")]
        public int? ProductUnitID { get; set; }
        public virtual ProductUnit? ProductUnit { get; set; }

        /// <summary>Snapshot name for custom FREE items (e.g. Free Mug). Shown as "Other: {name}".</summary>
        [MaxLength(200)]
        public string? CustomItemName { get; set; }

        /// <summary>Quantity sold in the packaging unit (e.g. Cartons).</summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; }

        /// <summary>Quantity converted to base units — used for inventory deduction.</summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal ConvertedQuantity { get; set; }

        /// <summary>
        /// Selling price per packaging unit at time of sale — historical snapshot, never changes.
        /// NORMAL sales returns MUST use this value; never ProductUnit.SellingPrice or Product.BaseSellingPrice.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Discount percentage rate allocated to this line at sale (0 when FixedAmount or no discount).
        /// Historical snapshot for returns — never re-read from live DiscountRules.
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal DiscountRate { get; set; } = 0;

        /// <summary>
        /// Monetary discount allocated to this line at sale (server-allocated from invoice-level Automatic/Manual discount).
        /// Historical snapshot used when releasing discount on returns.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0;

        /// <summary>
        /// NORMAL = regular sale item.
        /// FREE = promotional free item (UnitPrice=0, linked to a PromotionCampaign).
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string ItemType { get; set; } = "NORMAL";

        // Promotion that produced this FREE item — nullable (only set for FREE items)
        [ForeignKey("PromotionCampaign")]
        public int? PromotionID { get; set; }
        public virtual PromotionCampaign? PromotionCampaign { get; set; }

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
        public virtual ICollection<SalesReturnItem> SalesReturnItems { get; set; } = new List<SalesReturnItem>();
    }
}
