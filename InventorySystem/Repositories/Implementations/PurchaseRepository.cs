using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Purchases;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class PurchaseRepository : IPurchaseRepository
    {
        private readonly ApplicationDbContext _context;

        public PurchaseRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        #region 1. Query Operations

        public async Task<PagedResult<PurchaseListDto>> GetPagedAsync(PurchaseFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.PurchaseInvoices
                .AsNoTracking()
                .Where(p => !p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.InvoiceNumber))
            {
                var term = filter.InvoiceNumber.Trim().ToLower();
                query = query.Where(p => p.InvoiceNumber.ToLower().Contains(term));
            }

            if (filter.CompanyID.HasValue && filter.CompanyID.Value > 0)
            {
                query = query.Where(p => p.CompanyID == filter.CompanyID.Value);
            }

            if (filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0)
            {
                query = query.Where(p => p.WarehouseID == filter.WarehouseID.Value);
            }

            if (filter.DateFrom.HasValue)
            {
                query = query.Where(p => p.InvoiceDate >= filter.DateFrom.Value.Date);
            }

            if (filter.DateTo.HasValue)
            {
                query = query.Where(p => p.InvoiceDate <= filter.DateTo.Value.Date.AddDays(1).AddTicks(-1));
            }

            if (!string.IsNullOrWhiteSpace(filter.PaymentStatus))
            {
                var status = filter.PaymentStatus.Trim().ToUpper();
                query = query.Where(p => p.PaymentStatus == status);
            }

            int totalItems = await query.CountAsync(cancellationToken);

            int pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
            int pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

            var items = await query
                .OrderByDescending(p => p.InvoiceDate)
                .ThenByDescending(p => p.PurchaseInvoiceID)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PurchaseListDto
                {
                    PurchaseInvoiceID = p.PurchaseInvoiceID,
                    InvoiceNumber = p.InvoiceNumber,
                    SupplierInvoiceNumber = p.SupplierInvoiceNumber,
                    CompanyID = p.CompanyID,
                    CompanyName = p.Company.CompanyName,
                    WarehouseID = p.WarehouseID,
                    WarehouseName = p.Warehouse.Name,
                    InvoiceDate = p.InvoiceDate,
                    GrandTotal = p.GrandTotal,
                    PaidAmount = p.PaidAmount,
                    ReturnedAmount = p.PurchaseReturns.Where(r => !r.IsDeleted).Sum(r => (decimal?)r.NetRefundAmount) ?? 0m,
                    PaymentStatus = p.PaymentStatus
                })
                .ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                if (item.ReturnedAmount > 0)
                {
                    decimal netRemaining = Math.Max(0m, item.GrandTotal - item.PaidAmount - item.ReturnedAmount);
                    if (netRemaining <= 0.001m)
                    {
                        item.PaymentStatus = "PAID";
                    }
                    else
                    {
                        item.PaymentStatus = "PARTIAL";
                    }
                }
            }

            return new PagedResult<PurchaseListDto>(items, totalItems, pageNumber, pageSize);
        }

        public async Task<PurchaseDetailsDto?> GetDetailsByIdAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default)
        {
            var header = await _context.PurchaseInvoices
                .AsNoTracking()
                .Where(p => p.PurchaseInvoiceID == purchaseInvoiceId && !p.IsDeleted)
                .Select(p => new PurchaseHeaderDto
                {
                    PurchaseInvoiceID = p.PurchaseInvoiceID,
                    InvoiceNumber = p.InvoiceNumber,
                    SupplierInvoiceNumber = p.SupplierInvoiceNumber,
                    CompanyID = p.CompanyID,
                    CompanyName = p.Company.CompanyName,
                    WarehouseID = p.WarehouseID,
                    WarehouseName = p.Warehouse.Name,
                    InvoiceDate = p.InvoiceDate,
                    SubTotal = p.SubTotal,
                    GrandTotal = p.GrandTotal,
                    PaidAmount = p.PaidAmount,
                    ReturnedAmount = p.PurchaseReturns.Where(r => !r.IsDeleted).Sum(r => (decimal?)r.NetRefundAmount) ?? 0m,
                    PaymentStatus = p.PaymentStatus,
                    CreatedByName = p.CreatedByUser != null ? p.CreatedByUser.FullName : "System",
                    CreatedAt = p.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (header == null) return null;

            if (header.ReturnedAmount > 0)
            {
                decimal netRemaining = Math.Max(0m, header.GrandTotal - header.PaidAmount - header.ReturnedAmount);
                if (netRemaining <= 0.001m)
                {
                    header.PaymentStatus = "PAID";
                }
                else
                {
                    header.PaymentStatus = "PARTIAL";
                }
            }

            var items = await _context.PurchaseInvoiceItems
                .AsNoTracking()
                .Where(i => i.PurchaseInvoiceID == purchaseInvoiceId && !i.IsDeleted)
                .Select(i => new PurchaseItemDto
                {
                    PurchaseItemID = i.PurchaseItemID,
                    ProductID = i.ProductID,
                    ProductName = i.Product.ProductName,
                    SKU = i.Product.SKU ?? string.Empty,
                    ProductUnitID = i.ProductUnitID,
                    UnitName = i.ProductUnit.Unit.UnitName,
                    Quantity = i.Quantity,
                    ConvertedQuantity = i.ConvertedQuantity,
                    BaseUnitName = i.Product.BaseUnit != null ? i.Product.BaseUnit.UnitName : string.Empty,
                    UnitCost = i.UnitCost,
                    TotalCost = i.TotalCost
                })
                .ToListAsync(cancellationToken);

            return new PurchaseDetailsDto
            {
                Header = header,
                Items = items,
                Financial = new PurchaseFinancialSummaryDto
                {
                    GrandTotal = header.GrandTotal,
                    PaidAmount = header.PaidAmount
                }
            };
        }

        public async Task<PurchaseInvoice?> GetByIdAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PurchaseInvoiceID == purchaseInvoiceId && !p.IsDeleted, cancellationToken);
        }

        #endregion

        #region 2. Validation / Existence Checks

        public async Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber, int? excludeId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber)) return false;
            var term = invoiceNumber.Trim().ToLower();

            var query = _context.PurchaseInvoices.AsNoTracking().Where(p => !p.IsDeleted);
            if (excludeId.HasValue && excludeId.Value > 0)
            {
                query = query.Where(p => p.PurchaseInvoiceID != excludeId.Value);
            }

            return await query.AnyAsync(p => p.InvoiceNumber.ToLower() == term, cancellationToken);
        }

        public async Task<string> GenerateNextPurchaseInvoiceNumberAsync(CancellationToken cancellationToken = default)
        {
            string datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            string prefix = $"PINV-{datePart}-";

            var existingNumbers = await _context.PurchaseInvoices
                .AsNoTracking()
                .Where(pi => !pi.IsDeleted && pi.InvoiceNumber.StartsWith(prefix))
                .Select(pi => pi.InvoiceNumber)
                .ToListAsync(cancellationToken);

            int maxSequence = 0;
            foreach (var invoiceNumber in existingNumbers)
            {
                if (invoiceNumber.Length <= prefix.Length)
                {
                    continue;
                }

                string suffix = invoiceNumber[prefix.Length..];
                int separatorIndex = suffix.IndexOf('-');
                if (separatorIndex >= 0)
                {
                    suffix = suffix[..separatorIndex];
                }

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

        public async Task<bool> CompanyExistsAsync(int companyId, CancellationToken cancellationToken = default)
        {
            return await _context.Companies
                .AsNoTracking()
                .AnyAsync(c => c.CompanyID == companyId && !c.IsDeleted, cancellationToken);
        }

        public async Task<bool> WarehouseExistsAsync(int warehouseId, CancellationToken cancellationToken = default)
        {
            return await _context.Warehouses
                .AsNoTracking()
                .AnyAsync(w => w.WarehouseID == warehouseId && !w.IsDeleted && w.IsActive, cancellationToken);
        }

        public async Task<bool> ProductExistsAsync(int productId, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .AnyAsync(p => p.ProductID == productId && !p.IsDeleted && p.IsActive, cancellationToken);
        }

        public async Task<ProductUnit?> GetProductUnitAsync(int productUnitId, CancellationToken cancellationToken = default)
        {
            return await _context.ProductUnits
                .AsNoTracking()
                .Include(pu => pu.Unit)
                .FirstOrDefaultAsync(pu => pu.ProductUnitID == productUnitId && !pu.IsDeleted, cancellationToken);
        }

        #endregion

        #region 3. Financial Queries (Details screen tabs)

        public async Task<PurchaseFinancialSummaryDto?> GetFinancialSummaryAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseInvoices
                .AsNoTracking()
                .Where(p => p.PurchaseInvoiceID == purchaseInvoiceId && !p.IsDeleted)
                .Select(p => new PurchaseFinancialSummaryDto
                {
                    GrandTotal = p.GrandTotal,
                    PaidAmount = p.PaidAmount
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<PagedResult<PurchasePaymentHistoryDto>> GetPaymentHistoryAsync(int purchaseInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.CompanyPayments
                .AsNoTracking()
                .Where(cp => cp.PurchaseInvoiceID == purchaseInvoiceId && !cp.IsDeleted);

            int totalItems = await query.CountAsync(cancellationToken);

            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? 10 : pageSize;

            var items = await query
                .OrderByDescending(cp => cp.PaymentDate)
                .ThenByDescending(cp => cp.CompanyPaymentID)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(cp => new PurchasePaymentHistoryDto
                {
                    CompanyPaymentID = cp.CompanyPaymentID,
                    PaymentDate = cp.PaymentDate,
                    Amount = cp.Amount,
                    PaymentMethod = cp.PaymentMethod,
                    ReferenceNumber = cp.ReferenceNumber,
                    PaidByName = cp.PaidByUser != null ? cp.PaidByUser.FullName : "System",
                    CreatedAt = cp.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<PurchasePaymentHistoryDto>(items, totalItems, pageNumber, pageSize);
        }

        public async Task<PagedResult<PurchaseLedgerEntryDto>> GetLedgerByInvoiceAsync(int purchaseInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.CompanyLedgers
                .AsNoTracking()
                .Where(cl => cl.PurchaseInvoiceID == purchaseInvoiceId);

            int totalItems = await query.CountAsync(cancellationToken);

            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? 10 : pageSize;

            var items = await query
                .OrderByDescending(cl => cl.TransactionDate)
                .ThenByDescending(cl => cl.CompanyLedgerID)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(cl => new PurchaseLedgerEntryDto
                {
                    CompanyLedgerID = cl.CompanyLedgerID,
                    TransactionDate = cl.TransactionDate,
                    TransactionType = cl.TransactionType,
                    DebitAmount = cl.DebitAmount,
                    CreditAmount = cl.CreditAmount,
                    Description = cl.Description,
                    CreatedByName = cl.CreatedByUser != null ? cl.CreatedByUser.FullName : "System",
                    CreatedAt = cl.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<PurchaseLedgerEntryDto>(items, totalItems, pageNumber, pageSize);
        }

        #endregion

        #region 4. Write Operations (used inside atomic transaction)

        public async Task AddInvoiceAsync(PurchaseInvoice invoice, CancellationToken cancellationToken = default)
        {
            await _context.PurchaseInvoices.AddAsync(invoice, cancellationToken);
        }

        public async Task AddInvoiceItemAsync(PurchaseInvoiceItem item, CancellationToken cancellationToken = default)
        {
            await _context.PurchaseInvoiceItems.AddAsync(item, cancellationToken);
        }

        public async Task<InventoryStock?> GetInventoryStockTrackedAsync(int productId, int warehouseId, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryStocks
                .FirstOrDefaultAsync(s => s.ProductID == productId && s.WarehouseID == warehouseId && !s.IsDeleted, cancellationToken);
        }

        public async Task AddInventoryStockAsync(InventoryStock stock, CancellationToken cancellationToken = default)
        {
            await _context.InventoryStocks.AddAsync(stock, cancellationToken);
        }

        public async Task AddInventoryTransactionAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default)
        {
            await _context.InventoryTransactions.AddAsync(transaction, cancellationToken);
        }

        public async Task AddCompanyLedgerAsync(CompanyLedger ledger, CancellationToken cancellationToken = default)
        {
            await _context.CompanyLedgers.AddAsync(ledger, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        #endregion
    }
}
