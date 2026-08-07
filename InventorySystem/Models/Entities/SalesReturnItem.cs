using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Line items for a SalesReturn. Each line references the exact original SalesInvoiceItem.
    /// Returned quantity cannot exceed originally sold quantity (BR-033).
    /// Only products on the original invoice may be returned (BR-034).
    /// RefundUnitPrice is frozen snapshot from original invoice item.
    /// </summary>
    [Table("SalesReturnItems", Schema = "dbo")]
    public class SalesReturnItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SalesReturnItemID { get; set; }

        [ForeignKey("SalesReturn")]
        public int SalesReturnID { get; set; }
        public virtual SalesReturn SalesReturn { get; set; } = null!;

        /// <summary>The original invoice line being returned — nullable for MANUAL returns.</summary>
        [ForeignKey("SalesInvoiceItem")]
        public int? InvoiceItemID { get; set; }
        public virtual SalesInvoiceItem? SalesInvoiceItem { get; set; }

        [ForeignKey("Product")]
        public int ProductID { get; set; }
        public virtual Product Product { get; set; } = null!;

        [ForeignKey("ProductUnit")]
        public int ProductUnitID { get; set; }
        public virtual ProductUnit ProductUnit { get; set; } = null!;

        /// <summary>Quantity returned in packaging unit.</summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; }

        /// <summary>Quantity converted to base units for inventory restocking.</summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal ConvertedQuantity { get; set; }

        /// <summary>Unit price snapshot at time of sale.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundUnitPrice { get; set; } = 0;

        /// <summary>Line total refund amount granted.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundAmount { get; set; } = 0;

        [MaxLength(50)]
        public string? Reason { get; set; }

        /// <summary>Sellable | Damaged</summary>
        [MaxLength(20)]
        public string ReturnCondition { get; set; } = "Sellable";

        // ===== RELATIONSHIPS =====
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    }
}
