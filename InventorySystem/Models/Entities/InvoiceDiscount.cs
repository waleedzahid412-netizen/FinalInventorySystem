using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Historical snapshot of a discount applied to a SalesInvoice (Automatic rule or Manual).
    /// Immutable at application time (BR-031). Returns MUST use these snapshotted fields —
    /// never live DiscountRules.MinimumOrderAmount / DiscountValue.
    /// DiscountRuleID is null for Manual discounts.
    /// No IsDeleted on this table per SCHEMA.md.
    /// </summary>
    [Table("InvoiceDiscounts", Schema = "dbo")]
    public class InvoiceDiscount
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InvoiceDiscountID { get; set; }

        [ForeignKey("SalesInvoice")]
        public int InvoiceID { get; set; }
        public virtual SalesInvoice SalesInvoice { get; set; } = null!;

        /// <summary>Null when DiscountSource = Manual (no DiscountRule).</summary>
        [ForeignKey("DiscountRule")]
        public int? DiscountRuleID { get; set; }
        public virtual DiscountRule? DiscountRule { get; set; }

        /// <summary>Snapshot of rule name (or "Manual Discount") at time of application — immutable.</summary>
        [Required]
        [MaxLength(100)]
        public string RuleName { get; set; } = string.Empty;

        /// <summary>Snapshot of type (Percentage/FixedAmount) at time of application — immutable.</summary>
        [Required]
        [MaxLength(20)]
        public string DiscountType { get; set; } = string.Empty;

        /// <summary>Snapshot of rate/value at time of application — immutable.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }

        /// <summary>Actual monetary amount discounted from the invoice.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        /// <summary>
        /// Snapshotted Automatic threshold at sale. Null/0 for Manual.
        /// Returns must use this value — never live DiscountRules.MinimumOrderAmount.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinimumOrderAmount { get; set; }

        /// <summary>Optional snapshotted upper bound at sale. Null for Manual or unbounded rules.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaximumOrderAmount { get; set; }

        /// <summary>Automatic | Manual</summary>
        [Required]
        [MaxLength(20)]
        public string DiscountSource { get; set; } = "Automatic";

        [ForeignKey("AppliedByUser")]
        public int AppliedBy { get; set; }
        public virtual User AppliedByUser { get; set; } = null!;
    }
}
