using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    [Table("InvoiceEditAudits", Schema = "dbo")]
    public class InvoiceEditAudit
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InvoiceEditAuditID { get; set; }

        public int InvoiceID { get; set; }

        [Required]
        [MaxLength(20)]
        public string InvoiceType { get; set; } = string.Empty; // "Sales" or "Purchase"

        public int PreviousVersion { get; set; }
        public int NewVersion { get; set; }

        public int PreviousItemCount { get; set; }
        public int NewItemCount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OldSubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NewSubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OldDiscountTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NewDiscountTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OldGrandTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NewGrandTotal { get; set; }

        [Required]
        [MaxLength(500)]
        public string EditReason { get; set; } = string.Empty;

        [ForeignKey("EditedByUser")]
        public int EditedBy { get; set; }
        public virtual User EditedByUser { get; set; } = null!;

        public DateTime EditedAt { get; set; } = DateTime.UtcNow;
    }
}
