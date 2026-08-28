using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Finalized sales invoice. Immutable once any payment is received (BR-017).
    /// Finalizing immediately deducts inventory (BR-007).
    /// IsLocked = true once a payment is made — prevents any editing.
    /// AreaID/SubAreaID are delivery location snapshots inherited from Customer at time of sale.
    /// </summary>
    [Table("SalesInvoices", Schema = "dbo")]
    public class SalesInvoice : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InvoiceID { get; set; }

        [ForeignKey("Customer")]
        public int CustomerID { get; set; }
        public virtual Customer Customer { get; set; } = null!;

        /// <summary>Invoice-level supplier (manufacturer/company). Required — one invoice, one company (BR-044).</summary>
        [ForeignKey("Company")]
        public int CompanyID { get; set; }
        public virtual Company Company { get; set; } = null!;

        /// <summary>External person who brought/placed the order.</summary>
        [ForeignKey("Booker")]
        public int? BookerID { get; set; }
        public virtual Booker? Booker { get; set; }

        /// <summary>Internal salesperson responsible for the order.</summary>
        [ForeignKey("SalespersonUser")]
        public int? SalespersonID { get; set; }
        public virtual User? SalespersonUser { get; set; }

        [ForeignKey("Warehouse")]
        public int WarehouseID { get; set; }
        public virtual Warehouse Warehouse { get; set; } = null!;

        // Delivery location snapshot — copied from Customer at time of invoice creation
        [ForeignKey("Area")]
        public int? AreaID { get; set; }
        public virtual Area? Area { get; set; }

        [ForeignKey("SubArea")]
        public int? SubAreaID { get; set; }
        public virtual SubArea? SubArea { get; set; }

        /// <summary>Global supplier (person) assigned to fulfill/deliver this invoice. Not company-scoped.</summary>
        [ForeignKey("Supplier")]
        public int? SupplierID { get; set; }
        public virtual Supplier? Supplier { get; set; }

        // Source quotation if this invoice was converted from one
        [ForeignKey("Quotation")]
        public int? QuotationID { get; set; }
        public virtual Quotation? Quotation { get; set; }

        [ForeignKey("CreatedByUser")]
        public int CreatedBy { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountTotal { get; set; } = 0;

        /// <summary>
        /// None = no invoice discount applied.
        /// Automatic = threshold DiscountRule applied at sale (snapshot in InvoiceDiscounts).
        /// Manual = seller-entered percentage or fixed amount (no threshold).
        /// Customer = seller applied customer's PreferredDiscountPercent (snapshot; no threshold).
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string DiscountMode { get; set; } = "None";

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxTotal { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrandTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; } = 0;

        /// <summary>UNPAID | PARTIAL | PAID</summary>
        [Required]
        [MaxLength(30)]
        public string PaymentStatus { get; set; } = "UNPAID";

        /// <summary>
        /// Locked from editing once any payment is made (BR-017).
        /// Use SalesReturns to correct a finalized invoice (BR-018).
        /// </summary>
        public bool IsLocked { get; set; } = false;

        public int Version { get; set; } = 1;

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

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
        public virtual ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
        public virtual ICollection<CustomerPayment> CustomerPayments { get; set; } = new List<CustomerPayment>();
        public virtual ICollection<CustomerLedger> LedgerEntries { get; set; } = new List<CustomerLedger>();
        public virtual ICollection<SalesReturn> SalesReturns { get; set; } = new List<SalesReturn>();
        public virtual ICollection<InvoicePromotion> InvoicePromotions { get; set; } = new List<InvoicePromotion>();
        public virtual ICollection<InvoiceDiscount> InvoiceDiscounts { get; set; } = new List<InvoiceDiscount>();
    }
}
