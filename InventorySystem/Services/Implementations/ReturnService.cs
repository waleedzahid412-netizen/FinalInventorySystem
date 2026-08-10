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
        private readonly ILogger<ReturnService> _logger;

        public ReturnService(
            IReturnRepository returnRepository,
            ApplicationDbContext context,
            ILogger<ReturnService> logger)
        {
            _returnRepository = returnRepository ?? throw new ArgumentNullException(nameof(returnRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
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

            var priorDisplayGrouped = (priorReturnItems ?? new List<SalesReturnItem>())
                .Where(i => i.InvoiceItemID.HasValue)
                .GroupBy(i => i.InvoiceItemID!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var priorChargedGrouped = (priorReturnItems ?? new List<SalesReturnItem>())
                .Where(i => i.InvoiceItemID.HasValue)
                .GroupBy(i => i.InvoiceItemID!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.PromoPenaltyQuantity));

            decimal grossReturnedValue = 0m;
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
                decimal requestedConverted = UnitConversionHelper.ToBaseUnits(itemInput.Quantity, conversionFactor);

                decimal priorConverted = priorGrouped.TryGetValue(invoiceItem.InvoiceItemID, out var pQty) ? pQty : 0m;
                decimal remainingConverted = Math.Max(0m, invoiceItem.ConvertedQuantity - priorConverted);

                if (!isFree)
                {
                    if (requestedConverted > remainingConverted + 0.0001m)
                    {
                        return OperationResult<ClawbackPreviewDto>.Fail($"Return quantity for product '{invoiceItem.Product?.ProductName ?? "Unknown"}' exceeds remaining returnable quantity.");
                    }
                    decimal lineGross = Math.Round(itemInput.Quantity * invoiceItem.UnitPrice, 2);
                    grossReturnedValue += lineGross;
                }
            }

            if (includeSchemeCalculation)
            {
                // 2. Discount Clawback
                if (invoice.InvoiceDiscounts != null && invoice.InvoiceDiscounts.Any())
                {
                    decimal postReturnSubtotal = Math.Max(0m, invoice.SubTotal - grossReturnedValue);
                    foreach (var discountSnapshot in invoice.InvoiceDiscounts)
                    {
                        if (discountSnapshot.DiscountRuleID > 0)
                        {
                            var rule = await _context.DiscountRules.AsNoTracking().FirstOrDefaultAsync(r => r.DiscountRuleID == discountSnapshot.DiscountRuleID, cancellationToken);
                            if (rule != null && postReturnSubtotal < rule.MinimumOrderAmount)
                            {
                                clawbackPenalty += discountSnapshot.DiscountAmount;
                                breakdown.Add($"Discount '{rule.RuleName}' threshold violated (Order Subtotal after return: {postReturnSubtotal:C} < Required: {rule.MinimumOrderAmount:C}). Clawback penalty: {discountSnapshot.DiscountAmount:C}");
                            }
                        }
                    }
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
                            var buyItem = invoiceItems.FirstOrDefault(i => i.ProductID == rule.BuyProductID && !string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase));
                            var freeItem = invoiceItems.FirstOrDefault(i => i.ProductID == rule.FreeProductID && string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase));

                            if (freeItem == null) continue;

                            decimal buyConversion = buyItem?.ProductUnit?.ConversionToBaseUnit > 0 ? buyItem.ProductUnit.ConversionToBaseUnit : 1m;
                            decimal buyReturnedInputQty = buyItem != null ? (itemsInput.FirstOrDefault(i => i.InvoiceItemID == buyItem.InvoiceItemID)?.Quantity ?? 0m) : 0m;
                            decimal buyCurrentReturnedBaseQty = buyItem != null ? UnitConversionHelper.ToBaseUnits(buyReturnedInputQty, buyConversion) : 0m;

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

                            decimal selectedFreeReturnQty = itemsInput.FirstOrDefault(i => i.InvoiceItemID == freeItem.InvoiceItemID)?.Quantity ?? 0m;

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
                                FreeProductId = freeItem.ProductID,
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

            decimal netRefund = Math.Max(0m, grossReturnedValue - clawbackPenalty - promoPenalty);

            var preview = new ClawbackPreviewDto
            {
                GrossReturnedValue = grossReturnedValue,
                DiscountClawback = clawbackPenalty,
                PromoPenalty = promoPenalty,
                NetRefundAmount = netRefund,
                Breakdown = breakdown,
                FreePromotions = freePromotions
            };

            return OperationResult<ClawbackPreviewDto>.Ok(preview);
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

            var validItemsToProcess = new List<(ReturnItemInput Input, SalesInvoiceItem InvoiceItem, decimal ConvertedQty, decimal RefundUnitPrice, decimal LineRefund, decimal PromoPenaltyQty, decimal PromoPenaltyAmt)>();

            foreach (var itemInput in request.Items.Where(i => i.Quantity > 0))
            {
                var invoiceItem = invoice.Items.FirstOrDefault(i => i.InvoiceItemID == itemInput.InvoiceItemID);
                if (invoiceItem == null)
                {
                    return OperationResult<int>.Fail($"Invoice item #{itemInput.InvoiceItemID} is invalid for this sales invoice.");
                }

                bool isFree = string.Equals(invoiceItem.ItemType, "FREE", StringComparison.OrdinalIgnoreCase);
                decimal conversionFactor = invoiceItem.ProductUnit?.ConversionToBaseUnit > 0 ? invoiceItem.ProductUnit.ConversionToBaseUnit : 1m;
                decimal requestedConverted = UnitConversionHelper.ToBaseUnits(itemInput.Quantity, conversionFactor);

                var freeDto = preview.FreePromotions?.FirstOrDefault(f => f.FreeInvoiceItemId == invoiceItem.InvoiceItemID);
                decimal freeItemPrice = invoiceItem.UnitPrice > 0 ? invoiceItem.UnitPrice : (invoiceItem.Product != null && invoiceItem.Product.BaseSellingPrice > 0 ? invoiceItem.Product.BaseSellingPrice : 0m);

                decimal unreturnedChargedFreeQty = freeDto?.UnreturnedChargedFreeQuantity ?? 0m;
                decimal chargedReturnedQty = isFree ? Math.Min(itemInput.Quantity, unreturnedChargedFreeQty) : 0m;

                decimal refundUnitPrice = isFree ? freeItemPrice : invoiceItem.UnitPrice;
                decimal lineRefund = isFree ? Math.Round(chargedReturnedQty * freeItemPrice, 2) : Math.Round(itemInput.Quantity * refundUnitPrice, 2);

                decimal promoPenaltyQty = freeDto?.NewPenaltyChargedQuantity ?? 0m;
                decimal promoPenaltyAmt = freeDto?.RetainedValue ?? 0m;

                validItemsToProcess.Add((itemInput, invoiceItem, requestedConverted, refundUnitPrice, lineRefund, promoPenaltyQty, promoPenaltyAmt));
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
                        PromoPenaltyQuantity = tuple.PromoPenaltyQty,
                        PromoPenaltyAmount = tuple.PromoPenaltyAmt,
                        Reason = tuple.Input.Reason,
                        ReturnCondition = string.IsNullOrWhiteSpace(tuple.Input.ReturnCondition) ? "Sellable" : tuple.Input.ReturnCondition
                    };
                    await _context.SalesReturnItems.AddAsync(returnItem, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken); // Generates SalesReturnItemID

                    // Stock Restocking for returned item (paid or free)
                    var stock = await _returnRepository.GetInventoryStockTrackedAsync(tuple.InvoiceItem.ProductID, invoice.WarehouseID, cancellationToken);
                    if (stock == null)
                    {
                        stock = new InventoryStock
                        {
                            ProductID = tuple.InvoiceItem.ProductID,
                            WarehouseID = invoice.WarehouseID,
                            Quantity = tuple.ConvertedQty,
                            UpdatedAt = now
                        };
                        await _returnRepository.AddInventoryStockAsync(stock, cancellationToken);
                    }
                    else
                    {
                        stock.Quantity += tuple.ConvertedQty;
                        stock.UpdatedAt = now;
                    }

                    // InventoryTransaction Log
                    var invTx = new InventoryTransaction
                    {
                        ProductID = tuple.InvoiceItem.ProductID,
                        WarehouseID = invoice.WarehouseID,
                        TransactionType = "RETURN",
                        Quantity = tuple.ConvertedQty,
                        ReferenceNumber = returnNumber,
                        SalesReturnItemID = returnItem.SalesReturnItemID,
                        CreatedBy = userId,
                        CreatedAt = now,
                        IsActive = true,
                        IsDeleted = false
                    };
                    await _returnRepository.AddInventoryTransactionAsync(invTx, cancellationToken);
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

                // 3. Customer Ledger Entry (Created for all returns with NetRefundAmount > 0 regardless of SettlementMethod)
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
                return OperationResult<int>.Fail($"Failed to process sales return: {ex.Message}");
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

                    // Stock Restocking
                    var stock = await _returnRepository.GetInventoryStockTrackedAsync(tuple.Product.ProductID, request.WarehouseID, cancellationToken);
                    if (stock == null)
                    {
                        stock = new InventoryStock
                        {
                            ProductID = tuple.Product.ProductID,
                            WarehouseID = request.WarehouseID,
                            Quantity = tuple.ConvertedQty,
                            UpdatedAt = now
                        };
                        await _returnRepository.AddInventoryStockAsync(stock, cancellationToken);
                    }
                    else
                    {
                        stock.Quantity += tuple.ConvertedQty;
                        stock.UpdatedAt = now;
                    }

                    // InventoryTransaction Log
                    var invTx = new InventoryTransaction
                    {
                        ProductID = tuple.Product.ProductID,
                        WarehouseID = request.WarehouseID,
                        TransactionType = "RETURN",
                        Quantity = tuple.ConvertedQty,
                        ReferenceNumber = returnNumber,
                        SalesReturnItemID = returnItem.SalesReturnItemID,
                        CreatedBy = userId,
                        CreatedAt = now,
                        IsActive = true,
                        IsDeleted = false
                    };
                    await _returnRepository.AddInventoryTransactionAsync(invTx, cancellationToken);
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
                return OperationResult<int>.Fail($"Failed to process manual sales return: {ex.Message}");
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
                return OperationResult<int>.Fail($"Failed to process purchase return: {ex.Message}");
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
    }
}
