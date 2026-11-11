using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventorySystem.Constants;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.DamagedStockClaims;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class DamagedStockClaimService : IDamagedStockClaimService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDamagedStockClaimRepository _repository;
        private readonly IFifoCostingService _fifoCostingService;
        private readonly ILogger<DamagedStockClaimService> _logger;

        public DamagedStockClaimService(
            ApplicationDbContext context,
            IDamagedStockClaimRepository repository,
            IFifoCostingService fifoCostingService,
            ILogger<DamagedStockClaimService> logger)
        {
            _context = context;
            _repository = repository;
            _fifoCostingService = fifoCostingService;
            _logger = logger;
        }

        public Task<(List<DamagedStockClaimListItemDto> Items, int TotalCount)> GetClaimsAsync(
            DamagedStockClaimFilterDto filter,
            CancellationToken cancellationToken = default)
            => _repository.GetClaimsAsync(filter, cancellationToken);

        public Task<List<DamagedStockRowDto>> GetDamagedStockAsync(
            int companyId,
            int? warehouseId = null,
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
            => _repository.GetDamagedStockAsync(companyId, warehouseId, searchTerm, cancellationToken);

        public async Task<DamagedStockClaimDetailDto?> GetClaimDetailAsync(
            int claimId,
            CancellationToken cancellationToken = default)
        {
            var claim = await _repository.GetClaimByIdAsync(claimId, includeDetails: true, cancellationToken);
            if (claim == null)
            {
                return null;
            }

            return MapDetail(claim);
        }

        public async Task<OperationResult<int>> CreateClaimAsync(
            int companyId,
            CreateDamagedStockClaimRequest request,
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (companyId <= 0)
            {
                return OperationResult<int>.Fail("A specific company must be selected.");
            }

            if (request.Lines == null || request.Lines.Count == 0)
            {
                return OperationResult<int>.Fail("Add at least one damaged product line.");
            }

            if (request.Lines.Select(l => l.InventoryStockID).Distinct().Count() != request.Lines.Count)
            {
                return OperationResult<int>.Fail("Duplicate inventory stock lines are not allowed on one claim.");
            }

            var errors = new List<string>();
            var preparedLines = new List<(InventoryStock Stock, CreateDamagedStockClaimLineDto Line, decimal UnitCost, decimal LineAmount)>();

            foreach (var line in request.Lines)
            {
                if (line.ClaimQuantity <= 0)
                {
                    errors.Add("Claim quantity must be greater than zero.");
                    continue;
                }

                var stock = await _repository.GetStockForUpdateAsync(line.InventoryStockID, cancellationToken);
                if (stock == null)
                {
                    errors.Add($"Inventory stock #{line.InventoryStockID} was not found.");
                    continue;
                }

                var product = await _context.Products.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ProductID == stock.ProductID, cancellationToken);
                if (product == null || product.CompanyID != companyId)
                {
                    errors.Add($"Product on stock #{line.InventoryStockID} is outside the selected company.");
                    continue;
                }

                if (line.ClaimQuantity > stock.DamagedQuantity)
                {
                    errors.Add($"{product.ProductName}: claim qty {line.ClaimQuantity:N3} exceeds available damaged {stock.DamagedQuantity:N3}.");
                    continue;
                }

                decimal unitCost = line.UnitClaimCost;
                if (unitCost < 0)
                {
                    errors.Add($"{product.ProductName}: unit claim cost cannot be negative.");
                    continue;
                }

                if (unitCost == 0)
                {
                    unitCost = await _repository.GetSuggestedUnitCostAsync(stock.ProductID, stock.WarehouseID, cancellationToken);
                }

                decimal lineAmount = Math.Round(line.ClaimQuantity * unitCost, 2, MidpointRounding.AwayFromZero);
                preparedLines.Add((stock, line, unitCost, lineAmount));
            }

            if (errors.Count > 0)
            {
                return OperationResult<int>.Fail(errors);
            }

            if (preparedLines.Count == 0)
            {
                return OperationResult<int>.Fail("No valid claim lines to process.");
            }

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                string claimNumber = await _repository.GenerateNextClaimNumberAsync(cancellationToken);
                var now = DateTime.UtcNow;

                var claim = new DamagedStockClaim
                {
                    ClaimNumber = claimNumber,
                    CompanyID = companyId,
                    ClaimDate = request.ClaimDate?.ToUniversalTime() ?? now,
                    ClaimStatus = DamagedStockClaimStatuses.PendingSupplierConfirmation,
                    SettlementStatus = DamagedStockSettlementStatuses.Pending,
                    Remarks = request.Remarks,
                    CreatedAt = now,
                    CreatedBy = userId,
                    TotalClaimedQuantity = preparedLines.Sum(p => p.Line.ClaimQuantity),
                    TotalClaimAmount = preparedLines.Sum(p => p.LineAmount)
                };

                foreach (var prepared in preparedLines)
                {
                    var stock = prepared.Stock;
                    var line = prepared.Line;

                    stock.DamagedQuantity -= line.ClaimQuantity;
                    stock.ClaimPendingQuantity += line.ClaimQuantity;
                    stock.UpdatedAt = now;
                    stock.UpdatedBy = userId;

                    var item = new DamagedStockClaimItem
                    {
                        ProductID = stock.ProductID,
                        WarehouseID = stock.WarehouseID,
                        InventoryStockID = stock.InventoryStockID,
                        DamagedQuantityAvailableAtClaim = stock.DamagedQuantity + line.ClaimQuantity,
                        ClaimedQuantity = line.ClaimQuantity,
                        UnitClaimCost = prepared.UnitCost,
                        LineClaimAmount = prepared.LineAmount,
                        DamageReason = line.DamageReason,
                        Remarks = line.Remarks,
                        RejectedDecision = DamagedStockRejectedDecisions.None
                    };
                    claim.Items.Add(item);
                }

                await _repository.AddClaimAsync(claim, cancellationToken);
                await _repository.SaveChangesAsync(cancellationToken);

                foreach (var item in claim.Items)
                {
                    await _repository.AddInventoryTransactionAsync(new InventoryTransaction
                    {
                        ProductID = item.ProductID,
                        WarehouseID = item.WarehouseID,
                        TransactionType = DamagedStockInventoryTxTypes.ClaimPending,
                        Quantity = -item.ClaimedQuantity,
                        ReferenceNumber = claim.ClaimNumber,
                        DamagedStockClaimItemID = item.DamagedStockClaimItemID,
                        CreatedBy = userId,
                        CreatedAt = now
                    }, cancellationToken);
                }

                await _repository.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                _logger.LogInformation("Created damaged stock claim {ClaimNumber} for company {CompanyId}", claimNumber, companyId);
                return OperationResult<int>.Ok(claim.DamagedStockClaimID, $"Claim {claimNumber} created.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to create damaged stock claim for company {CompanyId}", companyId);
                return OperationResult<int>.Fail("Failed to create claim. Please try again.");
            }
        }

        public async Task<OperationResult> ConfirmSupplierResponseAsync(
            int claimId,
            ConfirmDamagedStockClaimRequest request,
            int userId,
            CancellationToken cancellationToken = default)
        {
            var claim = await _repository.GetClaimByIdAsync(claimId, includeDetails: true, cancellationToken);
            if (claim == null)
            {
                return OperationResult.Fail("Claim not found.");
            }

            if (claim.ClaimStatus != DamagedStockClaimStatuses.PendingSupplierConfirmation)
            {
                return OperationResult.Fail("Only pending claims can be confirmed.");
            }

            if (request.Lines == null || request.Lines.Count == 0)
            {
                return OperationResult.Fail("Provide accept/reject quantities for each line.");
            }

            var activeItems = claim.Items.Where(i => !i.IsDeleted).ToList();
            if (request.Lines.Count != activeItems.Count
                || request.Lines.Select(l => l.DamagedStockClaimItemID).Distinct().Count() != activeItems.Count)
            {
                return OperationResult.Fail("Confirmation must include every claim line exactly once.");
            }

            var errors = new List<string>();
            foreach (var lineReq in request.Lines)
            {
                var item = activeItems.FirstOrDefault(i => i.DamagedStockClaimItemID == lineReq.DamagedStockClaimItemID);
                if (item == null)
                {
                    errors.Add($"Unknown claim item #{lineReq.DamagedStockClaimItemID}.");
                    continue;
                }

                if (lineReq.AcceptedQuantity < 0 || lineReq.RejectedQuantity < 0)
                {
                    errors.Add($"Item #{item.DamagedStockClaimItemID}: quantities cannot be negative.");
                    continue;
                }

                decimal sum = lineReq.AcceptedQuantity + lineReq.RejectedQuantity;
                if (Math.Abs(sum - item.ClaimedQuantity) > 0.0005m)
                {
                    errors.Add($"{item.Product?.ProductName ?? "Item"}: accepted + rejected ({sum:N3}) must equal claimed ({item.ClaimedQuantity:N3}).");
                }
            }

            if (errors.Count > 0)
            {
                return OperationResult.Fail(errors);
            }

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                decimal totalAccepted = 0m;
                decimal totalRejected = 0m;

                foreach (var lineReq in request.Lines)
                {
                    var item = activeItems.First(i => i.DamagedStockClaimItemID == lineReq.DamagedStockClaimItemID);
                    item.AcceptedQuantity = lineReq.AcceptedQuantity;
                    item.RejectedQuantity = lineReq.RejectedQuantity;
                    totalAccepted += lineReq.AcceptedQuantity;
                    totalRejected += lineReq.RejectedQuantity;

                    if (lineReq.AcceptedQuantity > 0)
                    {
                        var stock = await _repository.GetStockForUpdateAsync(item.InventoryStockID, cancellationToken);
                        if (stock == null || stock.ClaimPendingQuantity < lineReq.AcceptedQuantity)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return OperationResult.Fail("Claim-pending stock is insufficient for accepted quantity.");
                        }

                        stock.ClaimPendingQuantity -= lineReq.AcceptedQuantity;
                        stock.UpdatedAt = now;
                        stock.UpdatedBy = userId;

                        await _repository.AddInventoryTransactionAsync(new InventoryTransaction
                        {
                            ProductID = item.ProductID,
                            WarehouseID = item.WarehouseID,
                            TransactionType = DamagedStockInventoryTxTypes.ClaimAcceptedClear,
                            Quantity = -lineReq.AcceptedQuantity,
                            ReferenceNumber = claim.ClaimNumber,
                            DamagedStockClaimItemID = item.DamagedStockClaimItemID,
                            CreatedBy = userId,
                            CreatedAt = now
                        }, cancellationToken);
                    }

                    if (lineReq.RejectedQuantity > 0)
                    {
                        item.RejectedDecision = DamagedStockRejectedDecisions.None;
                    }
                }

                claim.TotalAcceptedQuantity = totalAccepted;
                claim.TotalRejectedQuantity = totalRejected;
                claim.SupplierResponseNotes = request.SupplierResponseNotes;
                claim.ConfirmedAt = now;
                claim.ConfirmedBy = userId;
                claim.UpdatedAt = now;
                claim.UpdatedBy = userId;

                if (totalAccepted == 0 && totalRejected > 0)
                {
                    claim.ClaimStatus = DamagedStockClaimStatuses.Rejected;
                    claim.SettlementStatus = DamagedStockSettlementStatuses.NotRequired;
                }
                else if (totalRejected == 0 && totalAccepted > 0)
                {
                    claim.ClaimStatus = DamagedStockClaimStatuses.Accepted;
                    claim.SettlementStatus = DamagedStockSettlementStatuses.Pending;
                }
                else
                {
                    claim.ClaimStatus = DamagedStockClaimStatuses.PartiallyAccepted;
                    claim.SettlementStatus = totalAccepted > 0
                        ? DamagedStockSettlementStatuses.Pending
                        : DamagedStockSettlementStatuses.NotRequired;
                }

                // Accepted claim amount = proportional by accepted qty per line
                decimal acceptedClaimAmount = activeItems.Sum(i =>
                {
                    if (i.ClaimedQuantity <= 0 || !i.AcceptedQuantity.HasValue)
                    {
                        return 0m;
                    }

                    return Math.Round(
                        (i.AcceptedQuantity.Value / i.ClaimedQuantity) * i.LineClaimAmount,
                        2,
                        MidpointRounding.AwayFromZero);
                });
                // Store remaining settle target implicitly via TotalClaimAmount on accepted portion:
                // keep TotalClaimAmount as original; RemainingSettlement uses accepted value.
                // Overwrite TotalClaimAmount? Plan keeps original; remaining uses accepted portion.
                // We'll track via computed RemainingSettlementAmount in detail DTO.

                await _repository.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                // Auto-close if fully rejected and no pending decisions needed (rejected still needs decisions)
                _ = acceptedClaimAmount; // used conceptually in settlement remaining calc
                return OperationResult.Ok("Supplier confirmation recorded.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed confirming claim {ClaimId}", claimId);
                return OperationResult.Fail("Failed to record supplier confirmation.");
            }
        }

        public async Task<OperationResult> ApplyRejectedDecisionAsync(
            int claimId,
            RejectedStockDecisionRequest request,
            int userId,
            CancellationToken cancellationToken = default)
        {
            var claim = await _repository.GetClaimByIdAsync(claimId, includeDetails: true, cancellationToken);
            if (claim == null)
            {
                return OperationResult.Fail("Claim not found.");
            }

            if (claim.ClaimStatus == DamagedStockClaimStatuses.PendingSupplierConfirmation
                || claim.ClaimStatus == DamagedStockClaimStatuses.Closed)
            {
                return OperationResult.Fail("Rejected stock decisions are not available in the current claim status.");
            }

            var item = claim.Items.FirstOrDefault(i =>
                !i.IsDeleted && i.DamagedStockClaimItemID == request.DamagedStockClaimItemID);
            if (item == null)
            {
                return OperationResult.Fail("Claim item not found.");
            }

            if (!item.RejectedQuantity.HasValue || item.RejectedQuantity.Value <= 0)
            {
                return OperationResult.Fail("This line has no rejected quantity.");
            }

            if (item.RejectedDecision != DamagedStockRejectedDecisions.None
                && item.RejectedDecision != DamagedStockRejectedDecisions.KeepPending)
            {
                return OperationResult.Fail("A final rejected-stock decision was already applied.");
            }

            string decision = request.Decision?.Trim() ?? string.Empty;
            if (decision != DamagedStockRejectedDecisions.ReturnToDamaged
                && decision != DamagedStockRejectedDecisions.WriteOff
                && decision != DamagedStockRejectedDecisions.KeepPending)
            {
                return OperationResult.Fail("Invalid rejected-stock decision.");
            }

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                decimal rejectedQty = item.RejectedQuantity.Value;

                if (decision == DamagedStockRejectedDecisions.KeepPending)
                {
                    item.RejectedDecision = DamagedStockRejectedDecisions.KeepPending;
                    item.RejectedDecisionAt = now;
                    item.RejectedDecisionBy = userId;
                    claim.UpdatedAt = now;
                    claim.UpdatedBy = userId;
                    await _repository.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    return OperationResult.Ok("Rejected quantity kept pending.");
                }

                var stock = await _repository.GetStockForUpdateAsync(item.InventoryStockID, cancellationToken);
                if (stock == null || stock.ClaimPendingQuantity < rejectedQty)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return OperationResult.Fail("Claim-pending stock is insufficient for this decision.");
                }

                stock.ClaimPendingQuantity -= rejectedQty;
                stock.UpdatedAt = now;
                stock.UpdatedBy = userId;

                string txType;
                if (decision == DamagedStockRejectedDecisions.ReturnToDamaged)
                {
                    stock.DamagedQuantity += rejectedQty;
                    txType = DamagedStockInventoryTxTypes.ClaimReturnDamaged;
                }
                else
                {
                    txType = DamagedStockInventoryTxTypes.ClaimWriteOff;
                }

                await _repository.AddInventoryTransactionAsync(new InventoryTransaction
                {
                    ProductID = item.ProductID,
                    WarehouseID = item.WarehouseID,
                    TransactionType = txType,
                    Quantity = -rejectedQty,
                    ReferenceNumber = claim.ClaimNumber,
                    DamagedStockClaimItemID = item.DamagedStockClaimItemID,
                    CreatedBy = userId,
                    CreatedAt = now
                }, cancellationToken);

                item.RejectedDecision = decision;
                item.RejectedDecisionAt = now;
                item.RejectedDecisionBy = userId;
                claim.UpdatedAt = now;
                claim.UpdatedBy = userId;

                await _repository.SaveChangesAsync(cancellationToken);

                await TryAutoCloseInternalAsync(claim, userId, now, cancellationToken);
                await _repository.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return OperationResult.Ok(
                    decision == DamagedStockRejectedDecisions.ReturnToDamaged
                        ? "Rejected stock returned to damaged."
                        : "Rejected stock written off.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed applying rejected decision on claim {ClaimId}", claimId);
                return OperationResult.Fail("Failed to apply rejected-stock decision.");
            }
        }

        public async Task<OperationResult<int>> AddSettlementAsync(
            int claimId,
            AddClaimSettlementRequest request,
            int userId,
            CancellationToken cancellationToken = default)
        {
            var claim = await _repository.GetClaimByIdAsync(claimId, includeDetails: true, cancellationToken);
            if (claim == null)
            {
                return OperationResult<int>.Fail("Claim not found.");
            }

            if (claim.ClaimStatus is DamagedStockClaimStatuses.PendingSupplierConfirmation
                or DamagedStockClaimStatuses.Rejected
                or DamagedStockClaimStatuses.Closed)
            {
                return OperationResult<int>.Fail("Settlements are not available for this claim status.");
            }

            if (claim.SettlementStatus == DamagedStockSettlementStatuses.NotRequired)
            {
                return OperationResult<int>.Fail("No settlement is required for this claim.");
            }

            if (claim.TotalAcceptedQuantity <= 0)
            {
                return OperationResult<int>.Fail("Claim has no accepted quantity to settle.");
            }

            string method = request.SettlementMethod?.Trim() ?? string.Empty;
            var errors = ValidateSettlementRequest(claim, request, method);
            if (errors.Count > 0)
            {
                return OperationResult<int>.Fail(errors);
            }

            if (method == DamagedStockSettlementMethods.DifferentProductReplacement
                && request.ProductID.HasValue)
            {
                var productCompanyId = await _context.Products.AsNoTracking()
                    .Where(p => p.ProductID == request.ProductID.Value)
                    .Select(p => (int?)p.CompanyID)
                    .FirstOrDefaultAsync(cancellationToken);
                if (!productCompanyId.HasValue || productCompanyId.Value != claim.CompanyID)
                {
                    return OperationResult<int>.Fail("Replacement product must belong to the claim company.");
                }
            }

            decimal acceptedClaimAmount = GetAcceptedClaimAmount(claim);
            decimal remaining = Math.Round(acceptedClaimAmount - claim.TotalSettledAmount, 2, MidpointRounding.AwayFromZero);
            if (request.Amount > remaining)
            {
                return OperationResult<int>.Fail($"Settlement amount {request.Amount:N2} exceeds remaining {remaining:N2}.");
            }

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var settlementDate = request.SettlementDate?.ToUniversalTime() ?? now;

                var settlement = new DamagedStockClaimSettlement
                {
                    DamagedStockClaimID = claim.DamagedStockClaimID,
                    SettlementMethod = method,
                    SettlementDate = settlementDate,
                    Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
                    ProductID = request.ProductID,
                    WarehouseID = request.WarehouseID,
                    ReceivedQuantity = request.ReceivedQuantity,
                    UnitCostInBase = request.UnitCostInBase,
                    IsVerifiedReceived = request.IsVerifiedReceived,
                    PaymentMethod = request.PaymentMethod,
                    BankName = request.BankName,
                    ReferenceNumber = request.ReferenceNumber,
                    Remarks = request.Remarks,
                    CreatedAt = now,
                    CreatedBy = userId,
                    IsPosted = false
                };

                // Post side effects
                if (method is DamagedStockSettlementMethods.SameProductReplacement
                    or DamagedStockSettlementMethods.DifferentProductReplacement)
                {
                    if (!request.IsVerifiedReceived)
                    {
                        await _repository.AddSettlementAsync(settlement, cancellationToken);
                        claim.TotalSettledAmount = Math.Round(claim.TotalSettledAmount + settlement.Amount, 2, MidpointRounding.AwayFromZero);
                        RefreshSettlementStatus(claim, acceptedClaimAmount);
                        claim.UpdatedAt = now;
                        claim.UpdatedBy = userId;
                        await _repository.SaveChangesAsync(cancellationToken);
                        await TryAutoCloseInternalAsync(claim, userId, now, cancellationToken);
                        await _repository.SaveChangesAsync(cancellationToken);
                        await tx.CommitAsync(cancellationToken);
                        return OperationResult<int>.Ok(settlement.DamagedStockClaimSettlementID,
                            "Settlement recorded (awaiting physical verification before inventory post).");
                    }

                    int productId = request.ProductID!.Value;
                    int warehouseId = request.WarehouseID!.Value;
                    decimal qty = request.ReceivedQuantity!.Value;
                    decimal unitCost = request.UnitCostInBase ?? 0m;

                    var stock = await _context.InventoryStocks
                        .FirstOrDefaultAsync(s => s.ProductID == productId && s.WarehouseID == warehouseId, cancellationToken);

                    if (stock == null)
                    {
                        stock = new InventoryStock
                        {
                            ProductID = productId,
                            WarehouseID = warehouseId,
                            Quantity = 0,
                            DamagedQuantity = 0,
                            ClaimPendingQuantity = 0,
                            CreatedAt = now,
                            CreatedBy = userId,
                            IsActive = true
                        };
                        await _context.InventoryStocks.AddAsync(stock, cancellationToken);
                    }

                    stock.Quantity += qty;
                    stock.UpdatedAt = now;
                    stock.UpdatedBy = userId;

                    var invTx = new InventoryTransaction
                    {
                        ProductID = productId,
                        WarehouseID = warehouseId,
                        TransactionType = DamagedStockInventoryTxTypes.ClaimReplacement,
                        Quantity = qty,
                        ReferenceNumber = claim.ClaimNumber,
                        CreatedBy = userId,
                        CreatedAt = now
                    };
                    await _repository.AddInventoryTransactionAsync(invTx, cancellationToken);
                    await _repository.SaveChangesAsync(cancellationToken);

                    await _fifoCostingService.CreateLayerFromClaimReplacementAsync(
                        productId, warehouseId, qty, unitCost, settlementDate, userId, cancellationToken);

                    settlement.InventoryTransactionID = invTx.TransactionID;
                    settlement.IsPosted = true;
                }
                else if (method is DamagedStockSettlementMethods.LedgerAdjustment
                         or DamagedStockSettlementMethods.CashRefund)
                {
                    var ledger = new CompanyLedger
                    {
                        CompanyID = claim.CompanyID,
                        TransactionDate = settlementDate,
                        TransactionType = "CLAIM_SETTLEMENT",
                        DebitAmount = settlement.Amount,
                        CreditAmount = 0,
                        DamagedStockClaimID = claim.DamagedStockClaimID,
                        Description = method == DamagedStockSettlementMethods.CashRefund
                            ? $"Claim cash refund {claim.ClaimNumber}" +
                              (string.IsNullOrWhiteSpace(request.PaymentMethod) ? "" : $" ({request.PaymentMethod})")
                            : $"Claim ledger adjustment {claim.ClaimNumber}",
                        CreatedBy = userId,
                        CreatedAt = now
                    };
                    await _repository.AddCompanyLedgerAsync(ledger, cancellationToken);
                    await _repository.SaveChangesAsync(cancellationToken);

                    settlement.CompanyLedgerID = ledger.CompanyLedgerID;
                    settlement.IsPosted = true;
                }

                await _repository.AddSettlementAsync(settlement, cancellationToken);

                claim.TotalSettledAmount = Math.Round(claim.TotalSettledAmount + settlement.Amount, 2, MidpointRounding.AwayFromZero);
                RefreshSettlementStatus(claim, acceptedClaimAmount);
                claim.UpdatedAt = now;
                claim.UpdatedBy = userId;

                await _repository.SaveChangesAsync(cancellationToken);
                await TryAutoCloseInternalAsync(claim, userId, now, cancellationToken);
                await _repository.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return OperationResult<int>.Ok(settlement.DamagedStockClaimSettlementID, "Settlement recorded.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed adding settlement on claim {ClaimId}", claimId);
                return OperationResult<int>.Fail("Failed to record settlement.");
            }
        }

        public async Task<OperationResult> TryCloseClaimAsync(
            int claimId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            var claim = await _repository.GetClaimByIdAsync(claimId, includeDetails: true, cancellationToken);
            if (claim == null)
            {
                return OperationResult.Fail("Claim not found.");
            }

            if (claim.ClaimStatus == DamagedStockClaimStatuses.Closed)
            {
                return OperationResult.Ok("Claim is already closed.");
            }

            var now = DateTime.UtcNow;
            bool closed = await TryAutoCloseInternalAsync(claim, userId, now, cancellationToken);
            if (!closed)
            {
                return OperationResult.Fail(GetCloseBlockReason(claim));
            }

            await _repository.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok("Claim closed.");
        }

        private static List<string> ValidateSettlementRequest(
            DamagedStockClaim claim,
            AddClaimSettlementRequest request,
            string method)
        {
            var errors = new List<string>();

            if (method != DamagedStockSettlementMethods.SameProductReplacement
                && method != DamagedStockSettlementMethods.DifferentProductReplacement
                && method != DamagedStockSettlementMethods.LedgerAdjustment
                && method != DamagedStockSettlementMethods.CashRefund)
            {
                errors.Add("Invalid settlement method.");
                return errors;
            }

            if (request.Amount <= 0)
            {
                errors.Add("Settlement amount must be greater than zero.");
            }

            if (method is DamagedStockSettlementMethods.SameProductReplacement
                or DamagedStockSettlementMethods.DifferentProductReplacement)
            {
                if (!request.ProductID.HasValue || request.ProductID.Value <= 0)
                {
                    errors.Add("Replacement product is required.");
                }

                if (!request.WarehouseID.HasValue || request.WarehouseID.Value <= 0)
                {
                    errors.Add("Warehouse is required for replacement receipt.");
                }

                if (!request.ReceivedQuantity.HasValue || request.ReceivedQuantity.Value <= 0)
                {
                    errors.Add("Received quantity is required.");
                }

                if (method == DamagedStockSettlementMethods.SameProductReplacement
                    && request.ProductID.HasValue)
                {
                    var acceptedProductIds = claim.Items
                        .Where(i => !i.IsDeleted && (i.AcceptedQuantity ?? 0) > 0)
                        .Select(i => i.ProductID)
                        .Distinct()
                        .ToHashSet();

                    if (!acceptedProductIds.Contains(request.ProductID.Value))
                    {
                        errors.Add("Same-product replacement must use a product accepted on this claim.");
                    }
                }
            }

            if (method == DamagedStockSettlementMethods.CashRefund)
            {
                if (string.IsNullOrWhiteSpace(request.PaymentMethod)
                    || (request.PaymentMethod != "Cash"
                        && request.PaymentMethod != "Bank"
                        && request.PaymentMethod != "Cheque"))
                {
                    errors.Add("Cash refund requires PaymentMethod Cash, Bank, or Cheque.");
                }
            }

            return errors;
        }

        private static decimal GetAcceptedClaimAmount(DamagedStockClaim claim)
        {
            return claim.Items.Where(i => !i.IsDeleted).Sum(i =>
            {
                if (i.ClaimedQuantity <= 0 || !i.AcceptedQuantity.HasValue)
                {
                    return 0m;
                }

                return Math.Round(
                    (i.AcceptedQuantity.Value / i.ClaimedQuantity) * i.LineClaimAmount,
                    2,
                    MidpointRounding.AwayFromZero);
            });
        }

        private static void RefreshSettlementStatus(DamagedStockClaim claim, decimal acceptedClaimAmount)
        {
            if (acceptedClaimAmount <= 0)
            {
                claim.SettlementStatus = DamagedStockSettlementStatuses.NotRequired;
                return;
            }

            if (claim.TotalSettledAmount <= 0)
            {
                claim.SettlementStatus = DamagedStockSettlementStatuses.Pending;
            }
            else if (claim.TotalSettledAmount + 0.009m >= acceptedClaimAmount)
            {
                claim.SettlementStatus = DamagedStockSettlementStatuses.Settled;
                claim.TotalSettledAmount = acceptedClaimAmount;
            }
            else
            {
                claim.SettlementStatus = DamagedStockSettlementStatuses.PartiallySettled;
            }
        }

        private static bool RejectedDecisionsComplete(DamagedStockClaim claim)
        {
            foreach (var item in claim.Items.Where(i => !i.IsDeleted))
            {
                if ((item.RejectedQuantity ?? 0) <= 0)
                {
                    continue;
                }

                if (item.RejectedDecision == DamagedStockRejectedDecisions.None
                    || item.RejectedDecision == DamagedStockRejectedDecisions.KeepPending)
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetCloseBlockReason(DamagedStockClaim claim)
        {
            if (claim.ClaimStatus == DamagedStockClaimStatuses.PendingSupplierConfirmation)
            {
                return "Confirm supplier response before closing.";
            }

            if (!RejectedDecisionsComplete(claim))
            {
                return "Resolve all rejected stock decisions (Return to Damaged or Write Off) before closing.";
            }

            decimal acceptedAmount = GetAcceptedClaimAmount(claim);
            if (acceptedAmount > 0 && claim.SettlementStatus != DamagedStockSettlementStatuses.Settled)
            {
                return "Complete settlement of the accepted claim amount before closing.";
            }

            return "Claim cannot be closed yet.";
        }

        private Task<bool> TryAutoCloseInternalAsync(
            DamagedStockClaim claim,
            int userId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            if (claim.ClaimStatus == DamagedStockClaimStatuses.Closed)
            {
                return Task.FromResult(true);
            }

            if (claim.ClaimStatus == DamagedStockClaimStatuses.PendingSupplierConfirmation)
            {
                return Task.FromResult(false);
            }

            if (!RejectedDecisionsComplete(claim))
            {
                return Task.FromResult(false);
            }

            decimal acceptedAmount = GetAcceptedClaimAmount(claim);
            if (acceptedAmount > 0 && claim.SettlementStatus != DamagedStockSettlementStatuses.Settled)
            {
                return Task.FromResult(false);
            }

            claim.ClaimStatus = DamagedStockClaimStatuses.Closed;
            claim.ClosedAt = now;
            claim.ClosedBy = userId;
            claim.UpdatedAt = now;
            claim.UpdatedBy = userId;
            return Task.FromResult(true);
        }

        private static DamagedStockClaimDetailDto MapDetail(DamagedStockClaim claim)
        {
            decimal acceptedAmount = GetAcceptedClaimAmount(claim);
            decimal remaining = Math.Max(0, Math.Round(acceptedAmount - claim.TotalSettledAmount, 2, MidpointRounding.AwayFromZero));

            var dto = new DamagedStockClaimDetailDto
            {
                DamagedStockClaimID = claim.DamagedStockClaimID,
                ClaimNumber = claim.ClaimNumber,
                ClaimDate = claim.ClaimDate,
                CompanyID = claim.CompanyID,
                CompanyName = claim.Company?.CompanyName ?? string.Empty,
                ClaimStatus = claim.ClaimStatus,
                SettlementStatus = claim.SettlementStatus,
                TotalClaimedQuantity = claim.TotalClaimedQuantity,
                TotalAcceptedQuantity = claim.TotalAcceptedQuantity,
                TotalRejectedQuantity = claim.TotalRejectedQuantity,
                TotalClaimAmount = claim.TotalClaimAmount,
                TotalSettledAmount = claim.TotalSettledAmount,
                RemainingSettlementAmount = remaining,
                Remarks = claim.Remarks,
                SupplierResponseNotes = claim.SupplierResponseNotes,
                ConfirmedAt = claim.ConfirmedAt,
                ClosedAt = claim.ClosedAt,
                CreatedAt = claim.CreatedAt,
                CreatedByName = claim.CreatedByUser?.FullName ?? string.Empty,
                Items = claim.Items.Where(i => !i.IsDeleted).Select(i => new DamagedStockClaimItemDetailDto
                {
                    DamagedStockClaimItemID = i.DamagedStockClaimItemID,
                    ProductID = i.ProductID,
                    ProductName = i.Product?.ProductName ?? string.Empty,
                    SKU = i.Product?.SKU,
                    WarehouseID = i.WarehouseID,
                    WarehouseName = i.Warehouse?.Name ?? string.Empty,
                    InventoryStockID = i.InventoryStockID,
                    DamagedQuantityAvailableAtClaim = i.DamagedQuantityAvailableAtClaim,
                    ClaimedQuantity = i.ClaimedQuantity,
                    UnitClaimCost = i.UnitClaimCost,
                    LineClaimAmount = i.LineClaimAmount,
                    AcceptedQuantity = i.AcceptedQuantity,
                    RejectedQuantity = i.RejectedQuantity,
                    RejectedDecision = i.RejectedDecision,
                    RejectedDecisionAt = i.RejectedDecisionAt,
                    DamageReason = i.DamageReason,
                    Remarks = i.Remarks
                }).ToList(),
                Settlements = claim.Settlements.Where(s => !s.IsDeleted).OrderBy(s => s.CreatedAt).Select(s => new DamagedStockClaimSettlementDetailDto
                {
                    DamagedStockClaimSettlementID = s.DamagedStockClaimSettlementID,
                    SettlementMethod = s.SettlementMethod,
                    SettlementDate = s.SettlementDate,
                    Amount = s.Amount,
                    ProductID = s.ProductID,
                    ProductName = s.Product?.ProductName,
                    WarehouseID = s.WarehouseID,
                    WarehouseName = s.Warehouse?.Name,
                    ReceivedQuantity = s.ReceivedQuantity,
                    UnitCostInBase = s.UnitCostInBase,
                    IsVerifiedReceived = s.IsVerifiedReceived,
                    IsPosted = s.IsPosted,
                    PaymentMethod = s.PaymentMethod,
                    BankName = s.BankName,
                    ReferenceNumber = s.ReferenceNumber,
                    CompanyLedgerID = s.CompanyLedgerID,
                    InventoryTransactionID = s.InventoryTransactionID,
                    Remarks = s.Remarks,
                    CreatedAt = s.CreatedAt
                }).ToList()
            };

            dto.CanConfirm = claim.ClaimStatus == DamagedStockClaimStatuses.PendingSupplierConfirmation;
            dto.CanDecideRejected =
                claim.ClaimStatus is DamagedStockClaimStatuses.PartiallyAccepted
                    or DamagedStockClaimStatuses.Accepted
                    or DamagedStockClaimStatuses.Rejected
                && dto.Items.Any(i => (i.RejectedQuantity ?? 0) > 0
                    && (i.RejectedDecision == DamagedStockRejectedDecisions.None
                        || i.RejectedDecision == DamagedStockRejectedDecisions.KeepPending));
            dto.CanSettle =
                claim.ClaimStatus is DamagedStockClaimStatuses.Accepted
                    or DamagedStockClaimStatuses.PartiallyAccepted
                && claim.SettlementStatus is DamagedStockSettlementStatuses.Pending
                    or DamagedStockSettlementStatuses.PartiallySettled
                && remaining > 0;
            dto.CanClose = claim.ClaimStatus != DamagedStockClaimStatuses.Closed
                && claim.ClaimStatus != DamagedStockClaimStatuses.PendingSupplierConfirmation
                && RejectedDecisionsComplete(claim)
                && (acceptedAmount <= 0 || claim.SettlementStatus == DamagedStockSettlementStatuses.Settled);

            var history = new List<DamagedStockClaimHistoryEntryDto>
            {
                new()
                {
                    At = claim.CreatedAt,
                    Event = "Claim Created",
                    Detail = $"{claim.ClaimNumber} — {claim.TotalClaimedQuantity:N3} units / {claim.TotalClaimAmount:N2}"
                }
            };

            if (claim.ConfirmedAt.HasValue)
            {
                history.Add(new DamagedStockClaimHistoryEntryDto
                {
                    At = claim.ConfirmedAt.Value,
                    Event = "Supplier Confirmation",
                    Detail = $"Accepted {claim.TotalAcceptedQuantity:N3}, Rejected {claim.TotalRejectedQuantity:N3}. Status: {claim.ClaimStatus}"
                });
            }

            foreach (var item in dto.Items.Where(i => i.RejectedDecisionAt.HasValue
                         && i.RejectedDecision != DamagedStockRejectedDecisions.None))
            {
                history.Add(new DamagedStockClaimHistoryEntryDto
                {
                    At = item.RejectedDecisionAt!.Value,
                    Event = "Rejected Stock Decision",
                    Detail = $"{item.ProductName}: {item.RejectedDecision} ({item.RejectedQuantity:N3})"
                });
            }

            foreach (var settlement in dto.Settlements)
            {
                history.Add(new DamagedStockClaimHistoryEntryDto
                {
                    At = settlement.CreatedAt,
                    Event = "Settlement",
                    Detail = $"{settlement.SettlementMethod} — {settlement.Amount:N2}"
                        + (settlement.IsPosted ? " (posted)" : " (not posted)")
                });
            }

            if (claim.ClosedAt.HasValue)
            {
                history.Add(new DamagedStockClaimHistoryEntryDto
                {
                    At = claim.ClosedAt.Value,
                    Event = "Claim Closed",
                    Detail = null
                });
            }

            dto.History = history.OrderBy(h => h.At).ToList();
            return dto;
        }
    }
}
