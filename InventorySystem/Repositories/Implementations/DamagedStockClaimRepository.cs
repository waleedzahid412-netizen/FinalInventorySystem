using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.DamagedStockClaims;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class DamagedStockClaimRepository : IDamagedStockClaimRepository
    {
        private readonly ApplicationDbContext _context;

        public DamagedStockClaimRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateNextClaimNumberAsync(CancellationToken cancellationToken = default)
        {
            string datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            string prefix = $"DSC-{datePart}-";

            var existingNumbers = await _context.DamagedStockClaims
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.ClaimNumber.StartsWith(prefix))
                .Select(c => c.ClaimNumber)
                .ToListAsync(cancellationToken);

            int maxSequence = 0;
            foreach (var claimNumber in existingNumbers)
            {
                if (claimNumber.Length <= prefix.Length)
                {
                    continue;
                }

                string suffix = claimNumber[prefix.Length..];
                if (int.TryParse(suffix, out int sequence) && sequence > maxSequence)
                {
                    maxSequence = sequence;
                }
            }

            int nextSequence = maxSequence + 1;
            return nextSequence <= 9999
                ? $"{prefix}{nextSequence:D4}"
                : $"{prefix}{nextSequence}";
        }

        public async Task<(List<DamagedStockClaimListItemDto> Items, int TotalCount)> GetClaimsAsync(
            DamagedStockClaimFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var query = _context.DamagedStockClaims
                .AsNoTracking()
                .Include(c => c.Company)
                .Include(c => c.Items)
                .AsQueryable();

            if (filter.CompanyID.HasValue && filter.CompanyID.Value > 0)
            {
                query = query.Where(c => c.CompanyID == filter.CompanyID.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.ClaimStatus))
            {
                query = query.Where(c => c.ClaimStatus == filter.ClaimStatus);
            }

            if (!string.IsNullOrWhiteSpace(filter.SettlementStatus))
            {
                query = query.Where(c => c.SettlementStatus == filter.SettlementStatus);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(c => c.ClaimDate >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                var end = filter.ToDate.Value.Date.AddDays(1);
                query = query.Where(c => c.ClaimDate < end);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                string term = filter.SearchTerm.Trim();
                query = query.Where(c =>
                    c.ClaimNumber.Contains(term)
                    || c.Company.CompanyName.Contains(term));
            }

            int total = await query.CountAsync(cancellationToken);

            int page = filter.Page < 1 ? 1 : filter.Page;
            int pageSize = filter.PageSize < 1 ? 25 : Math.Min(filter.PageSize, 100);

            var items = await query
                .OrderByDescending(c => c.ClaimDate)
                .ThenByDescending(c => c.DamagedStockClaimID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new DamagedStockClaimListItemDto
                {
                    DamagedStockClaimID = c.DamagedStockClaimID,
                    ClaimNumber = c.ClaimNumber,
                    ClaimDate = c.ClaimDate,
                    CompanyID = c.CompanyID,
                    CompanyName = c.Company.CompanyName,
                    TotalProducts = c.Items.Count(i => !i.IsDeleted),
                    TotalClaimedQuantity = c.TotalClaimedQuantity,
                    TotalClaimAmount = c.TotalClaimAmount,
                    ClaimStatus = c.ClaimStatus,
                    SettlementStatus = c.SettlementStatus
                })
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public async Task<List<DamagedStockRowDto>> GetDamagedStockAsync(
            int companyId,
            int? warehouseId = null,
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.InventoryStocks
                .AsNoTracking()
                .Include(s => s.Product)
                .Include(s => s.Warehouse)
                .Where(s => s.DamagedQuantity > 0
                    && s.Product.CompanyID == companyId
                    && !s.Product.IsDeleted);

            if (warehouseId.HasValue && warehouseId.Value > 0)
            {
                query = query.Where(s => s.WarehouseID == warehouseId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();
                query = query.Where(s =>
                    s.Product.ProductName.Contains(term)
                    || (s.Product.SKU != null && s.Product.SKU.Contains(term)));
            }

            var rows = await query
                .OrderBy(s => s.Product.ProductName)
                .ThenBy(s => s.Warehouse.Name)
                .Select(s => new DamagedStockRowDto
                {
                    InventoryStockID = s.InventoryStockID,
                    ProductID = s.ProductID,
                    ProductName = s.Product.ProductName,
                    SKU = s.Product.SKU,
                    WarehouseID = s.WarehouseID,
                    WarehouseName = s.Warehouse.Name,
                    DamagedQuantity = s.DamagedQuantity,
                    ClaimPendingQuantity = s.ClaimPendingQuantity
                })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                row.SuggestedUnitCost = await GetSuggestedUnitCostAsync(row.ProductID, row.WarehouseID, cancellationToken);
            }

            return rows;
        }

        public async Task<DamagedStockClaim?> GetClaimByIdAsync(
            int claimId,
            bool includeDetails = true,
            CancellationToken cancellationToken = default)
        {
            IQueryable<DamagedStockClaim> query = _context.DamagedStockClaims;

            if (includeDetails)
            {
                query = query
                    .Include(c => c.Company)
                    .Include(c => c.CreatedByUser)
                    .Include(c => c.Items).ThenInclude(i => i.Product)
                    .Include(c => c.Items).ThenInclude(i => i.Warehouse)
                    .Include(c => c.Settlements).ThenInclude(s => s.Product)
                    .Include(c => c.Settlements).ThenInclude(s => s.Warehouse);
            }

            return await query.FirstOrDefaultAsync(c => c.DamagedStockClaimID == claimId, cancellationToken);
        }

        public Task<InventoryStock?> GetStockForUpdateAsync(int inventoryStockId, CancellationToken cancellationToken = default)
        {
            return _context.InventoryStocks
                .FirstOrDefaultAsync(s => s.InventoryStockID == inventoryStockId, cancellationToken);
        }

        public async Task<decimal> GetSuggestedUnitCostAsync(int productId, int warehouseId, CancellationToken cancellationToken = default)
        {
            var openLayerCost = await _context.InventoryCostLayers
                .AsNoTracking()
                .Where(l => l.ProductID == productId
                    && l.WarehouseID == warehouseId
                    && l.RemainingQuantity > 0)
                .OrderByDescending(l => l.ReceivedAt)
                .ThenByDescending(l => l.CostLayerID)
                .Select(l => (decimal?)l.UnitCostInBase)
                .FirstOrDefaultAsync(cancellationToken);

            if (openLayerCost.HasValue && openLayerCost.Value > 0)
            {
                return openLayerCost.Value;
            }

            var lastPurchaseCost = await _context.PurchaseInvoiceItems
                .AsNoTracking()
                .Where(i => i.ProductID == productId && !i.PurchaseInvoice.IsDeleted)
                .OrderByDescending(i => i.PurchaseInvoice.InvoiceDate)
                .ThenByDescending(i => i.PurchaseItemID)
                .Select(i => (decimal?)i.UnitCost)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastPurchaseCost.HasValue && lastPurchaseCost.Value > 0)
            {
                return lastPurchaseCost.Value;
            }

            var productAvg = await _context.Products
                .AsNoTracking()
                .Where(p => p.ProductID == productId)
                .Select(p => (decimal?)p.AveragePurchaseCost)
                .FirstOrDefaultAsync(cancellationToken);

            return productAvg ?? 0m;
        }

        public async Task AddClaimAsync(DamagedStockClaim claim, CancellationToken cancellationToken = default)
        {
            await _context.DamagedStockClaims.AddAsync(claim, cancellationToken);
        }

        public async Task AddInventoryTransactionAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default)
        {
            await _context.InventoryTransactions.AddAsync(transaction, cancellationToken);
        }

        public async Task AddCompanyLedgerAsync(CompanyLedger ledger, CancellationToken cancellationToken = default)
        {
            await _context.CompanyLedgers.AddAsync(ledger, cancellationToken);
        }

        public async Task AddSettlementAsync(DamagedStockClaimSettlement settlement, CancellationToken cancellationToken = default)
        {
            await _context.DamagedStockClaimSettlements.AddAsync(settlement, cancellationToken);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
