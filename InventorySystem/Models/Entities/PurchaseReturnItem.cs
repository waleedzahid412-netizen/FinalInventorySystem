using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    [Table("PurchaseReturnItems", Schema = "dbo")]
    public class PurchaseReturnItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PurchaseReturnItemID { get; set; }

        [ForeignKey("PurchaseReturn")]
        public int PurchaseReturnID { get; set; }
        public virtual PurchaseReturn PurchaseReturn { get; set; } = null!;

        [ForeignKey("PurchaseInvoiceItem")]
        public int PurchaseInvoiceItemID { get; set; }
        public virtual PurchaseInvoiceItem PurchaseInvoiceItem { get; set; } = null!;

        [ForeignKey("Product")]
        public int ProductID { get; set; }
        public virtual Product Product { get; set; } = null!;

        [ForeignKey("ProductUnit")]
        public int ProductUnitID { get; set; }
        public virtual ProductUnit ProductUnit { get; set; } = null!;

        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal ConvertedQuantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundUnitCost { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundAmount { get; set; } = 0;

        [MaxLength(50)]
        public string? Reason { get; set; }

        [MaxLength(20)]
        public string ReturnCondition { get; set; } = "Sellable";

        // ===== RELATIONSHIPS =====
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    }
}
