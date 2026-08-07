using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Optional pre-sale quotation issued to a customer.
    /// Quotations do NOT affect inventory (BR-014).
    /// A quotation can be converted into exactly one SalesInvoice (BR-015).
    /// </summary>
    [Table("Quotations", Schema = "dbo")]
    public class Quotation : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int QuotationID { get; set; }

        [ForeignKey("Customer")]
        public int CustomerID { get; set; }
        public virtual Customer Customer { get; set; } = null!;

        public DateTime QuotationDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        /// <summary>DRAFT | SENT | ACCEPTED | REJECTED | CONVERTED</summary>
        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "DRAFT";

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

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
        public virtual ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();
        public virtual ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();
    }
}
