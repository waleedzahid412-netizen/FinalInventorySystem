using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Historical snapshot of a PromotionCampaign applied to a SalesInvoice.
    /// Immutable record — never updated after creation (BR-028).
    /// No IsDeleted on this table per SCHEMA.md.
    /// </summary>
    [Table("InvoicePromotions", Schema = "dbo")]
    public class InvoicePromotion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InvoicePromotionID { get; set; }

        [ForeignKey("SalesInvoice")]
        public int InvoiceID { get; set; }
        public virtual SalesInvoice SalesInvoice { get; set; } = null!;

        [ForeignKey("PromotionCampaign")]
        public int PromotionID { get; set; }
        public virtual PromotionCampaign PromotionCampaign { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0;

        [ForeignKey("AppliedByUser")]
        public int AppliedBy { get; set; }
        public virtual User AppliedByUser { get; set; } = null!;
    }
}
