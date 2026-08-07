using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    [Table("PurchaseReturns", Schema = "dbo")]
    public class PurchaseReturn : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PurchaseReturnID { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReturnNumber { get; set; } = string.Empty;

        [ForeignKey("PurchaseInvoice")]
        public int PurchaseInvoiceID { get; set; }
        public virtual PurchaseInvoice PurchaseInvoice { get; set; } = null!;

        [ForeignKey("Company")]
        public int CompanyID { get; set; }
        public virtual Company Company { get; set; } = null!;

        [ForeignKey("Warehouse")]
        public int WarehouseID { get; set; }
        public virtual Warehouse Warehouse { get; set; } = null!;

        public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

        [MaxLength(255)]
        public string? Reason { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetRefundAmount { get; set; } = 0;

        [ForeignKey("CreatedByUser")]
        public int CreatedBy { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // ===== RELATIONSHIPS =====
        public virtual ICollection<PurchaseReturnItem> Items { get; set; } = new List<PurchaseReturnItem>();
        public virtual ICollection<CompanyLedger> CompanyLedgerEntries { get; set; } = new List<CompanyLedger>();
    }
}
