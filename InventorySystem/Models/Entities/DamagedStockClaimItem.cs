using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Line on a damaged-stock claim. Quantities are always in base units.
    /// </summary>
    [Table("DamagedStockClaimItems", Schema = "dbo")]
    public class DamagedStockClaimItem : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DamagedStockClaimItemID { get; set; }

        [ForeignKey("DamagedStockClaim")]
        public int DamagedStockClaimID { get; set; }
        public virtual DamagedStockClaim DamagedStockClaim { get; set; } = null!;

        [ForeignKey("Product")]
        public int ProductID { get; set; }
        public virtual Product Product { get; set; } = null!;

        [ForeignKey("Warehouse")]
        public int WarehouseID { get; set; }
        public virtual Warehouse Warehouse { get; set; } = null!;

        /// <summary>Original InventoryStock row referenced at claim time.</summary>
        [ForeignKey("InventoryStock")]
        public int InventoryStockID { get; set; }
        public virtual InventoryStock InventoryStock { get; set; } = null!;

        /// <summary>Snapshot of DamagedQuantity available when the claim line was created.</summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal DamagedQuantityAvailableAtClaim { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal ClaimedQuantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitClaimCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineClaimAmount { get; set; }

        /// <summary>Null until supplier confirmation is recorded.</summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal? AcceptedQuantity { get; set; }

        /// <summary>Null until supplier confirmation is recorded.</summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal? RejectedQuantity { get; set; }

        /// <summary>None | ReturnToDamaged | WriteOff | KeepPending</summary>
        [Required]
        [MaxLength(30)]
        public string RejectedDecision { get; set; } = "None";

        public DateTime? RejectedDecisionAt { get; set; }

        [ForeignKey("RejectedDecisionByUser")]
        public int? RejectedDecisionBy { get; set; }
        public virtual User? RejectedDecisionByUser { get; set; }

        [MaxLength(255)]
        public string? DamageReason { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    }
}
