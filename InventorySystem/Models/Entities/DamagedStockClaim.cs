using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Header for a damaged-stock claim against a Company (brand/manufacturer).
    /// CompanyID is stamped from ambient company scope — never from the form.
    /// </summary>
    [Table("DamagedStockClaims", Schema = "dbo")]
    public class DamagedStockClaim : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DamagedStockClaimID { get; set; }

        [Required]
        [MaxLength(50)]
        public string ClaimNumber { get; set; } = string.Empty;

        [ForeignKey("Company")]
        public int CompanyID { get; set; }
        public virtual Company Company { get; set; } = null!;

        public DateTime ClaimDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// PendingSupplierConfirmation | PartiallyAccepted | Accepted | Rejected | Closed
        /// </summary>
        [Required]
        [MaxLength(40)]
        public string ClaimStatus { get; set; } = "PendingSupplierConfirmation";

        /// <summary>
        /// NotRequired | Pending | PartiallySettled | Settled
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string SettlementStatus { get; set; } = "Pending";

        [Column(TypeName = "decimal(18,3)")]
        public decimal TotalClaimedQuantity { get; set; } = 0;

        [Column(TypeName = "decimal(18,3)")]
        public decimal TotalAcceptedQuantity { get; set; } = 0;

        [Column(TypeName = "decimal(18,3)")]
        public decimal TotalRejectedQuantity { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalClaimAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSettledAmount { get; set; } = 0;

        [MaxLength(500)]
        public string? Remarks { get; set; }

        [MaxLength(500)]
        public string? SupplierResponseNotes { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        [ForeignKey("ConfirmedByUser")]
        public int? ConfirmedBy { get; set; }
        public virtual User? ConfirmedByUser { get; set; }

        public DateTime? ClosedAt { get; set; }

        [ForeignKey("ClosedByUser")]
        public int? ClosedBy { get; set; }
        public virtual User? ClosedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CreatedByUser")]
        public int CreatedBy { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("UpdatedByUser")]
        public int? UpdatedBy { get; set; }
        public virtual User? UpdatedByUser { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public virtual ICollection<DamagedStockClaimItem> Items { get; set; } = new List<DamagedStockClaimItem>();
        public virtual ICollection<DamagedStockClaimSettlement> Settlements { get; set; } = new List<DamagedStockClaimSettlement>();
        public virtual ICollection<CompanyLedger> CompanyLedgerEntries { get; set; } = new List<CompanyLedger>();
    }
}
