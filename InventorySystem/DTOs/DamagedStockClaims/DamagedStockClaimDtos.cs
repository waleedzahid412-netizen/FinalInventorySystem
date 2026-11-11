using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.DTOs.DamagedStockClaims
{
    public class DamagedStockClaimFilterDto
    {
        public int? CompanyID { get; set; }
        public string? ClaimStatus { get; set; }
        public string? SettlementStatus { get; set; }
        public string? SearchTerm { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class DamagedStockRowDto
    {
        public int InventoryStockID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public decimal DamagedQuantity { get; set; }
        public decimal ClaimPendingQuantity { get; set; }
        public decimal SuggestedUnitCost { get; set; }
    }

    public class DamagedStockClaimListItemDto
    {
        public int DamagedStockClaimID { get; set; }
        public string ClaimNumber { get; set; } = string.Empty;
        public DateTime ClaimDate { get; set; }
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int TotalProducts { get; set; }
        public decimal TotalClaimedQuantity { get; set; }
        public decimal TotalClaimAmount { get; set; }
        public string ClaimStatus { get; set; } = string.Empty;
        public string SettlementStatus { get; set; } = string.Empty;
    }

    public class CreateDamagedStockClaimLineDto
    {
        [Required]
        public int InventoryStockID { get; set; }

        [Range(0.001, double.MaxValue)]
        public decimal ClaimQuantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal UnitClaimCost { get; set; }

        [MaxLength(255)]
        public string? DamageReason { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }
    }

    public class CreateDamagedStockClaimRequest
    {
        public DateTime? ClaimDate { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }

        [MinLength(1)]
        public List<CreateDamagedStockClaimLineDto> Lines { get; set; } = new();
    }

    public class ConfirmClaimLineDto
    {
        [Required]
        public int DamagedStockClaimItemID { get; set; }

        [Range(0, double.MaxValue)]
        public decimal AcceptedQuantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal RejectedQuantity { get; set; }
    }

    public class ConfirmDamagedStockClaimRequest
    {
        [MaxLength(500)]
        public string? SupplierResponseNotes { get; set; }

        [MinLength(1)]
        public List<ConfirmClaimLineDto> Lines { get; set; } = new();
    }

    public class RejectedStockDecisionRequest
    {
        [Required]
        public int DamagedStockClaimItemID { get; set; }

        /// <summary>ReturnToDamaged | WriteOff | KeepPending</summary>
        [Required]
        [MaxLength(30)]
        public string Decision { get; set; } = string.Empty;
    }

    public class AddClaimSettlementRequest
    {
        /// <summary>SameProductReplacement | DifferentProductReplacement | LedgerAdjustment | CashRefund</summary>
        [Required]
        [MaxLength(40)]
        public string SettlementMethod { get; set; } = string.Empty;

        public DateTime? SettlementDate { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        public int? ProductID { get; set; }
        public int? WarehouseID { get; set; }

        [Range(0.001, double.MaxValue)]
        public decimal? ReceivedQuantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? UnitCostInBase { get; set; }

        public bool IsVerifiedReceived { get; set; } = true;

        [MaxLength(20)]
        public string? PaymentMethod { get; set; }

        [MaxLength(100)]
        public string? BankName { get; set; }

        [MaxLength(100)]
        public string? ReferenceNumber { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }
    }

    public class DamagedStockClaimItemDetailDto
    {
        public int DamagedStockClaimItemID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public int InventoryStockID { get; set; }
        public decimal DamagedQuantityAvailableAtClaim { get; set; }
        public decimal ClaimedQuantity { get; set; }
        public decimal UnitClaimCost { get; set; }
        public decimal LineClaimAmount { get; set; }
        public decimal? AcceptedQuantity { get; set; }
        public decimal? RejectedQuantity { get; set; }
        public string RejectedDecision { get; set; } = "None";
        public DateTime? RejectedDecisionAt { get; set; }
        public string? DamageReason { get; set; }
        public string? Remarks { get; set; }
    }

    public class DamagedStockClaimSettlementDetailDto
    {
        public int DamagedStockClaimSettlementID { get; set; }
        public string SettlementMethod { get; set; } = string.Empty;
        public DateTime SettlementDate { get; set; }
        public decimal Amount { get; set; }
        public int? ProductID { get; set; }
        public string? ProductName { get; set; }
        public int? WarehouseID { get; set; }
        public string? WarehouseName { get; set; }
        public decimal? ReceivedQuantity { get; set; }
        public decimal? UnitCostInBase { get; set; }
        public bool IsVerifiedReceived { get; set; }
        public bool IsPosted { get; set; }
        public string? PaymentMethod { get; set; }
        public string? BankName { get; set; }
        public string? ReferenceNumber { get; set; }
        public int? CompanyLedgerID { get; set; }
        public int? InventoryTransactionID { get; set; }
        public string? Remarks { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DamagedStockClaimHistoryEntryDto
    {
        public DateTime At { get; set; }
        public string Event { get; set; } = string.Empty;
        public string? Detail { get; set; }
    }

    public class DamagedStockClaimDetailDto
    {
        public int DamagedStockClaimID { get; set; }
        public string ClaimNumber { get; set; } = string.Empty;
        public DateTime ClaimDate { get; set; }
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string ClaimStatus { get; set; } = string.Empty;
        public string SettlementStatus { get; set; } = string.Empty;
        public decimal TotalClaimedQuantity { get; set; }
        public decimal TotalAcceptedQuantity { get; set; }
        public decimal TotalRejectedQuantity { get; set; }
        public decimal TotalClaimAmount { get; set; }
        public decimal TotalSettledAmount { get; set; }
        public decimal RemainingSettlementAmount { get; set; }
        public string? Remarks { get; set; }
        public string? SupplierResponseNotes { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public List<DamagedStockClaimItemDetailDto> Items { get; set; } = new();
        public List<DamagedStockClaimSettlementDetailDto> Settlements { get; set; } = new();
        public List<DamagedStockClaimHistoryEntryDto> History { get; set; } = new();
        public bool CanConfirm { get; set; }
        public bool CanDecideRejected { get; set; }
        public bool CanSettle { get; set; }
        public bool CanClose { get; set; }
    }
}
