using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    [Table("Companies", Schema = "dbo")]
    public class Company : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CompanyID { get; set; }

        [Required]
        [MaxLength(150)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? TaxID { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditLimit { get; set; } = 0;

        /// <summary>
        /// Optional company percentage (e.g. commission / share). Null when not set.
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal? CompanyPercentage { get; set; }

        // ===== AUDIT FIELDS =====
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("UpdatedByUser")]
        public int? UpdatedBy { get; set; }
        public virtual User? UpdatedByUser { get; set; }

        // ===== SOFT DELETE =====
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // ===== RELATIONSHIPS =====
        public virtual ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
        public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
        public virtual ICollection<Booker> Bookers { get; set; } = new List<Booker>();
        public virtual ICollection<PurchaseInvoice> PurchaseInvoices { get; set; } = new List<PurchaseInvoice>();
        public virtual ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();
        public virtual ICollection<PromotionCampaign> PromotionCampaigns { get; set; } = new List<PromotionCampaign>();
        public virtual ICollection<DiscountRule> DiscountRules { get; set; } = new List<DiscountRule>();
        public virtual ICollection<CompanyPayment> CompanyPayments { get; set; } = new List<CompanyPayment>();
        public virtual ICollection<CompanyLedger> CompanyLedgerEntries { get; set; } = new List<CompanyLedger>();
    }
}
