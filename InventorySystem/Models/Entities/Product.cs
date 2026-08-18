using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    [Table("Products", Schema = "dbo")]
    public class Product : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductID { get; set; }

        [ForeignKey("Category")]
        public int CategoryID { get; set; }
        public virtual Category Category { get; set; } = null!;

        [ForeignKey("Company")]
        public int CompanyID { get; set; }
        public virtual Company Company { get; set; } = null!;

        /// <summary>
        /// Base unit for inventory tracking (e.g. Piece).
        /// All inventory quantities are stored in this unit.
        /// Formerly named InventoryUnitID — renamed to BaseUnitID in Final Schema.
        /// </summary>
        [ForeignKey("BaseUnit")]
        public int BaseUnitID { get; set; }
        public virtual Unit BaseUnit { get; set; } = null!;

        [Required]
        [MaxLength(150)]
        public string ProductName { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Description { get; set; }

        /// <summary>Stock Keeping Unit code — unique across all products.</summary>
        [MaxLength(50)]
        public string? SKU { get; set; }

        /// <summary>Scannable barcode — unique across all products.</summary>
        [MaxLength(100)]
        public string? Barcode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseSellingPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AveragePurchaseCost { get; set; } = 0;

        public int ReorderLevel { get; set; } = 0;

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
        public virtual ICollection<ProductUnit> ProductUnits { get; set; } = new List<ProductUnit>();
        public virtual ICollection<InventoryStock> InventoryStocks { get; set; } = new List<InventoryStock>();
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
        public virtual ICollection<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; } = new List<PurchaseInvoiceItem>();
        public virtual ICollection<SalesInvoiceItem> SalesInvoiceItems { get; set; } = new List<SalesInvoiceItem>();
        public virtual ICollection<QuotationItem> QuotationItems { get; set; } = new List<QuotationItem>();
        public virtual ICollection<SalesReturnItem> SalesReturnItems { get; set; } = new List<SalesReturnItem>();
        // PromotionRules: this product as BuyProduct
        public virtual ICollection<PromotionRule> BuyPromotionRules { get; set; } = new List<PromotionRule>();
        // PromotionRules: this product as FreeProduct
        public virtual ICollection<PromotionRule> FreePromotionRules { get; set; } = new List<PromotionRule>();
    }
}
