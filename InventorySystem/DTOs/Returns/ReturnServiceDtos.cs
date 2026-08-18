using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InventorySystem.DTOs.Returns
{
    // ===== REQUEST DTOs =====
    public class ReturnItemInput
    {
        public int InvoiceItemID { get; set; }
        public int? ProductID { get; set; }
        public int? ProductUnitID { get; set; }

        /// <summary>
        /// Entered return quantity in the selected unit mode (whole numbers only when &gt; 0).
        /// Packaging mode: sold ProductUnit (e.g. Carton). Base mode: product BaseUnit (e.g. Piece).
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// "Packaging" (default) = Quantity is in sold unit; "Base" = Quantity is in base units.
        /// </summary>
        public string ReturnUnitMode { get; set; } = "Packaging";

        public string? Reason { get; set; }
        public string ReturnCondition { get; set; } = "Sellable"; // Sellable | Damaged
    }

    public class ProcessSalesReturnRequest
    {
        public int SalesInvoiceID { get; set; }
        public DateTime ReturnDate { get; set; } = DateTime.UtcNow;
        public string? Reason { get; set; }
        public bool IncludeSchemeCalculation { get; set; } = true;
        public string SettlementMethod { get; set; } = "ACCOUNT_ADJUSTMENT"; // CASH | ACCOUNT_ADJUSTMENT
        public List<ReturnItemInput> Items { get; set; } = new List<ReturnItemInput>();
    }

    public class ManualReturnItemInput
    {
        public int ProductID { get; set; }
        public int ProductUnitID { get; set; }
        public decimal Quantity { get; set; }
        public decimal ManualReturnUnitPrice { get; set; }
        public string? Reason { get; set; }
        public string ReturnCondition { get; set; } = "Sellable"; // Sellable | Damaged
    }

    public class ProcessManualSalesReturnRequest
    {
        public int CustomerID { get; set; }
        public int WarehouseID { get; set; }
        public DateTime ReturnDate { get; set; } = DateTime.UtcNow;
        public string? Reason { get; set; }
        public string SettlementMethod { get; set; } = "ACCOUNT_ADJUSTMENT"; // CASH | ACCOUNT_ADJUSTMENT
        public List<ManualReturnItemInput> Items { get; set; } = new List<ManualReturnItemInput>();
    }

    public class ProcessPurchaseReturnRequest
    {
        public int PurchaseInvoiceID { get; set; }
        public DateTime ReturnDate { get; set; } = DateTime.UtcNow;
        public string? Reason { get; set; }
        public List<ReturnItemInput> Items { get; set; } = new List<ReturnItemInput>();
    }

    public class PreviewReturnRequest
    {
        public int SalesInvoiceID { get; set; }
        public bool IncludeSchemeCalculation { get; set; } = true;
        public List<ReturnItemInput> Items { get; set; } = new List<ReturnItemInput>();
    }

    // ===== RESPONSE / CALCULATION DTOs =====
    public class FreePromotionClawbackDto
    {
        public int PromotionId { get; set; }
        public string PromotionName { get; set; } = string.Empty;
        public int QualifyingInvoiceItemId { get; set; }
        public string QualifyingProductName { get; set; } = string.Empty;
        public int FreeInvoiceItemId { get; set; }
        public int FreeProductId { get; set; }
        public string FreeProductName { get; set; } = string.Empty;

        // Distinct Quantity Concepts for Physical & Financial Accuracy
        public decimal OriginalFreeQuantity { get; set; }
        public decimal PreviouslyReturnedFreeQuantity { get; set; }
        public decimal PreviouslyChargedFreeQuantity { get; set; }
        public decimal PreviouslyReturnedChargedFreeQuantity { get; set; }
        public decimal UnreturnedChargedFreeQuantity { get; set; }
        public decimal PhysicallyRemainingFreeQuantity { get; set; }
        public decimal AtRiskFreeQuantity { get; set; }
        public decimal MaxReturnableFreeQuantity { get; set; }
        public decimal CurrentlySelectedFreeReturnQuantity { get; set; }
        public decimal RetainedFreeQuantityAfterCurrentReturn { get; set; }
        public decimal ReturnedPreviouslyChargedQuantity { get; set; }
        public decimal RemainingPreviouslyChargedQuantity { get; set; }
        public decimal NewlyUnearnedFreeQuantity { get; set; }

        // Financial Snapshots & Previously Charged Status
        public bool IsPreviouslyCharged { get; set; }
        public decimal NewPenaltyChargedQuantity { get; set; }
        public decimal RefundIfReturned { get; set; }
        public decimal UnitValue { get; set; }
        public decimal RetainedValue { get; set; }
    }

    public class ClawbackPreviewDto
    {
        public decimal GrossReturnedValue { get; set; }
        /// <summary>Discount released with the returned quantities (item share).</summary>
        public decimal ItemDiscountReleased { get; set; }
        /// <summary>Additional unearned discount removed when Automatic threshold is lost.</summary>
        public decimal DiscountClawback { get; set; }
        public decimal ClawbackPenalty => DiscountClawback;
        public decimal PromoPenalty { get; set; }
        public decimal RetainedFreeItemValue => PromoPenalty;
        /// <summary>Positive = customer credit; negative = additional amount due.</summary>
        public decimal NetRefundAmount { get; set; }
        public bool IsAdditionalAmountDue => NetRefundAmount < 0m;
        public decimal AdditionalAmountDue => NetRefundAmount < 0m ? Math.Abs(NetRefundAmount) : 0m;
        public decimal PreviousRemainingSubtotal { get; set; }
        public decimal PreviousRemainingDiscount { get; set; }
        public decimal PreviousRemainingNet { get; set; }
        public decimal NextRemainingSubtotal { get; set; }
        public decimal NextRemainingDiscount { get; set; }
        public decimal NextRemainingNet { get; set; }
        public List<string> Breakdown { get; set; } = new List<string>();
        public List<FreePromotionClawbackDto> FreePromotions { get; set; } = new List<FreePromotionClawbackDto>();
        /// <summary>Per invoice-item discount released in this return (for persistence).</summary>
        public Dictionary<int, decimal> ItemDiscountReleasedByInvoiceItemId { get; set; } = new Dictionary<int, decimal>();
    }

    public class ReturnEligibleItemDto
    {
        public int InvoiceItemID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public int ProductUnitID { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public string BaseUnitName { get; set; } = string.Empty;
        public decimal ConversionToBaseUnit { get; set; } = 1;
        public decimal OriginalQuantity { get; set; }
        public decimal OriginalConvertedQuantity { get; set; }
        public decimal AlreadyReturnedQuantity { get; set; }
        public decimal AlreadyReturnedConvertedQuantity { get; set; }
        public decimal RemainingReturnableQuantity { get; set; }
        public decimal RemainingReturnableConvertedQuantity { get; set; }
        public decimal UnitPriceOrCost { get; set; }
        public string ItemType { get; set; } = "NORMAL";
        public int? PromotionID { get; set; }
    }

    public class SalesReturnEligibilityDto
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public bool IsLocked { get; set; }
        public List<ReturnEligibleItemDto> Items { get; set; } = new List<ReturnEligibleItemDto>();
    }

    public class PurchaseReturnEligibilityDto
    {
        public int PurchaseInvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public bool IsLocked { get; set; }
        public List<ReturnEligibleItemDto> Items { get; set; } = new List<ReturnEligibleItemDto>();
    }

    // ===== DETAILS DTOs =====
    public class SalesReturnItemDto
    {
        public int SalesReturnItemID { get; set; }
        public int? InvoiceItemID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public int ProductUnitID { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ConvertedQuantity { get; set; }
        public decimal RefundUnitPrice { get; set; }
        public decimal RefundAmount { get; set; }
        public string? Reason { get; set; }
        public string ReturnCondition { get; set; } = "Sellable";
    }

    public class SalesReturnHeaderDto
    {
        public int SalesReturnID { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public string ReturnType { get; set; } = "INVOICE"; // INVOICE | MANUAL
        public int? InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int? WarehouseID { get; set; }
        public string? WarehouseName { get; set; }
        public string SettlementMethod { get; set; } = "ACCOUNT_ADJUSTMENT"; // CASH | ACCOUNT_ADJUSTMENT
        public DateTime ReturnDate { get; set; }
        public string? Reason { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal ClawbackPenalty { get; set; }
        public decimal PromoPenalty { get; set; }
        public decimal NetRefundAmount { get; set; }
        public bool IncludeSchemeCalculation { get; set; }
        public string CreatedByUserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class SalesReturnDetailsDto
    {
        public SalesReturnHeaderDto Header { get; set; } = new SalesReturnHeaderDto();
        public List<SalesReturnItemDto> Items { get; set; } = new List<SalesReturnItemDto>();
    }

    public class PurchaseReturnItemDto
    {
        public int PurchaseReturnItemID { get; set; }
        public int PurchaseInvoiceItemID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public int ProductUnitID { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ConvertedQuantity { get; set; }
        public decimal RefundUnitCost { get; set; }
        public decimal RefundAmount { get; set; }
        public string? Reason { get; set; }
        public string ReturnCondition { get; set; } = "Sellable";
    }

    public class PurchaseReturnHeaderDto
    {
        public int PurchaseReturnID { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public int PurchaseInvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime ReturnDate { get; set; }
        public string? Reason { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal NetRefundAmount { get; set; }
        public string CreatedByUserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class PurchaseReturnDetailsDto
    {
        public PurchaseReturnHeaderDto Header { get; set; } = new PurchaseReturnHeaderDto();
        public List<PurchaseReturnItemDto> Items { get; set; } = new List<PurchaseReturnItemDto>();
    }
}
