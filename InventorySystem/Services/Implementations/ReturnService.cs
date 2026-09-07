using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Returns;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class ReturnService : IReturnService
    {
        private readonly IReturnRepository _returnRepository;
        private readonly ApplicationDbContext _context;
        private readonly IFifoCostingService _fifoCostingService;
        private readonly ILogger<ReturnService> _logger;

        public ReturnService(
            IReturnRepository returnRepository,
            ApplicationDbContext context,
            IFifoCostingService fifoCostingService,
            ILogger<ReturnService> logger)
        {
            _returnRepository = returnRepository ?? throw new ArgumentNullException(nameof(returnRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _fifoCostingService = fifoCostingService ?? throw new ArgumentNullException(nameof(fifoCostingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OperationResult<SalesReturnEligibilityDto>> GetSalesReturnEligibilityAsync(int salesInvoiceId, CancellationToken cancellationToken = default)
        {
            var invoice = await _returnRepository.GetSalesInvoiceForReturnAsync(salesInvoiceId, cancellationToken);
            if (invoice == null)
            {
                return OperationResult<SalesReturnEligibilityDto>.Fail($"Sales Invoice #{salesInvoiceId} not found.");
            }

            var priorReturnItems = await _returnRepository.GetPriorSalesReturnItemsAsync(salesInvoiceId, cancellationToken);
            var priorGrouped = (priorReturnItems ?? new List<SalesReturnItem>())
                .Where(i => i.InvoiceItemID.HasValue)
                .GroupBy(i => i.InvoiceItemID!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.ConvertedQuantity));

            var dto = new SalesReturnEligibilityDto
            {
                InvoiceID = invoice.InvoiceID,
                InvoiceNumber = invoice.InvoiceNumber,
                CustomerID = invoice.CustomerID,
                CustomerName = invoice.Customer?.ShopName ?? string.Empty,
                WarehouseID = invoice.WarehouseID,
                WarehouseName = invoice.Warehouse?.Name ?? string.Empty,
                InvoiceDate = invoice.InvoiceDate,
                IsLocked = invoice.IsLocked,
                Items = invoice.Items.Select(item =>
                {
                    decimal priorConverted = priorGrouped.TryGetValue(item.InvoiceItemID, out var pQty) ? pQty : 0m;
                    decimal conversionFactor = item.ProductUnit?.ConversionToBaseUnit > 0 ? item.ProductUnit.ConversionToBaseUnit : 1m;
                    decimal priorInPackagingUnit = UnitConversionHelper.ToDisplayUnits(priorConverted, conversionFactor);
                    decimal remainingConverted = Math.Max(0m, item.ConvertedQuantity - priorConverted);
                    decimal remainingInPackagingUnit = UnitConversionHelper.ToDisplayUnits(remainingConverted, conversionFactor);

                    return new ReturnEligibleItemDto
                    {
                        InvoiceItemID = item.InvoiceItemID,
                        ProductID = item.ProductID ?? 0,
                        ProductName = !string.IsNullOrWhiteSpace(item.CustomItemName)
                            ? $"Other: {item.CustomItemName}"
                            : (item.Product?.ProductName ?? string.Empty),
                        SKU = item.Product?.SKU,
                        ProductUnitID = item.ProductUnitID ?? 0,
                        UnitName = item.ProductUnit?.Unit?.UnitName ?? (!string.IsNullOrWhiteSpace(item.CustomItemName) ? "Item" : string.Empty),
                        BaseUnitName = item.Product?.BaseUnit?.UnitName ?? (!string.IsNullOrWhiteSpace(item.CustomItemName) ? "Item" : string.Empty),
                        ConversionToBaseUnit = conversionFactor,
                        OriginalQuantity = item.Quantity,
                        OriginalConvertedQuantity = item.ConvertedQuantity,
                        AlreadyReturnedQuantity = priorInPackagingUnit,
                        AlreadyReturnedConvertedQuantity = priorConverted,
                        RemainingReturnableQuantity = remainingInPackagingUnit,
                        RemainingReturnableConvertedQuantity = remainingConverted,
                        UnitPriceOrCost = item.UnitPrice,
                        ItemType = item.ItemType,
                        PromotionID = item.PromotionID
                    };
                }).ToList()
            };

            return OperationResult<SalesReturnEligibilityDto>.Ok(dto);
        }

        public async Task<OperationResult<ClawbackPreviewDto>> PreviewSalesClawbackAsync(PreviewReturnRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null || request.SalesInvoiceID <= 0)
            {
                return OperationResult<ClawbackPreviewDto>.Fail("Invalid return preview request.");
            }

            var invoice = await _returnRepository.GetSalesInvoiceForReturnAsync(request.SalesInvoiceID, cancellationToken);
            if (invoice == null)
            {
                return OperationResult<ClawbackPreviewDto>.Fail($"Sales Invoice #{request.SalesInvoiceID} not found.");
            }

            var priorReturnItems = await _returnRepository.GetPriorSalesReturnItemsAsync(request.SalesInvoiceID, cancellationToken);

            return await CalculateSalesReturnFinancialsAsync(
                invoice,
                priorReturnItems ?? new List<SalesReturnItem>(),
                request.Items ?? new List<ReturnItemInput>(),
                request.IncludeSchemeCalculation,
                cancellationToken);
        }

        private async Task<OperationResult<ClawbackPreviewDto>> CalculateSalesReturnFinancialsAsync(
            SalesInvoice invoice,
            List<SalesReturnItem> priorReturnItems,
            List<ReturnItemInput> itemsInput,
            bool includeSchemeCalculation,
            CancellationToken cancellationToken)
        {
            var priorGrouped = (priorReturnItems ?? new List<SalesReturnItem>())
                .Where(i => i.InvoiceItemID.HasValue)
                .GroupBy(i => i.InvoiceItemID!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.ConvertedQuantity));

            var priorRefundGrouped = (priorReturnItems ?? new List<SalesReturnItem>())
                .Where(i => i.InvoiceItemID.HasValue)
                .GroupBy(i => i.InvoiceItemID!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.RefundAmount));

            var priorDisplayGrouped = (priorReturnItems ?? new List<SalesReturnItem>())
                .Where(i => i.InvoiceItemID.HasValue)
                .GroupBy(i => i.InvoiceItemID!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var priorChargedGrouped = (priorReturnItems ?? new List<SalesReturnItem>())
                .Where(i => i.InvoiceItemID.HasValue)
                .GroupBy(i => i.InvoiceItemID!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.PromoPenaltyQuantity));

            decimal grossReturnedValue = 0m;
            decimal normalGrossReturnedValue = 0m;
            decimal clawbackPenalty = 0m;
            decimal promoPenalty = 0m;
            var breakdown = new List<string>();
            var freePromotions = new List<FreePromotionClawbackDto>();

            var invoiceItems = invoice.Items ?? new List<SalesInvoiceItem>();

            // 1. Validate paid item quantities & calculate Gross Returned Value
            foreach (var itemInput in itemsInput.Where(i => i.Quantity > 0))
            {
                var invoiceItem = invoiceItems.FirstOrDefault(i => i.InvoiceItemID == itemInput.InvoiceItemID);
                if (invoiceItem == null)
                {
                    return OperationResult<ClawbackPreviewDto>.Fail($"Invoice item #{itemInput.InvoiceItemID} is invalid for this sales invoice.");
                }

                // Sole authority for ItemType is persisted SalesInvoiceItem.ItemType from Database
                bool isFree = string.Equals(invoiceItem.ItemType, "FREE", StringComparison.OrdinalIgnoreCase);

                decimal conversionFactor = invoiceItem.ProductUnit?.ConversionToBaseUnit > 0 ? invoiceItem.ProductUnit.ConversionToBaseUnit : 1m;
                if (!ReturnQuantityHelper.TryNormalize(itemInput.Quantity, itemInput.ReturnUnitMode, conversionFactor, out _, out decimal requestedConverted, out string? normalizeError))
                {
                    return OperationResult<ClawbackPreviewDto>.Fail($"Product '{invoiceItem.Product?.ProductName ?? "Unknown"}': {normalizeError}");
                }

                decimal priorConverted = priorGrouped.TryGetValue(invoiceItem.InvoiceItemID, out var pQty) ? pQty : 0m;
                decimal remainingConverted = Math.Max(0m, invoiceItem.ConvertedQuantity - priorConverted);

                if (!isFree)
                {
                    if (requestedConverted > remainingConverted + 0.0001m)
                    {
                        return OperationResult<ClawbackPreviewDto>.Fail($"Return quantity for product '{invoiceItem.Product?.ProductName ?? "Unknown"}' exceeds remaining returnable quantity.");
                    }

                    decimal originalBase = invoiceItem.ConvertedQuantity > 0m
                        ? invoiceItem.ConvertedQuantity
                        : UnitConversionHelper.ToBaseUnits(invoiceItem.Quantity, conversionFactor);
                    decimal lineTotal = ReturnValuationHelper.ComputeOriginalLineTotal(invoiceItem.Quantity, invoiceItem.UnitPrice);
                    decimal priorRefund = priorRefundGrouped.TryGetValue(invoiceItem.InvoiceItemID, out var pr) ? pr : 0m;
                    // Base-proportional share of packaging line total (avoids 1/13 → 0.077 × price = 18.02).
                    decimal lineGross = ReturnValuationHelper.ComputeBaseProportionalAmount(
                        requestedConverted,
                        originalBase,
                        lineTotal,
                        priorConverted,
                        priorRefund);
                    grossReturnedValue += lineGross;
                    normalGrossReturnedValue += lineGross;
                }
            }

            // Remaining-state discount (historical invoice snapshots only — never live DiscountRules / product prices).
            var previousState = ComputeRemainingDiscountState(invoice, priorReturnItems ?? new List<SalesReturnItem>(), null);
            var nextState = ComputeRemainingDiscountState(invoice, priorReturnItems ?? new List<SalesReturnItem>(), itemsInput);

            if (includeSchemeCalculation)
            {
                // Discount reduction = previous remaining discount − next remaining discount
                // Item share released with returned qty; clawback = unearned remainder when Automatic threshold is lost.
                clawbackPenalty = Math.Max(0m,
                    Math.Round(previousState.RemainingDiscount - nextState.RemainingDiscount - nextState.ItemDiscountReleasedThisReturn, 2, MidpointRounding.AwayFromZero));

                if (nextState.ItemDiscountReleasedThisReturn > 0)
                {
                    breakdown.Add($"Discount released with returned items: {nextState.ItemDiscountReleasedThisReturn:C}");
                }
                if (clawbackPenalty > 0)
                {
                    breakdown.Add($"Automatic threshold no longer met after return (remaining subtotal {nextState.RemainingSubtotal:C}). Unearned remaining discount clawback: {clawbackPenalty:C}");
                }

                // 3. Promotion Free Item Calculation (Unit-Based & Cumulative History)
                var promoCampaignIds = new HashSet<int>();
                if (invoice.InvoicePromotions != null)
                {
                    foreach (var ip in invoice.InvoicePromotions)
                    {
                        if (ip.PromotionID > 0) promoCampaignIds.Add(ip.PromotionID);
                    }
                }
                foreach (var item in invoiceItems)
                {
                    if (item.PromotionID.HasValue && item.PromotionID.Value > 0)
                    {
                        promoCampaignIds.Add(item.PromotionID.Value);
                    }
                }

                if (promoCampaignIds.Any())
                {
                    foreach (var promoId in promoCampaignIds)
                    {
                        var campaign = await _context.PromotionCampaigns
                            .Include(pc => pc.PromotionRules)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(pc => pc.PromotionID == promoId, cancellationToken);

                        if (campaign == null || campaign.PromotionRules == null || !campaign.PromotionRules.Any()) continue;

                        foreach (var rule in campaign.PromotionRules)
                        {
                            // Custom "Other" free gifts have no retail value — skip clawback for this rule
                            if (rule.IsCustomFreeItem || !rule.FreeProductID.HasValue)
                            {
                                continue;
                            }

                            var buyItem = invoiceItems.FirstOrDefault(i => i.ProductID == rule.BuyProductID && !string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase));
                            var freeItem = invoiceItems.FirstOrDefault(i => i.ProductID == rule.FreeProductID && string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase));

                            if (freeItem == null) continue;

                            decimal buyConversion = buyItem?.ProductUnit?.ConversionToBaseUnit > 0 ? buyItem.ProductUnit.ConversionToBaseUnit : 1m;
                            var buyInput = buyItem != null ? itemsInput.FirstOrDefault(i => i.InvoiceItemID == buyItem.InvoiceItemID) : null;
                            decimal buyCurrentReturnedBaseQty = 0m;
                            if (buyInput != null && buyInput.Quantity > 0)
                            {
                                if (!ReturnQuantityHelper.TryNormalize(buyInput.Quantity, buyInput.ReturnUnitMode, buyConversion, out _, out buyCurrentReturnedBaseQty, out string? buyNormError))
                                {
                                    return OperationResult<ClawbackPreviewDto>.Fail($"Product '{buyItem?.Product?.ProductName ?? "Unknown"}': {buyNormError}");
                                }
                            }

                            decimal buyOriginalConverted = buyItem?.ConvertedQuantity ?? 0m;
                            decimal buyPriorReturnedConverted = buyItem != null && priorGrouped.TryGetValue(buyItem.InvoiceItemID, out var bpQty) ? bpQty : 0m;

                            decimal buyRemainingBeforeReturn = Math.Max(0m, buyOriginalConverted - buyPriorReturnedConverted);
                            decimal buyRemainingAfterReturn = Math.Max(0m, buyRemainingBeforeReturn - buyCurrentReturnedBaseQty);

                            decimal thresholdBaseQty = rule.BuyQuantity > 0 ? rule.BuyQuantity : 1m;
                            decimal freePerThreshold = rule.FreeQuantity > 0 ? rule.FreeQuantity : 1m;

                            // Incremental entitlement calculation
                            decimal freeEarnedBefore = Math.Floor(buyRemainingBeforeReturn / thresholdBaseQty) * freePerThreshold;
                            decimal freeEarnedAfter = Math.Floor(buyRemainingAfterReturn / thresholdBaseQty) * freePerThreshold;
                            decimal freeBaseQtyAtRisk = Math.Max(0m, freeEarnedBefore - freeEarnedAfter);

                            decimal freeConversion = freeItem.ProductUnit?.ConversionToBaseUnit > 0 ? freeItem.ProductUnit.ConversionToBaseUnit : 1m;
                            decimal freeAtRiskDisplayQty = freeBaseQtyAtRisk / freeConversion;

                            decimal freePriorReturnedDisplayQty = priorDisplayGrouped.TryGetValue(freeItem.InvoiceItemID, out var fPriorQty) ? fPriorQty : 0m;

                            var priorItemsList = priorReturnItems ?? new List<SalesReturnItem>();
                            decimal previousPromoPenaltyQuantity = priorItemsList
                                .Where(i => i.InvoiceItemID == freeItem.InvoiceItemID)
                                .Sum(i => i.PromoPenaltyQuantity);

                            decimal freeItemPrice = freeItem.UnitPrice > 0 ? freeItem.UnitPrice : (freeItem.Product != null && freeItem.Product.BaseSellingPrice > 0 ? freeItem.Product.BaseSellingPrice : 0m);

                            decimal priorReturnedChargedUnits = priorItemsList
                                .Where(i => i.InvoiceItemID == freeItem.InvoiceItemID && i.RefundAmount > 0)
                                .Sum(i =>
                                {
                                    decimal price = i.RefundUnitPrice > 0 ? i.RefundUnitPrice : (freeItemPrice > 0 ? freeItemPrice : 1m);
                                    return Math.Min(i.Quantity, Math.Round(i.RefundAmount / price, 3));
                                });

                            // Step 1: PreviouslyChargedFreeQty = Math.Max(0m, PreviousPromoPenaltyQuantity - PriorReturnedChargedUnits)
                            decimal previouslyChargedFreeQty = Math.Max(0m, previousPromoPenaltyQuantity - priorReturnedChargedUnits);
                            decimal physicallyRemainingFreeQty = Math.Max(0m, freeItem.Quantity - freePriorReturnedDisplayQty);

                            // Max returnable free quantity is proportional to returned paid items plus previously charged unreturned free units
                            decimal maxReturnableFreeQty = Math.Min(physicallyRemainingFreeQty, freeAtRiskDisplayQty + previouslyChargedFreeQty);

                            decimal selectedFreeReturnQty = 0m;
                            var freeInput = itemsInput.FirstOrDefault(i => i.InvoiceItemID == freeItem.InvoiceItemID);
                            if (freeInput != null && freeInput.Quantity > 0)
                            {
                                if (!ReturnQuantityHelper.TryNormalize(freeInput.Quantity, freeInput.ReturnUnitMode, freeConversion, out selectedFreeReturnQty, out _, out string? freeNormError))
                                {
                                    return OperationResult<ClawbackPreviewDto>.Fail($"Free promotional item '{freeItem.Product?.ProductName ?? "Free Item"}': {freeNormError}");
                                }
                            }

                            // Strict server validation (No silent clamping)
                            if (selectedFreeReturnQty > maxReturnableFreeQty + 0.0001m)
                            {
                                return OperationResult<ClawbackPreviewDto>.Fail($"Selected return quantity ({selectedFreeReturnQty}) for free promotional item '{freeItem.Product?.ProductName ?? "Free Item"}' exceeds maximum returnable quantity ({maxReturnableFreeQty}).");
                            }

                            // Step 2: ReturnedPreviouslyChargedQty = Math.Min(PreviouslyChargedFreeQty, ReturnedNowQty)
                            decimal returnedPreviouslyChargedQty = Math.Min(previouslyChargedFreeQty, selectedFreeReturnQty);

                            // Step 3: RemainingPreviouslyChargedQty = Math.Max(0m, PreviouslyChargedFreeQty - ReturnedPreviouslyChargedQty)
                            decimal remainingPreviouslyChargedQty = Math.Max(0m, previouslyChargedFreeQty - returnedPreviouslyChargedQty);

                            // Step 4: FreeRefund = Math.Round(ReturnedPreviouslyChargedQty * FreeItemUnitPrice, 2)
                            decimal freeRefund = Math.Round(returnedPreviouslyChargedQty * freeItemPrice, 2);
                            grossReturnedValue += freeRefund;

                            // Step 5: RetainedTotalFreeQty = Math.Max(0m, PhysicallyRemainingFreeQty - ReturnedNowQty)
                            decimal retainedTotalFreeQty = Math.Max(0m, physicallyRemainingFreeQty - selectedFreeReturnQty);

                            // Step 6: RemainingUnchargedQty = Math.Max(0m, RetainedTotalFreeQty - RemainingPreviouslyChargedQty)
                            decimal remainingUnchargedQty = Math.Max(0m, retainedTotalFreeQty - remainingPreviouslyChargedQty);

                            // Step 7: UnearnedUnchargedQty = Math.Max(0m, RemainingUnchargedQty - FreeEarnedAfter)
                            decimal unearnedUnchargedQty = Math.Max(0m, remainingUnchargedQty - freeEarnedAfter);

                            // Step 8: NewlyChargedFreeQty = UnearnedUnchargedQty (ONLY units newly penalized by THIS transaction)
                            decimal newlyChargedFreeQty = unearnedUnchargedQty;

                            // Step 9: PromoPenaltyAmount = Math.Round(NewlyChargedFreeQty * FreeItemUnitPrice, 2)
                            decimal promoPenaltyAmount = Math.Round(newlyChargedFreeQty * freeItemPrice, 2);

                            _logger.LogInformation(
                                "Promotional Free Item Calculation breakdown: " +
                                "InvoiceItemID={InvoiceItemID}, " +
                                "OriginalFreeQty={OriginalFreeQty}, " +
                                "PreviouslyReturnedFreeQty={PreviouslyReturnedFreeQty}, " +
                                "PreviouslyChargedFreeQty={PreviouslyChargedFreeQty}, " +
                                "PhysicallyRemainingFreeQty={PhysicallyRemainingFreeQty}, " +
                                "ReturnedNowQty={ReturnedNowQty}, " +
                                "ReturnedPreviouslyChargedQty={ReturnedPreviouslyChargedQty}, " +
                                "RemainingPreviouslyChargedQty={RemainingPreviouslyChargedQty}, " +
                                "NewlyUnearnedFreeQty={NewlyUnearnedFreeQty}, " +
                                "NewPenaltyQty={NewPenaltyQty}, " +
                                "FreeItemUnitPrice={FreeItemUnitPrice}, " +
                                "FreeRefund={FreeRefund}, " +
                                "PromoPenalty={PromoPenalty}",
                                freeItem.InvoiceItemID,
                                freeItem.Quantity,
                                freePriorReturnedDisplayQty,
                                previouslyChargedFreeQty,
                                physicallyRemainingFreeQty,
                                selectedFreeReturnQty,
                                returnedPreviouslyChargedQty,
                                remainingPreviouslyChargedQty,
                                unearnedUnchargedQty,
                                newlyChargedFreeQty,
                                freeItemPrice,
                                freeRefund,
                                promoPenaltyAmount);

                            if (promoPenaltyAmount > 0)
                            {
                                promoPenalty += promoPenaltyAmount;
                                string freeProdName = freeItem.Product?.ProductName ?? "Free Item";
                                breakdown.Add($"Promotion '{campaign.Name}': {newlyChargedFreeQty:N2} free unit(s) ('{freeProdName}') newly subject to penalty. Promotional penalty: {promoPenaltyAmount:C}");
                            }

                            freePromotions.Add(new FreePromotionClawbackDto
                            {
                                PromotionId = campaign.PromotionID,
                                PromotionName = campaign.Name,
                                QualifyingInvoiceItemId = buyItem?.InvoiceItemID ?? 0,
                                QualifyingProductName = buyItem?.Product?.ProductName ?? "Qualifying Product",
                                FreeInvoiceItemId = freeItem.InvoiceItemID,
                                FreeProductId = freeItem.ProductID ?? 0,
                                FreeProductName = freeItem.Product?.ProductName ?? "Free Item",
                                OriginalFreeQuantity = freeItem.Quantity,
                                PreviouslyReturnedFreeQuantity = freePriorReturnedDisplayQty,
                                PreviouslyChargedFreeQuantity = previousPromoPenaltyQuantity,
                                PreviouslyReturnedChargedFreeQuantity = priorReturnedChargedUnits,
                                UnreturnedChargedFreeQuantity = previouslyChargedFreeQty,
                                PhysicallyRemainingFreeQuantity = physicallyRemainingFreeQty,
                                AtRiskFreeQuantity = freeAtRiskDisplayQty,
                                MaxReturnableFreeQuantity = maxReturnableFreeQty,
                                CurrentlySelectedFreeReturnQuantity = selectedFreeReturnQty,
                                RetainedFreeQuantityAfterCurrentReturn = retainedTotalFreeQty,
                                ReturnedPreviouslyChargedQuantity = returnedPreviouslyChargedQty,
                                RemainingPreviouslyChargedQuantity = remainingPreviouslyChargedQty,
                                NewlyUnearnedFreeQuantity = unearnedUnchargedQty,
                                IsPreviouslyCharged = previouslyChargedFreeQty > 0,
                                NewPenaltyChargedQuantity = newlyChargedFreeQty,
                                RefundIfReturned = Math.Round(Math.Min(1m, previouslyChargedFreeQty) * freeItemPrice, 2),
                                UnitValue = freeItemPrice,
                                RetainedValue = promoPenaltyAmount
                            });
                        }
                    }
                }
            }

            // Net financial effect for NORMAL remaining-state + FREE refunds − promo penalty.
            // Positive → customer credit; negative → additional amount due (do NOT floor at 0).
            decimal itemDiscountReleased = includeSchemeCalculation ? nextState.ItemDiscountReleasedThisReturn : 0m;
            if (!includeSchemeCalculation)
            {
                clawbackPenalty = 0m;
            }

            decimal freeRefundPortion = Math.Max(0m, grossReturnedValue - normalGrossReturnedValue);
            decimal netRefund;
            if (!includeSchemeCalculation)
            {
                // Override: refund full historical UnitPrice gross only (no discount/promo adjustments).
                netRefund = grossReturnedValue;
                itemDiscountReleased = 0m;
            }
            else
            {
                netRefund = Math.Round(
                    previousState.RemainingNet - nextState.RemainingNet + freeRefundPortion - promoPenalty,
                    2,
                    MidpointRounding.AwayFromZero);
            }

            var preview = new ClawbackPreviewDto
            {
                GrossReturnedValue = grossReturnedValue,
                ItemDiscountReleased = itemDiscountReleased,
                DiscountClawback = clawbackPenalty,
                PromoPenalty = promoPenalty,
                NetRefundAmount = netRefund,
                PreviousRemainingSubtotal = previousState.RemainingSubtotal,
                PreviousRemainingDiscount = previousState.RemainingDiscount,
                PreviousRemainingNet = previousState.RemainingNet,
                NextRemainingSubtotal = nextState.RemainingSubtotal,
                NextRemainingDiscount = nextState.RemainingDiscount,
                NextRemainingNet = nextState.RemainingNet,
                Breakdown = breakdown,
                FreePromotions = freePromotions,
                ItemDiscountReleasedByInvoiceItemId = nextState.ItemDiscountReleasedByInvoiceItemId
            };

            return OperationResult<ClawbackPreviewDto>.Ok(preview);
        }

        private sealed class RemainingDiscountState
        {
            public decimal RemainingSubtotal { get; init; }
            public decimal RemainingDiscount { get; init; }
            public decimal RemainingNet => Math.Max(0m, RemainingSubtotal - RemainingDiscount);
            public decimal ItemDiscountReleasedThisReturn { get; init; }
            public Dictionary<int, decimal> ItemDiscountReleasedByInvoiceItemId { get; init; } = new Dictionary<int, decimal>();
        }

        /// <summary>
        /// Computes remaining NORMAL-item subtotal/discount/net from historical invoice snapshots
        /// and cumulative returns. Uses SalesInvoiceItem.UnitPrice only — never live product prices.
        /// Uses InvoiceDiscounts snapshot for Automatic threshold — never live DiscountRules.
        /// Money uses base-unit proportions of the packaging line total (not rounded packaging fractions).
        /// </summary>
        private static RemainingDiscountState ComputeRemainingDiscountState(
            SalesInvoice invoice,
            List<SalesReturnItem> priorReturnItems,
            List<ReturnItemInput>? additionalReturnItems)
        {
            var invoiceItems = (invoice.Items ?? new List<SalesInvoiceItem>())
                .Where(i => !i.IsDeleted)
                .ToList();

            var priorByItem = priorReturnItems
                .Where(i => i.InvoiceItemID.HasValue)
                .GroupBy(i => i.InvoiceItemID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => (
                        BaseQty: g.Sum(x => x.ConvertedQuantity),
                        RefundAmount: g.Sum(x => x.RefundAmount),
                        DiscountReleased: g.Sum(x => x.DiscountAmount)
                    ));

            var additionalBaseByItem = new Dictionary<int, decimal>();
            if (additionalReturnItems != null)
            {
                foreach (var input in additionalReturnItems.Where(i => i.Quantity > 0))
                {
                    var invoiceItem = invoiceItems.FirstOrDefault(i => i.InvoiceItemID == input.InvoiceItemID);
                    if (invoiceItem == null || string.Equals(invoiceItem.ItemType, "FREE", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    decimal conversionFactor = invoiceItem.ProductUnit?.ConversionToBaseUnit > 0 ? invoiceItem.ProductUnit.ConversionToBaseUnit : 1m;
                    if (!ReturnQuantityHelper.TryNormalize(input.Quantity, input.ReturnUnitMode, conversionFactor, out _, out decimal baseQty, out _))
                    {
                        continue;
                    }

                    if (!additionalBaseByItem.ContainsKey(invoiceItem.InvoiceItemID))
                    {
                        additionalBaseByItem[invoiceItem.InvoiceItemID] = 0m;
                    }
                    additionalBaseByItem[invoiceItem.InvoiceItemID] += baseQty;
                }
            }

            decimal remainingSubtotal = 0m;
            decimal remainingLineDiscountSum = 0m;
            decimal itemDiscountReleasedThisReturn = 0m;
            var releasedByItem = new Dictionary<int, decimal>();

            var snapshot = invoice.InvoiceDiscounts?.OrderBy(d => d.InvoiceDiscountID).FirstOrDefault();
            bool isPercentage = snapshot != null
                && string.Equals(snapshot.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase);
            decimal snapshotRate = snapshot?.DiscountValue ?? 0m;
            string discountMode = invoice.DiscountMode ?? "None";
            if (string.IsNullOrWhiteSpace(discountMode) || string.Equals(discountMode, "None", StringComparison.OrdinalIgnoreCase))
            {
                if (snapshot != null && snapshot.DiscountAmount > 0)
                {
                    if (string.Equals(snapshot.DiscountSource, "Manual", StringComparison.OrdinalIgnoreCase))
                    {
                        discountMode = "Manual";
                    }
                    else if (string.Equals(snapshot.DiscountSource, "Customer", StringComparison.OrdinalIgnoreCase))
                    {
                        discountMode = "Customer";
                    }
                    else
                    {
                        discountMode = "Automatic";
                    }
                }
            }

            foreach (var item in invoiceItems)
            {
                if (string.Equals(item.ItemType, "FREE", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Historical selling price snapshot — NEVER ProductUnit.SellingPrice / BaseSellingPrice
                decimal unitPrice = item.UnitPrice;
                decimal originalQty = item.Quantity;
                decimal conversionFactor = item.ProductUnit?.ConversionToBaseUnit > 0 ? item.ProductUnit.ConversionToBaseUnit : 1m;
                decimal originalBase = item.ConvertedQuantity > 0m
                    ? item.ConvertedQuantity
                    : UnitConversionHelper.ToBaseUnits(originalQty, conversionFactor);
                decimal lineTotal = ReturnValuationHelper.ComputeOriginalLineTotal(originalQty, unitPrice);

                decimal priorBase = priorByItem.TryGetValue(item.InvoiceItemID, out var prior) ? prior.BaseQty : 0m;
                decimal priorRefund = priorByItem.TryGetValue(item.InvoiceItemID, out var priorR) ? priorR.RefundAmount : 0m;
                decimal priorDiscReleased = priorByItem.TryGetValue(item.InvoiceItemID, out var priorD) ? priorD.DiscountReleased : 0m;
                decimal addBase = additionalBaseByItem.TryGetValue(item.InvoiceItemID, out var aq) ? aq : 0m;

                decimal thisGross = ReturnValuationHelper.ComputeBaseProportionalAmount(
                    addBase,
                    originalBase,
                    lineTotal,
                    priorBase,
                    priorRefund);
                decimal remainingLineValue = Math.Max(0m, lineTotal - Math.Min(priorRefund, lineTotal) - thisGross);
                remainingSubtotal += remainingLineValue;

                decimal originalLineDiscount = item.DiscountAmount;
                // Legacy fallback: if item discount was never allocated but snapshot is %, use rate × line gross
                if (originalLineDiscount <= 0m && isPercentage && snapshotRate > 0m)
                {
                    originalLineDiscount = Math.Round(lineTotal * (snapshotRate / 100m), 2, MidpointRounding.AwayFromZero);
                }

                decimal remainingLineDiscountBeforeAdd = Math.Max(0m, originalLineDiscount - priorDiscReleased);
                decimal releasedNow = 0m;
                if (addBase > 0m && originalBase > 0m && originalLineDiscount > 0m)
                {
                    releasedNow = ReturnValuationHelper.ComputeBaseProportionalAmount(
                        addBase,
                        originalBase,
                        originalLineDiscount,
                        priorBase,
                        priorDiscReleased);
                    releasedNow = Math.Min(releasedNow, remainingLineDiscountBeforeAdd);
                }

                releasedByItem[item.InvoiceItemID] = releasedNow;
                itemDiscountReleasedThisReturn += releasedNow;
                remainingLineDiscountSum += Math.Max(0m, remainingLineDiscountBeforeAdd - releasedNow);
            }

            itemDiscountReleasedThisReturn = Math.Round(itemDiscountReleasedThisReturn, 2, MidpointRounding.AwayFromZero);
            remainingSubtotal = Math.Round(remainingSubtotal, 2, MidpointRounding.AwayFromZero);
            remainingLineDiscountSum = Math.Round(remainingLineDiscountSum, 2, MidpointRounding.AwayFromZero);

            decimal remainingDiscount = 0m;
            if (string.Equals(discountMode, "Automatic", StringComparison.OrdinalIgnoreCase) && snapshot != null)
            {
                decimal? minAmount = snapshot.MinimumOrderAmount;
                decimal? maxAmount = snapshot.MaximumOrderAmount;
                bool eligible = remainingSubtotal > 0m;
                if (minAmount.HasValue)
                {
                    eligible = eligible && remainingSubtotal >= minAmount.Value;
                }
                if (maxAmount.HasValue)
                {
                    eligible = eligible && remainingSubtotal <= maxAmount.Value;
                }

                if (eligible)
                {
                    if (isPercentage && snapshotRate > 0m)
                    {
                        remainingDiscount = Math.Round(remainingSubtotal * (snapshotRate / 100m), 2, MidpointRounding.AwayFromZero);
                    }
                    else
                    {
                        // Fixed automatic: keep remaining allocated line shares
                        remainingDiscount = remainingLineDiscountSum;
                    }
                }
                else
                {
                    remainingDiscount = 0m;
                }
            }
            else if (string.Equals(discountMode, "Manual", StringComparison.OrdinalIgnoreCase)
                || string.Equals(discountMode, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                // No threshold — remaining discount is what remains allocated to remaining quantities
                remainingDiscount = remainingLineDiscountSum;
            }
            else
            {
                remainingDiscount = remainingLineDiscountSum;
            }

            remainingDiscount = Math.Min(remainingDiscount, remainingSubtotal);

            return new RemainingDiscountState
            {
                RemainingSubtotal = remainingSubtotal,
                RemainingDiscount = remainingDiscount,
                ItemDiscountReleasedThisReturn = itemDiscountReleasedThisReturn,
                ItemDiscountReleasedByInvoiceItemId = releasedByItem
            };
        }

        public async Task<OperationResult<int>> ProcessSalesReturnAsync(ProcessSalesReturnRequest request, int userId, CancellationToken cancellationToken = default)
        {
            if (request == null || request.SalesInvoiceID <= 0)
            {
                return OperationResult<int>.Fail("Invalid sales return request.");
            }

            if (request.Items == null || !request.Items.Any(i => i.Quantity > 0))
            {
                return OperationResult<int>.Fail("At least one item must have a return quantity greater than zero.");
            }

            var invoice = await _returnRepository.GetSalesInvoiceForReturnAsync(request.SalesInvoiceID, cancellationToken);
            if (invoice == null)
            {
                return OperationResult<int>.Fail($"Sales Invoice #{request.SalesInvoiceID} not found.");
            }

            var priorReturnItems = await _returnRepository.GetPriorSalesReturnItemsAsync(request.SalesInvoiceID, cancellationToken);

            // Execute Authoritative Financial Calculation & Server-Side Validation
            var calcResult = await CalculateSalesReturnFinancialsAsync(
                invoice,
                priorReturnItems ?? new List<SalesReturnItem>(),
                request.Items,
                request.IncludeSchemeCalculation,
                cancellationToken);

            if (!calcResult.Success || calcResult.Data == null)
            {
                return OperationResult<int>.Fail(calcResult.Message);
            }

            var preview = calcResult.Data;
            var now = DateTime.UtcNow;
            string returnNumber = await _returnRepository.GenerateSalesReturnNumberAsync(cancellationToken);

            var priorGrouped = (priorReturnItems ?? new List<SalesReturnItem>())
                .Where(i => i.InvoiceItemID.HasValue)
                .GroupBy(i => i.InvoiceItemID!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.ConvertedQuantity));

            var priorRefundGrouped = (priorReturnItems ?? new List<SalesReturnItem>())
                .Where(i => i.InvoiceItemID.HasValue)
                .GroupBy(i => i.InvoiceItemID!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.RefundAmount));

            var validItemsToProcess = new List<(ReturnItemInput Input, SalesInvoiceItem InvoiceItem, decimal ConvertedQty, decimal RefundUnitPrice, decimal LineRefund, decimal PromoPenaltyQty, decimal PromoPenaltyAmt, decimal DiscountReleased)>();

            foreach (var itemInput in request.Items.Where(i => i.Quantity > 0))
            {
                var invoiceItem = invoice.Items.FirstOrDefault(i => i.InvoiceItemID == itemInput.InvoiceItemID);
                if (invoiceItem == null)
                {
                    return OperationResult<int>.Fail($"Invoice item #{itemInput.InvoiceItemID} is invalid for this sales invoice.");
                }

                bool isFree = string.Equals(invoiceItem.ItemType, "FREE", StringComparison.OrdinalIgnoreCase);
                decimal conversionFactor = invoiceItem.ProductUnit?.ConversionToBaseUnit > 0 ? invoiceItem.ProductUnit.ConversionToBaseUnit : 1m;
                if (!ReturnQuantityHelper.TryNormalize(itemInput.Quantity, itemInput.ReturnUnitMode, conversionFactor, out decimal packagingQty, out decimal requestedConverted, out string? normalizeError))
                {
                    return OperationResult<int>.Fail($"Product '{invoiceItem.Product?.ProductName ?? "Unknown"}': {normalizeError}");
                }

                // Persist normalized packaging quantity for refund/storage convention
                itemInput.Quantity = packagingQty;

                bool isCustomFree = isFree && !string.IsNullOrWhiteSpace(invoiceItem.CustomItemName);
                var freeDto = preview.FreePromotions?.FirstOrDefault(f => f.FreeInvoiceItemId == invoiceItem.InvoiceItemID);
                decimal freeItemPrice = isCustomFree
                    ? 0m
                    : (invoiceItem.UnitPrice > 0 ? invoiceItem.UnitPrice : (invoiceItem.Product != null && invoiceItem.Product.BaseSellingPrice > 0 ? invoiceItem.Product.BaseSellingPrice : 0m));

                decimal unreturnedChargedFreeQty = freeDto?.UnreturnedChargedFreeQuantity ?? 0m;
                decimal chargedReturnedQty = isFree ? Math.Min(packagingQty, unreturnedChargedFreeQty) : 0m;

                decimal refundUnitPrice = isFree ? freeItemPrice : invoiceItem.UnitPrice;
                // NORMAL: base-proportional share of historical packaging line total — never live master price.
                decimal lineRefund;
                if (isCustomFree)
                {
                    lineRefund = 0m;
                }
                else if (isFree)
                {
                    lineRefund = Math.Round(chargedReturnedQty * freeItemPrice, 2);
                }
                else
                {
                    decimal originalBase = invoiceItem.ConvertedQuantity > 0m
                        ? invoiceItem.ConvertedQuantity
                        : UnitConversionHelper.ToBaseUnits(invoiceItem.Quantity, conversionFactor);
                    decimal lineTotal = ReturnValuationHelper.ComputeOriginalLineTotal(invoiceItem.Quantity, invoiceItem.UnitPrice);
                    decimal priorConverted = priorGrouped.TryGetValue(invoiceItem.InvoiceItemID, out var pc) ? pc : 0m;
                    decimal priorRefund = priorRefundGrouped.TryGetValue(invoiceItem.InvoiceItemID, out var pr) ? pr : 0m;
                    lineRefund = ReturnValuationHelper.ComputeBaseProportionalAmount(
                        requestedConverted,
                        originalBase,
                        lineTotal,
                        priorConverted,
                        priorRefund);
                }

                decimal promoPenaltyQty = isCustomFree ? 0m : (freeDto?.NewPenaltyChargedQuantity ?? 0m);
                decimal promoPenaltyAmt = isCustomFree ? 0m : (freeDto?.RetainedValue ?? 0m);
                decimal discountReleased = 0m;
                if (!isFree && preview.ItemDiscountReleasedByInvoiceItemId != null
                    && preview.ItemDiscountReleasedByInvoiceItemId.TryGetValue(invoiceItem.InvoiceItemID, out var released))
                {
                    discountReleased = released;
                }

                validItemsToProcess.Add((itemInput, invoiceItem, requestedConverted, refundUnitPrice, lineRefund, promoPenaltyQty, promoPenaltyAmt, discountReleased));
            }

            string settlementMethod = string.Equals(request.SettlementMethod, "CASH", StringComparison.OrdinalIgnoreCase) ? "CASH" : "ACCOUNT_ADJUSTMENT";

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // 1. Insert SalesReturn Header
                var salesReturn = new SalesReturn
                {
                    ReturnNumber = returnNumber,
                    ReturnType = "INVOICE",
                    InvoiceID = invoice.InvoiceID,
                    CustomerID = invoice.CustomerID,
                    WarehouseID = invoice.WarehouseID,
                    SettlementMethod = settlementMethod,
                    ReturnDate = request.ReturnDate,
                    Reason = request.Reason,
                    GrossAmount = preview.GrossReturnedValue,
                    ClawbackPenalty = preview.DiscountClawback,
                    PromoPenalty = preview.PromoPenalty,
                    NetRefundAmount = preview.NetRefundAmount,
                    IncludeSchemeCalculation = request.IncludeSchemeCalculation,
                    CreatedBy = userId,
                    CreatedAt = now,
                    IsDeleted = false
                };

                await _returnRepository.AddSalesReturnAsync(salesReturn, cancellationToken);
                await _returnRepository.SaveChangesAsync(cancellationToken); // Generates SalesReturnID

                // 2. Insert Items, Update Stock & InventoryTransactions
                foreach (var tuple in validItemsToProcess)
                {
                    var returnItem = new SalesReturnItem
                    {
                        SalesReturnID = salesReturn.SalesReturnID,
                        InvoiceItemID = tuple.InvoiceItem.InvoiceItemID,
                        ProductID = tuple.InvoiceItem.ProductID,
                        ProductUnitID = tuple.InvoiceItem.ProductUnitID,
                        Quantity = tuple.Input.Quantity,
                        ConvertedQuantity = tuple.ConvertedQty,
                        RefundUnitPrice = tuple.RefundUnitPrice,
                        RefundAmount = tuple.LineRefund,
                        DiscountAmount = tuple.DiscountReleased,
                        PromoPenaltyQuantity = tuple.PromoPenaltyQty,
                        PromoPenaltyAmount = tuple.PromoPenaltyAmt,
                        Reason = tuple.Input.Reason,
                        ReturnCondition = string.IsNullOrWhiteSpace(tuple.Input.ReturnCondition) ? "Sellable" : tuple.Input.ReturnCondition
                    };
                    await _context.SalesReturnItems.AddAsync(returnItem, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken); // Generates SalesReturnItemID

                    // Custom FREE gifts are non-inventory — no stock restock / transaction
                    if (!tuple.InvoiceItem.ProductID.HasValue)
                    {
                        continue;
                    }

                    // Stock Restocking for returned item (paid or free product).
                    // Sellable -> Quantity; Damaged -> DamagedQuantity (never sellable).
                    bool isDamaged = IsDamagedReturnCondition(returnItem.ReturnCondition);
                    var stock = await _returnRepository.GetInventoryStockTrackedAsync(tuple.InvoiceItem.ProductID.Value, invoice.WarehouseID, cancellationToken);
                    if (stock == null)
                    {
                        stock = new InventoryStock
                        {
                            ProductID = tuple.InvoiceItem.ProductID.Value,
                            WarehouseID = invoice.WarehouseID,
                            Quantity = isDamaged ? 0m : tuple.ConvertedQty,
                            DamagedQuantity = isDamaged ? tuple.ConvertedQty : 0m,
                            UpdatedAt = now
                        };
                        await _returnRepository.AddInventoryStockAsync(stock, cancellationToken);
                    }
                    else
                    {
                        if (isDamaged)
                            stock.DamagedQuantity += tuple.ConvertedQty;
                        else
                            stock.Quantity += tuple.ConvertedQty;
                        stock.UpdatedAt = now;
                    }

                    // InventoryTransaction Log
                    var invTx = new InventoryTransaction
                    {
                        ProductID = tuple.InvoiceItem.ProductID.Value,
                        WarehouseID = invoice.WarehouseID,
                        TransactionType = isDamaged ? "RETURN_DAMAGED" : "RETURN",
                        Quantity = tuple.ConvertedQty,
                        ReferenceNumber = returnNumber,
                        SalesReturnItemID = returnItem.SalesReturnItemID,
                        CreatedBy = userId,
                        CreatedAt = now,
                        IsActive = true,
                        IsDeleted = false
                    };
                    await _returnRepository.AddInventoryTransactionAsync(invTx, cancellationToken);

                    if (!isDamaged)
                    {
                        decimal unitCostInBase = tuple.InvoiceItem.UnitCostInBase > 0
                            ? tuple.InvoiceItem.UnitCostInBase
                            : (tuple.InvoiceItem.ConvertedQuantity > 0
                                ? tuple.InvoiceItem.CostOfGoodsSold / tuple.InvoiceItem.ConvertedQuantity
                                : 0m);

                        await _fifoCostingService.RestoreSellableReturnAsync(
                            tuple.InvoiceItem.ProductID!.Value,
                            invoice.WarehouseID,
                            tuple.ConvertedQty,
                            unitCostInBase,
                            returnItem.SalesReturnItemID,
                            request.ReturnDate,
                            userId,
                            cancellationToken);
                    }
                }

                // Also insert SalesReturnItem for any retained free items that incurred a NEW PromoPenalty (where Quantity returned = 0)
                if (preview.FreePromotions != null)
                {
                    var processedInvoiceItemIds = validItemsToProcess.Select(v => v.InvoiceItem.InvoiceItemID).ToHashSet();
                    foreach (var freeDto in preview.FreePromotions)
                    {
                        if (!processedInvoiceItemIds.Contains(freeDto.FreeInvoiceItemId) && freeDto.NewPenaltyChargedQuantity > 0)
                        {
                            var freeInvoiceItem = invoice.Items.FirstOrDefault(i => i.InvoiceItemID == freeDto.FreeInvoiceItemId);
                            if (freeInvoiceItem != null)
                            {
                                var penaltyReturnItem = new SalesReturnItem
                                {
                                    SalesReturnID = salesReturn.SalesReturnID,
                                    InvoiceItemID = freeInvoiceItem.InvoiceItemID,
                                    ProductID = freeInvoiceItem.ProductID,
                                    ProductUnitID = freeInvoiceItem.ProductUnitID,
                                    Quantity = 0m,
                                    ConvertedQuantity = 0m,
                                    RefundUnitPrice = freeDto.UnitValue,
                                    RefundAmount = 0m,
                                    PromoPenaltyQuantity = freeDto.NewPenaltyChargedQuantity,
                                    PromoPenaltyAmount = freeDto.RetainedValue,
                                    Reason = "Promotional penalty for retained free item",
                                    ReturnCondition = "Sellable"
                                };
                                await _context.SalesReturnItems.AddAsync(penaltyReturnItem, cancellationToken);
                            }
                        }
                    }
                    await _context.SaveChangesAsync(cancellationToken);
                }

                // 3. Customer Ledger — CREDIT for refund; DEBIT/ADJUSTMENT when additional amount due.
                // Do not mutate SalesInvoice SubTotal / DiscountTotal / GrandTotal.
                if (preview.NetRefundAmount > 0)
                {
                    var customerLedger = new CustomerLedger
                    {
                        CustomerID = invoice.CustomerID,
                        TransactionDate = request.ReturnDate,
                        TransactionType = "RETURN",
                        DebitAmount = 0,
                        CreditAmount = preview.NetRefundAmount,
                        SalesInvoiceID = invoice.InvoiceID,
                        SalesReturnID = salesReturn.SalesReturnID,
                        Description = $"Sales Return #{returnNumber} against Sales Invoice #{invoice.InvoiceNumber}",
                        CreatedBy = userId,
                        CreatedAt = now
                    };
                    await _returnRepository.AddCustomerLedgerAsync(customerLedger, cancellationToken);
                }
                else if (preview.NetRefundAmount < 0)
                {
                    decimal additionalDue = Math.Abs(preview.NetRefundAmount);
                    var customerLedger = new CustomerLedger
                    {
                        CustomerID = invoice.CustomerID,
                        TransactionDate = request.ReturnDate,
                        TransactionType = "ADJUSTMENT",
                        DebitAmount = additionalDue,
                        CreditAmount = 0,
                        SalesInvoiceID = invoice.InvoiceID,
                        SalesReturnID = salesReturn.SalesReturnID,
                        Description = $"Sales Return #{returnNumber} discount adjustment (additional amount due) against Sales Invoice #{invoice.InvoiceNumber}",
                        CreatedBy = userId,
                        CreatedAt = now
                    };
                    await _returnRepository.AddCustomerLedgerAsync(customerLedger, cancellationToken);
                }

                // 4. Lock Parent Invoice Permanently (BR-017 / Return Lock)
                invoice.IsLocked = true;
                invoice.UpdatedAt = now;
                invoice.UpdatedBy = userId;

                await _returnRepository.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Sales Return #{ReturnNumber} processed successfully for Invoice #{InvoiceNumber}. Settlement: {Settlement}, Net Refund: {NetRefund}", returnNumber, invoice.InvoiceNumber, settlementMethod, preview.NetRefundAmount);
                return OperationResult<int>.Ok(salesReturn.SalesReturnID, $"Sales Return #{returnNumber} processed successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error processing Sales Return for Invoice #{InvoiceNumber}", invoice.InvoiceNumber);
                return OperationResult<int>.Fail(UserFacingErrorMessages.SalesReturnProcessFailed);
            }
        }

        public async Task<OperationResult<int>> ProcessManualSalesReturnAsync(ProcessManualSalesReturnRequest request, int userId, CancellationToken cancellationToken = default)
        {
            if (request == null || request.CustomerID <= 0 || request.WarehouseID <= 0)
            {
                return OperationResult<int>.Fail("Invalid manual return request. Customer and Warehouse are required.");
            }

            if (request.Items == null || !request.Items.Any())
            {
                return OperationResult<int>.Fail("At least one product item is required for a manual return.");
            }

            var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerID == request.CustomerID && !c.IsDeleted, cancellationToken);
            if (customer == null)
            {
                return OperationResult<int>.Fail($"Customer #{request.CustomerID} not found or inactive.");
            }

            var warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.WarehouseID == request.WarehouseID && !w.IsDeleted, cancellationToken);
            if (warehouse == null)
            {
                return OperationResult<int>.Fail($"Warehouse #{request.WarehouseID} not found or inactive.");
            }

            var validItems = new List<(ManualReturnItemInput Input, Product Product, ProductUnit ProductUnit, decimal ConvertedQty, decimal LineRefund)>();
            decimal grossAmount = 0m;

            foreach (var itemInput in request.Items)
            {
                if (itemInput.Quantity <= 0)
                {
                    return OperationResult<int>.Fail("Return quantity must be greater than zero.");
                }
                if (itemInput.ManualReturnUnitPrice < 0)
                {
                    return OperationResult<int>.Fail("Manual return unit price cannot be negative.");
                }

                var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductID == itemInput.ProductID && !p.IsDeleted, cancellationToken);
                if (product == null)
                {
                    return OperationResult<int>.Fail($"Product #{itemInput.ProductID} not found or inactive.");
                }

                var productUnit = await _context.ProductUnits
                    .Include(pu => pu.Unit)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pu => pu.ProductUnitID == itemInput.ProductUnitID && pu.ProductID == itemInput.ProductID, cancellationToken);

                if (productUnit == null)
                {
                    return OperationResult<int>.Fail($"Selected unit is invalid for product '{product.ProductName}'.");
                }

                decimal conversionFactor = productUnit.ConversionToBaseUnit > 0 ? productUnit.ConversionToBaseUnit : 1m;
                decimal convertedQty = UnitConversionHelper.ToBaseUnits(itemInput.Quantity, conversionFactor);
                decimal lineRefund = Math.Round(itemInput.Quantity * itemInput.ManualReturnUnitPrice, 2);

                grossAmount += lineRefund;
                validItems.Add((itemInput, product, productUnit, convertedQty, lineRefund));
            }

            if (!validItems.Any(i => i.Input.Quantity > 0))
            {
                return OperationResult<int>.Fail("At least one item must have a return quantity greater than zero.");
            }

            decimal netRefundAmount = grossAmount; // No clawbacks or penalties for manual returns
            string settlementMethod = string.Equals(request.SettlementMethod, "CASH", StringComparison.OrdinalIgnoreCase) ? "CASH" : "ACCOUNT_ADJUSTMENT";
            var now = DateTime.UtcNow;
            string returnNumber = await _returnRepository.GenerateSalesReturnNumberAsync(cancellationToken);

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // 1. Insert SalesReturn Header
                var salesReturn = new SalesReturn
                {
                    ReturnNumber = returnNumber,
                    ReturnType = "MANUAL",
                    InvoiceID = null,
                    CustomerID = request.CustomerID,
                    WarehouseID = request.WarehouseID,
                    SettlementMethod = settlementMethod,
                    ReturnDate = request.ReturnDate,
                    Reason = request.Reason,
                    GrossAmount = grossAmount,
                    ClawbackPenalty = 0m,
                    PromoPenalty = 0m,
                    NetRefundAmount = netRefundAmount,
                    IncludeSchemeCalculation = false,
                    CreatedBy = userId,
                    CreatedAt = now,
                    IsDeleted = false
                };

                await _returnRepository.AddSalesReturnAsync(salesReturn, cancellationToken);
                await _returnRepository.SaveChangesAsync(cancellationToken);

                // 2. Insert Items, Restock Inventory & Log Transactions
                foreach (var tuple in validItems)
                {
                    var returnItem = new SalesReturnItem
                    {
                        SalesReturnID = salesReturn.SalesReturnID,
                        InvoiceItemID = null,
                        ProductID = tuple.Product.ProductID,
                        ProductUnitID = tuple.ProductUnit.ProductUnitID,
                        Quantity = tuple.Input.Quantity,
                        ConvertedQuantity = tuple.ConvertedQty,
                        RefundUnitPrice = tuple.Input.ManualReturnUnitPrice,
                        RefundAmount = tuple.LineRefund,
                        Reason = tuple.Input.Reason,
                        ReturnCondition = string.IsNullOrWhiteSpace(tuple.Input.ReturnCondition) ? "Sellable" : tuple.Input.ReturnCondition
                    };
                    await _context.SalesReturnItems.AddAsync(returnItem, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);

                    // Stock Restocking: Sellable -> Quantity; Damaged -> DamagedQuantity (never sellable).
                    bool isDamaged = IsDamagedReturnCondition(returnItem.ReturnCondition);
                    var stock = await _returnRepository.GetInventoryStockTrackedAsync(tuple.Product.ProductID, request.WarehouseID, cancellationToken);
                    if (stock == null)
                    {
                        stock = new InventoryStock
                        {
                            ProductID = tuple.Product.ProductID,
                            WarehouseID = request.WarehouseID,
                            Quantity = isDamaged ? 0m : tuple.ConvertedQty,
                            DamagedQuantity = isDamaged ? tuple.ConvertedQty : 0m,
                            UpdatedAt = now
                        };
                        await _returnRepository.AddInventoryStockAsync(stock, cancellationToken);
                    }
                    else
                    {
                        if (isDamaged)
                            stock.DamagedQuantity += tuple.ConvertedQty;
                        else
                            stock.Quantity += tuple.ConvertedQty;
                        stock.UpdatedAt = now;
                    }

                    // InventoryTransaction Log
                    var invTx = new InventoryTransaction
                    {
                        ProductID = tuple.Product.ProductID,
                        WarehouseID = request.WarehouseID,
                        TransactionType = isDamaged ? "RETURN_DAMAGED" : "RETURN",
                        Quantity = tuple.ConvertedQty,
                        ReferenceNumber = returnNumber,
                        SalesReturnItemID = returnItem.SalesReturnItemID,
                        CreatedBy = userId,
                        CreatedAt = now,
                        IsActive = true,
                        IsDeleted = false
                    };
                    await _returnRepository.AddInventoryTransactionAsync(invTx, cancellationToken);

                    if (!isDamaged)
                    {
                        await _fifoCostingService.RestoreManualReturnAsync(
                            tuple.Product.ProductID,
                            request.WarehouseID,
                            tuple.ConvertedQty,
                            returnItem.SalesReturnItemID,
                            request.ReturnDate,
                            userId,
                            cancellationToken);
                    }
                }

                // 3. Customer Ledger Entry (Created for all returns with NetRefundAmount > 0 regardless of SettlementMethod)
                if (netRefundAmount > 0)
                {
                    var customerLedger = new CustomerLedger
                    {
                        CustomerID = request.CustomerID,
                        TransactionDate = request.ReturnDate,
                        TransactionType = "RETURN",
                        DebitAmount = 0,
                        CreditAmount = netRefundAmount,
                        SalesInvoiceID = null,
                        SalesReturnID = salesReturn.SalesReturnID,
                        Description = $"Manual Sales Return #{returnNumber}",
                        CreatedBy = userId,
                        CreatedAt = now
                    };
                    await _returnRepository.AddCustomerLedgerAsync(customerLedger, cancellationToken);
                }

                await _returnRepository.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Manual Sales Return #{ReturnNumber} processed successfully. Customer ID: {CustomerID}, Settlement: {Settlement}, Net Refund: {NetRefund}", returnNumber, request.CustomerID, settlementMethod, netRefundAmount);
                return OperationResult<int>.Ok(salesReturn.SalesReturnID, $"Manual Sales Return #{returnNumber} processed successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error processing Manual Sales Return for Customer #{CustomerID}", request.CustomerID);
                return OperationResult<int>.Fail(UserFacingErrorMessages.ManualSalesReturnProcessFailed);
            }
        }

        public async Task<PagedResult<SalesReturnListDto>> GetPagedSalesReturnsAsync(SalesReturnFilterDto filter, CancellationToken cancellationToken = default)
        {
            return await _returnRepository.GetPagedSalesReturnsAsync(filter, cancellationToken);
        }

        public async Task<SalesReturnDetailsDto?> GetSalesReturnDetailsAsync(int salesReturnId, CancellationToken cancellationToken = default)
        {
            return await _returnRepository.GetSalesReturnDetailsAsync(salesReturnId, cancellationToken);
        }

        public async Task<OperationResult<PurchaseReturnEligibilityDto>> GetPurchaseReturnEligibilityAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default)
        {
            var invoice = await _returnRepository.GetPurchaseInvoiceForReturnAsync(purchaseInvoiceId, cancellationToken);
            if (invoice == null)
            {
                return OperationResult<PurchaseReturnEligibilityDto>.Fail($"Purchase Invoice #{purchaseInvoiceId} not found.");
            }

            var priorReturnItems = await _returnRepository.GetPriorPurchaseReturnItemsAsync(purchaseInvoiceId, cancellationToken);
            var priorGrouped = priorReturnItems
                .GroupBy(i => i.PurchaseInvoiceItemID)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.ConvertedQuantity));

            var dto = new PurchaseReturnEligibilityDto
            {
                PurchaseInvoiceID = invoice.PurchaseInvoiceID,
                InvoiceNumber = invoice.InvoiceNumber,
                CompanyID = invoice.CompanyID,
                CompanyName = invoice.Company?.CompanyName ?? string.Empty,
                WarehouseID = invoice.WarehouseID,
                WarehouseName = invoice.Warehouse?.Name ?? string.Empty,
                InvoiceDate = invoice.InvoiceDate,
                Items = invoice.Items.Select(item =>
                {
                    decimal priorConverted = priorGrouped.TryGetValue(item.PurchaseItemID, out var pQty) ? pQty : 0m;
                    decimal conversionFactor = item.ProductUnit?.ConversionToBaseUnit > 0 ? item.ProductUnit.ConversionToBaseUnit : 1m;
                    decimal priorInPackagingUnit = UnitConversionHelper.ToDisplayUnits(priorConverted, conversionFactor);
                    decimal remainingConverted = Math.Max(0m, item.ConvertedQuantity - priorConverted);
                    decimal remainingInPackagingUnit = UnitConversionHelper.ToDisplayUnits(remainingConverted, conversionFactor);

                    return new ReturnEligibleItemDto
                    {
                        InvoiceItemID = item.PurchaseItemID,
                        ProductID = item.ProductID,
                        ProductName = item.Product?.ProductName ?? string.Empty,
                        SKU = item.Product?.SKU,
                        ProductUnitID = item.ProductUnitID,
                        UnitName = item.ProductUnit?.Unit?.UnitName ?? string.Empty,
                        ConversionToBaseUnit = conversionFactor,
                        OriginalQuantity = item.Quantity,
                        OriginalConvertedQuantity = item.ConvertedQuantity,
                        AlreadyReturnedQuantity = priorInPackagingUnit,
                        AlreadyReturnedConvertedQuantity = priorConverted,
                        RemainingReturnableQuantity = remainingInPackagingUnit,
                        UnitPriceOrCost = item.UnitCost,
                        ItemType = "NORMAL"
                    };
                }).ToList()
            };

            return OperationResult<PurchaseReturnEligibilityDto>.Ok(dto);
        }

        public async Task<OperationResult<int>> ProcessPurchaseReturnAsync(ProcessPurchaseReturnRequest request, int userId, CancellationToken cancellationToken = default)
        {
            if (request == null || request.PurchaseInvoiceID <= 0)
            {
                return OperationResult<int>.Fail("Invalid purchase return request.");
            }

            if (request.Items == null || !request.Items.Any(i => i.Quantity > 0))
            {
                return OperationResult<int>.Fail("At least one item must have a return quantity greater than zero.");
            }

            var invoice = await _returnRepository.GetPurchaseInvoiceForReturnAsync(request.PurchaseInvoiceID, cancellationToken);
            if (invoice == null)
            {
                return OperationResult<int>.Fail($"Purchase Invoice #{request.PurchaseInvoiceID} not found.");
            }

            var priorReturnItems = await _returnRepository.GetPriorPurchaseReturnItemsAsync(request.PurchaseInvoiceID, cancellationToken);
            var priorGrouped = priorReturnItems
                .GroupBy(i => i.PurchaseInvoiceItemID)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.ConvertedQuantity));

            var validItemsToProcess = new List<(ReturnItemInput Input, PurchaseInvoiceItem InvoiceItem, decimal ConvertedQty, decimal LineRefund)>();
            decimal grossAmount = 0m;

            foreach (var itemInput in request.Items.Where(i => i.Quantity > 0))
            {
                var invoiceItem = invoice.Items.FirstOrDefault(i => i.PurchaseItemID == itemInput.InvoiceItemID);
                if (invoiceItem == null)
                {
                    return OperationResult<int>.Fail($"Purchase invoice item #{itemInput.InvoiceItemID} is invalid.");
                }

                decimal conversionFactor = invoiceItem.ProductUnit?.ConversionToBaseUnit > 0 ? invoiceItem.ProductUnit.ConversionToBaseUnit : 1m;
                decimal requestedConverted = UnitConversionHelper.ToBaseUnits(itemInput.Quantity, conversionFactor);

                decimal priorConverted = priorGrouped.TryGetValue(invoiceItem.PurchaseItemID, out var pQty) ? pQty : 0m;
                decimal remainingConverted = Math.Max(0m, invoiceItem.ConvertedQuantity - priorConverted);

                if (requestedConverted > remainingConverted + 0.0001m)
                {
                    return OperationResult<int>.Fail($"Return quantity for '{invoiceItem.Product?.ProductName}' exceeds remaining returnable quantity.");
                }

                // STOCK SAFETY VALIDATION (BR-009)
                var stock = await _returnRepository.GetInventoryStockTrackedAsync(invoiceItem.ProductID, invoice.WarehouseID, cancellationToken);
                decimal currentStockQty = stock?.Quantity ?? 0m;

                if (currentStockQty < requestedConverted)
                {
                    return OperationResult<int>.Fail($"Insufficient stock for product '{invoiceItem.Product?.ProductName}' in warehouse '{invoice.Warehouse?.Name}'. Current stock: {currentStockQty:N2}, Requested return: {requestedConverted:N2}.");
                }

                decimal lineRefund = Math.Round(itemInput.Quantity * invoiceItem.UnitCost, 2);
                grossAmount += lineRefund;

                validItemsToProcess.Add((itemInput, invoiceItem, requestedConverted, lineRefund));
            }

            var now = DateTime.UtcNow;
            string returnNumber = await _returnRepository.GeneratePurchaseReturnNumberAsync(cancellationToken);

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // 1. Insert PurchaseReturn Header
                var purchaseReturn = new PurchaseReturn
                {
                    ReturnNumber = returnNumber,
                    PurchaseInvoiceID = invoice.PurchaseInvoiceID,
                    CompanyID = invoice.CompanyID,
                    WarehouseID = invoice.WarehouseID,
                    ReturnDate = request.ReturnDate,
                    Reason = request.Reason,
                    GrossAmount = grossAmount,
                    NetRefundAmount = grossAmount,
                    CreatedBy = userId,
                    CreatedAt = now,
                    IsDeleted = false
                };

                await _returnRepository.AddPurchaseReturnAsync(purchaseReturn, cancellationToken);
                await _returnRepository.SaveChangesAsync(cancellationToken); // Generates PurchaseReturnID

                // 2. Insert Items, Decrement Stock & InventoryTransactions
                foreach (var tuple in validItemsToProcess)
                {
                    var returnItem = new PurchaseReturnItem
                    {
                        PurchaseReturnID = purchaseReturn.PurchaseReturnID,
                        PurchaseInvoiceItemID = tuple.InvoiceItem.PurchaseItemID,
                        ProductID = tuple.InvoiceItem.ProductID,
                        ProductUnitID = tuple.InvoiceItem.ProductUnitID,
                        Quantity = tuple.Input.Quantity,
                        ConvertedQuantity = tuple.ConvertedQty,
                        RefundUnitCost = tuple.InvoiceItem.UnitCost,
                        RefundAmount = tuple.LineRefund,
                        Reason = tuple.Input.Reason,
                        ReturnCondition = string.IsNullOrWhiteSpace(tuple.Input.ReturnCondition) ? "Sellable" : tuple.Input.ReturnCondition
                    };
                    await _context.PurchaseReturnItems.AddAsync(returnItem, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken); // Generates PurchaseReturnItemID

                    // Stock Decrement
                    var stock = await _returnRepository.GetInventoryStockTrackedAsync(tuple.InvoiceItem.ProductID, invoice.WarehouseID, cancellationToken);
                    if (stock == null || stock.Quantity < tuple.ConvertedQty)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return OperationResult<int>.Fail($"Insufficient stock for product '{tuple.InvoiceItem.Product?.ProductName}'. Current: {stock?.Quantity ?? 0:N2}, Requested: {tuple.ConvertedQty:N2}.");
                    }

                    stock.Quantity -= tuple.ConvertedQty;
                    stock.UpdatedAt = now;

                    // InventoryTransaction Log
                    var invTx = new InventoryTransaction
                    {
                        ProductID = tuple.InvoiceItem.ProductID,
                        WarehouseID = invoice.WarehouseID,
                        TransactionType = "PURCHASE_RETURN",
                        Quantity = -tuple.ConvertedQty, // Negative = stock out
                        ReferenceNumber = returnNumber,
                        PurchaseReturnItemID = returnItem.PurchaseReturnItemID,
                        CreatedBy = userId,
                        CreatedAt = now,
                        IsActive = true,
                        IsDeleted = false
                    };
                    await _returnRepository.AddInventoryTransactionAsync(invTx, cancellationToken);
                }

                // 3. Company Ledger Entry (Debit reduces what we owe)
                if (grossAmount > 0)
                {
                    var companyLedger = new CompanyLedger
                    {
                        CompanyID = invoice.CompanyID,
                        TransactionDate = request.ReturnDate,
                        TransactionType = "PURCHASE_RETURN",
                        DebitAmount = grossAmount,
                        CreditAmount = 0,
                        PurchaseInvoiceID = invoice.PurchaseInvoiceID,
                        PurchaseReturnID = purchaseReturn.PurchaseReturnID,
                        Description = $"Purchase Return #{returnNumber} against Purchase Invoice #{invoice.InvoiceNumber}",
                        CreatedBy = userId,
                        CreatedAt = now
                    };
                    await _returnRepository.AddCompanyLedgerAsync(companyLedger, cancellationToken);
                }

                // 4. Lock Parent Invoice
                invoice.UpdatedAt = now;
                invoice.UpdatedBy = userId;

                await _returnRepository.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Purchase Return #{ReturnNumber} processed successfully for Purchase Invoice #{InvoiceNumber}. Refund: {Refund}", returnNumber, invoice.InvoiceNumber, grossAmount);
                return OperationResult<int>.Ok(purchaseReturn.PurchaseReturnID, $"Purchase Return #{returnNumber} processed successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error processing Purchase Return for Invoice #{InvoiceNumber}", invoice.InvoiceNumber);
                return OperationResult<int>.Fail(UserFacingErrorMessages.PurchaseReturnProcessFailed);
            }
        }

        public async Task<PagedResult<PurchaseReturnListDto>> GetPagedPurchaseReturnsAsync(PurchaseReturnFilterDto filter, CancellationToken cancellationToken = default)
        {
            return await _returnRepository.GetPagedPurchaseReturnsAsync(filter, cancellationToken);
        }

        public async Task<PurchaseReturnDetailsDto?> GetPurchaseReturnDetailsAsync(int purchaseReturnId, CancellationToken cancellationToken = default)
        {
            return await _returnRepository.GetPurchaseReturnDetailsAsync(purchaseReturnId, cancellationToken);
        }

        /// <summary>
        /// Damaged returns restock DamagedQuantity only. Null/blank/unknown conditions default to Sellable.
        /// </summary>
        private static bool IsDamagedReturnCondition(string? returnCondition)
        {
            return !string.IsNullOrWhiteSpace(returnCondition)
                && string.Equals(returnCondition.Trim(), "Damaged", StringComparison.OrdinalIgnoreCase);
        }
    }
}
