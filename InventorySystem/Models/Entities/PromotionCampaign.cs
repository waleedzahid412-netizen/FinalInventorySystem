using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Promotional campaign (formerly "Promotions" table — renamed to PromotionCampaigns in Final Schema).
    /// Promotions are NEVER applied automatically — seller always decides (BR-026).
    /// Campaign validity is controlled by StartDate and EndDate (BR-027).
    /// </summary>
    [Table("PromotionCampaigns", Schema = "dbo")]
    public class PromotionCampaign : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PromotionID { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        // ===== AUDIT FIELDS =====
        [ForeignKey("CreatedByUser")]
        public int CreatedBy { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;

        // ===== SOFT DELETE =====
        // Note: No CreatedAt / UpdatedAt in SCHEMA.md for this table
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // ===== RELATIONSHIPS =====
        public virtual ICollection<PromotionRule> PromotionRules { get; set; } = new List<PromotionRule>();
        public virtual ICollection<SalesInvoiceItem> AppliedInvoiceItems { get; set; } = new List<SalesInvoiceItem>();
        public virtual ICollection<InvoicePromotion> InvoicePromotions { get; set; } = new List<InvoicePromotion>();
    }
}
