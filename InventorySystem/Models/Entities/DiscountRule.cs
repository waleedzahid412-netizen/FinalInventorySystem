using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Defines invoice-level discount tier rules based on order value.
    /// System suggests applicable rules — seller decides whether to apply (BR-029, BR-030).
    /// DiscountType: Percentage = % off, FixedAmount = absolute amount off.
    /// Historical invoices store snapshots via InvoiceDiscounts — rule changes never affect past invoices (BR-031).
    /// </summary>
    [Table("DiscountRules", Schema = "dbo")]
    public class DiscountRule : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DiscountRuleID { get; set; }

        [Required]
        [MaxLength(100)]
        public string RuleName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinimumOrderAmount { get; set; } = 5000;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaximumOrderAmount { get; set; }

        /// <summary>Percentage | FixedAmount</summary>
        [Required]
        [MaxLength(20)]
        public string DiscountType { get; set; } = "Percentage";

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; } = 0;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        public int Priority { get; set; } = 1;

        /// <summary>Required company scope — rules only evaluate on matching invoices.</summary>
        [ForeignKey("Company")]
        public int CompanyID { get; set; }
        public virtual Company Company { get; set; } = null!;

        // ===== AUDIT FIELDS =====
        [ForeignKey("CreatedByUser")]
        public int CreatedBy { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ===== SOFT DELETE =====
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // ===== RELATIONSHIPS =====
        public virtual ICollection<InvoiceDiscount> InvoiceDiscounts { get; set; } = new List<InvoiceDiscount>();
    }
}
