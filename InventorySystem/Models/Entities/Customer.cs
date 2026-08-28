using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    [Table("Customers", Schema = "dbo")]
    public class Customer : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CustomerID { get; set; }

        [Required]
        [MaxLength(150)]
        public string ShopName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? OwnerName { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

        // Default delivery area — nullable (customer may not have an assigned area)
        [ForeignKey("Area")]
        public int? AreaID { get; set; }
        public virtual Area? Area { get; set; }

        // Default delivery sub-area — nullable
        [ForeignKey("SubArea")]
        public int? SubAreaID { get; set; }
        public virtual SubArea? SubArea { get; set; }

        [MaxLength(50)]
        public string? TaxID { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditLimit { get; set; } = 0;

        /// <summary>
        /// Optional customer-level preferred invoice discount percentage (0–100).
        /// Null means no preferred discount. Seller must explicitly Apply on the invoice.
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal? PreferredDiscountPercent { get; set; }

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
        public virtual ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();
        public virtual ICollection<CustomerPayment> CustomerPayments { get; set; } = new List<CustomerPayment>();
        public virtual ICollection<CustomerLedger> LedgerEntries { get; set; } = new List<CustomerLedger>();
        public virtual ICollection<SalesReturn> SalesReturns { get; set; } = new List<SalesReturn>();
        public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
    }
}
