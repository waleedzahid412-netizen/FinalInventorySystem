using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// One settlement entry against a damaged-stock claim.
    /// Multiple methods may be combined on a single claim.
    /// </summary>
    [Table("DamagedStockClaimSettlements", Schema = "dbo")]
    public class DamagedStockClaimSettlement : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DamagedStockClaimSettlementID { get; set; }

        [ForeignKey("DamagedStockClaim")]
        public int DamagedStockClaimID { get; set; }
        public virtual DamagedStockClaim DamagedStockClaim { get; set; } = null!;

        /// <summary>
        /// SameProductReplacement | DifferentProductReplacement | LedgerAdjustment | CashRefund
        /// </summary>
        [Required]
        [MaxLength(40)]
        public string SettlementMethod { get; set; } = string.Empty;

        public DateTime SettlementDate { get; set; } = DateTime.UtcNow;

        /// <summary>Monetary value applied toward the claim settlement.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [ForeignKey("Product")]
        public int? ProductID { get; set; }
        public virtual Product? Product { get; set; }

        [ForeignKey("Warehouse")]
        public int? WarehouseID { get; set; }
        public virtual Warehouse? Warehouse { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal? ReceivedQuantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? UnitCostInBase { get; set; }

        /// <summary>Replacement stock is posted to sellable inventory only when verified.</summary>
        public bool IsVerifiedReceived { get; set; } = false;

        /// <summary>True after inventory and/or ledger side-effects have been posted.</summary>
        public bool IsPosted { get; set; } = false;

        /// <summary>Cash | Bank | Cheque — used for CashRefund.</summary>
        [MaxLength(20)]
        public string? PaymentMethod { get; set; }

        [MaxLength(100)]
        public string? BankName { get; set; }

        [MaxLength(100)]
        public string? ReferenceNumber { get; set; }

        [ForeignKey("CompanyLedger")]
        public int? CompanyLedgerID { get; set; }
        public virtual CompanyLedger? CompanyLedger { get; set; }

        [ForeignKey("InventoryTransaction")]
        public int? InventoryTransactionID { get; set; }
        public virtual InventoryTransaction? InventoryTransaction { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CreatedByUser")]
        public int CreatedBy { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
