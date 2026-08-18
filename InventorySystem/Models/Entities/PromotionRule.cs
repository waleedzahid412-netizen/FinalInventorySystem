using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Defines the Buy X Get Y Free rules for a PromotionCampaign.
    /// Example: Buy 10 Coca-Cola → Get 2 Pepsi Free.
    /// BuyProductID and FreeProductID both FK to Products — configured with NoAction in Fluent API
    /// to avoid multiple cascade path conflict in SQL Server.
    /// No IsDeleted on this table per SCHEMA.md.
    /// </summary>
    [Table("PromotionRules", Schema = "dbo")]
    public class PromotionRule
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RuleID { get; set; }

        [ForeignKey("PromotionCampaign")]
        public int PromotionID { get; set; }
        public virtual PromotionCampaign PromotionCampaign { get; set; } = null!;

        [ForeignKey("BuyProduct")]
        public int BuyProductID { get; set; }
        public virtual Product BuyProduct { get; set; } = null!;

        public int BuyQuantity { get; set; }

        /// <summary>Real product reward. Null when IsCustomFreeItem is true.</summary>
        [ForeignKey("FreeProduct")]
        public int? FreeProductID { get; set; }
        public virtual Product? FreeProduct { get; set; }

        /// <summary>True when free reward is a non-inventory "Other" custom item.</summary>
        public bool IsCustomFreeItem { get; set; }

        /// <summary>Display name for custom free item (e.g. Free Mug). Max 200.</summary>
        [MaxLength(200)]
        public string? CustomFreeItemName { get; set; }

        public int FreeQuantity { get; set; }
    }
}
