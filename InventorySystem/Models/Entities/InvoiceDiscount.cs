using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Historical snapshot of a DiscountRule applied to a SalesInvoice.
    /// Immutable record — snapshots are frozen at the time of application (BR-031).
    /// All fields (RuleName, DiscountType, DiscountValue) are snapshots to protect
    /// historical invoices from future rule changes.
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

        [ForeignKey("DiscountRule")]
        public int DiscountRuleID { get; set; }
        public virtual DiscountRule DiscountRule { get; set; } = null!;

        /// <summary>Snapshot of rule name at time of application — immutable.</summary>
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

        [ForeignKey("AppliedByUser")]
        public int AppliedBy { get; set; }
        public virtual User AppliedByUser { get; set; } = null!;
    }
}
