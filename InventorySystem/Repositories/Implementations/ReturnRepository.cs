using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Returns;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class ReturnRepository : IReturnRepository
    {
        private readonly ApplicationDbContext _context;

        public ReturnRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<PagedResult<SalesReturnListDto>> GetPagedSalesReturnsAsync(SalesReturnFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.SalesReturns
                .AsNoTracking()
                .Where(r => !r.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.ReturnNumber))
            {
                query = query.Where(r => r.ReturnNumber.Contains(filter.ReturnNumber.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(filter.InvoiceNumber))
            {
                query = query.Where(r => r.SalesInvoice != null && r.SalesInvoice.InvoiceNumber.Contains(filter.InvoiceNumber.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(filter.ReturnType))
            {
                query = query.Where(r => r.ReturnType == filter.ReturnType);
            }

            if (!string.IsNullOrWhiteSpace(filter.SettlementMethod))
            {
                query = query.Where(r => r.SettlementMethod == filter.SettlementMethod);
            }

            if (filter.CustomerID.HasValue && filter.CustomerID.Value > 0)
            {
                query = query.Where(r => r.CustomerID == filter.CustomerID.Value);
            }

            if (filter.DateFrom.HasValue)
            {
                query = query.Where(r => r.ReturnDate >= filter.DateFrom.Value);
            }

            if (filter.DateTo.HasValue)
            {
                query = query.Where(r => r.ReturnDate <= filter.DateTo.Value);
            }

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(r => r.ReturnDate)
                .ThenByDescending(r => r.SalesReturnID)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(r => new SalesReturnListDto
                {
                    SalesReturnID = r.SalesReturnID,
                    ReturnNumber = r.ReturnNumber,
                    ReturnType = r.ReturnType ?? "INVOICE",
                    InvoiceID = r.InvoiceID,
                    InvoiceNumber = r.SalesInvoice != null ? r.SalesInvoice.InvoiceNumber : "—",
                    CustomerID = r.CustomerID,
                    CustomerName = r.Customer.ShopName,
                    SettlementMethod = r.SettlementMethod ?? "ACCOUNT_ADJUSTMENT",
                    ReturnDate = r.ReturnDate,
                    GrossAmount = r.GrossAmount,
                    ClawbackPenalty = r.ClawbackPenalty,
                    PromoPenalty = r.PromoPenalty,
                    NetRefundAmount = r.NetRefundAmount,
                    ItemCount = r.Items.Count,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<SalesReturnListDto>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<SalesReturnDetailsDto?> GetSalesReturnDetailsAsync(int salesReturnId, CancellationToken cancellationToken = default)
        {
            var salesReturn = await _context.SalesReturns
                .AsNoTracking()
                .Include(r => r.SalesInvoice)
                .Include(r => r.Customer)
                .Include(r => r.Warehouse)
                .Include(r => r.CreatedByUser)
                .Include(r => r.Items)
                    .ThenInclude(i => i.Product)
                .Include(r => r.Items)
                    .ThenInclude(i => i.ProductUnit)
                        .ThenInclude(u => u.Unit)
                .FirstOrDefaultAsync(r => r.SalesReturnID == salesReturnId && !r.IsDeleted, cancellationToken);

            if (salesReturn == null) return null;

            return new SalesReturnDetailsDto
            {
                Header = new SalesReturnHeaderDto
                {
                    SalesReturnID = salesReturn.SalesReturnID,
                    ReturnNumber = salesReturn.ReturnNumber,
                    ReturnType = salesReturn.ReturnType ?? "INVOICE",
                    InvoiceID = salesReturn.InvoiceID,
                    InvoiceNumber = salesReturn.SalesInvoice != null ? salesReturn.SalesInvoice.InvoiceNumber : "N/A (Manual Return)",
                    CustomerID = salesReturn.CustomerID,
                    CustomerName = salesReturn.Customer.ShopName,
                    WarehouseID = salesReturn.WarehouseID,
                    WarehouseName = salesReturn.Warehouse != null ? salesReturn.Warehouse.Name : null,
                    SettlementMethod = salesReturn.SettlementMethod ?? "ACCOUNT_ADJUSTMENT",
                    ReturnDate = salesReturn.ReturnDate,
                    Reason = salesReturn.Reason,
                    GrossAmount = salesReturn.GrossAmount,
                    ClawbackPenalty = salesReturn.ClawbackPenalty,
                    PromoPenalty = salesReturn.PromoPenalty,
                    NetRefundAmount = salesReturn.NetRefundAmount,
                    IncludeSchemeCalculation = salesReturn.IncludeSchemeCalculation,
                    CreatedByUserName = salesReturn.CreatedByUser != null ? salesReturn.CreatedByUser.Username : "System",
                    CreatedAt = salesReturn.CreatedAt
                },
                Items = salesReturn.Items.Select(i => new SalesReturnItemDto
                {
                    SalesReturnItemID = i.SalesReturnItemID,
                    InvoiceItemID = i.InvoiceItemID,
                    ProductID = i.ProductID,
                    ProductName = i.Product != null ? i.Product.ProductName : "Unknown Product",
                    SKU = i.Product != null ? i.Product.SKU : null,
                    ProductUnitID = i.ProductUnitID,
                    UnitName = i.ProductUnit != null && i.ProductUnit.Unit != null ? i.ProductUnit.Unit.UnitName : "Unit",
                    Quantity = i.Quantity,
                    ConvertedQuantity = i.ConvertedQuantity,
                    RefundUnitPrice = i.RefundUnitPrice,
                    RefundAmount = i.RefundAmount,
                    Reason = i.Reason,
                    ReturnCondition = i.ReturnCondition
                }).ToList()
            };
        }

        public async Task<SalesInvoice?> GetSalesInvoiceForReturnAsync(int salesInvoiceId, CancellationToken cancellationToken = default)
        {
            var invoice = await _context.SalesInvoices
                .Include(i => i.Customer)
                .Include(i => i.Warehouse)
                .Include(i => i.Items)
                    .ThenInclude(item => item.Product)
                .Include(i => i.Items)
                    .ThenInclude(item => item.ProductUnit)
                        .ThenInclude(pu => pu.Unit)
                .Include(i => i.InvoiceDiscounts)
                .Include(i => i.InvoicePromotions)
                    .ThenInclude(ip => ip.PromotionCampaign)
                        .ThenInclude(pc => pc.PromotionRules)
                .FirstOrDefaultAsync(i => i.InvoiceID == salesInvoiceId && !i.IsDeleted, cancellationToken);

            if (invoice != null && invoice.Items != null)
            {
                invoice.Items = invoice.Items.Where(item => !item.IsDeleted).ToList();
            }

            return invoice;
        }

        public async Task<List<SalesReturnItem>> GetPriorSalesReturnItemsAsync(int salesInvoiceId, CancellationToken cancellationToken = default)
        {
            return await _context.SalesReturnItems
                .AsNoTracking()
                .Where(sri => sri.SalesReturn.InvoiceID == salesInvoiceId && !sri.SalesReturn.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<string> GenerateSalesReturnNumberAsync(CancellationToken cancellationToken = default)
        {
            var maxId = await _context.SalesReturns
                .IgnoreQueryFilters()
                .MaxAsync(r => (int?)r.SalesReturnID, cancellationToken) ?? 0;

            return $"SR-{(maxId + 1):D5}";
        }

        public async Task<PagedResult<PurchaseReturnListDto>> GetPagedPurchaseReturnsAsync(PurchaseReturnFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.PurchaseReturns
                .AsNoTracking()
                .Where(r => !r.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.ReturnNumber))
            {
                query = query.Where(r => r.ReturnNumber.Contains(filter.ReturnNumber.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(filter.InvoiceNumber))
            {
                query = query.Where(r => r.PurchaseInvoice.InvoiceNumber.Contains(filter.InvoiceNumber.Trim()));
            }

            if (filter.CompanyID.HasValue && filter.CompanyID.Value > 0)
            {
                query = query.Where(r => r.CompanyID == filter.CompanyID.Value);
            }

            if (filter.DateFrom.HasValue)
            {
                query = query.Where(r => r.ReturnDate >= filter.DateFrom.Value);
            }

            if (filter.DateTo.HasValue)
            {
                query = query.Where(r => r.ReturnDate <= filter.DateTo.Value);
            }

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(r => r.ReturnDate)
                .ThenByDescending(r => r.PurchaseReturnID)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(r => new PurchaseReturnListDto
                {
                    PurchaseReturnID = r.PurchaseReturnID,
                    ReturnNumber = r.ReturnNumber,
                    PurchaseInvoiceID = r.PurchaseInvoiceID,
                    InvoiceNumber = r.PurchaseInvoice.InvoiceNumber,
                    CompanyID = r.CompanyID,
                    CompanyName = r.Company.CompanyName,
                    ReturnDate = r.ReturnDate,
                    GrossAmount = r.GrossAmount,
                    NetRefundAmount = r.NetRefundAmount,
                    ItemCount = r.Items.Count,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<PurchaseReturnListDto>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<PurchaseReturnDetailsDto?> GetPurchaseReturnDetailsAsync(int purchaseReturnId, CancellationToken cancellationToken = default)
        {
            var purchaseReturn = await _context.PurchaseReturns
                .AsNoTracking()
                .Include(r => r.PurchaseInvoice)
                .Include(r => r.Company)
                .Include(r => r.Warehouse)
                .Include(r => r.CreatedByUser)
                .Include(r => r.Items)
                    .ThenInclude(i => i.Product)
                .Include(r => r.Items)
                    .ThenInclude(i => i.ProductUnit)
                        .ThenInclude(u => u.Unit)
                .FirstOrDefaultAsync(r => r.PurchaseReturnID == purchaseReturnId && !r.IsDeleted, cancellationToken);

            if (purchaseReturn == null) return null;

            return new PurchaseReturnDetailsDto
            {
                Header = new PurchaseReturnHeaderDto
                {
                    PurchaseReturnID = purchaseReturn.PurchaseReturnID,
                    ReturnNumber = purchaseReturn.ReturnNumber,
                    PurchaseInvoiceID = purchaseReturn.PurchaseInvoiceID,
                    InvoiceNumber = purchaseReturn.PurchaseInvoice.InvoiceNumber,
                    CompanyID = purchaseReturn.CompanyID,
                    CompanyName = purchaseReturn.Company.CompanyName,
                    WarehouseID = purchaseReturn.WarehouseID,
                    WarehouseName = purchaseReturn.Warehouse.Name,
                    ReturnDate = purchaseReturn.ReturnDate,
                    Reason = purchaseReturn.Reason,
                    GrossAmount = purchaseReturn.GrossAmount,
                    NetRefundAmount = purchaseReturn.NetRefundAmount,
                    CreatedByUserName = purchaseReturn.CreatedByUser.Username,
                    CreatedAt = purchaseReturn.CreatedAt
                },
                Items = purchaseReturn.Items.Select(i => new PurchaseReturnItemDto
                {
                    PurchaseReturnItemID = i.PurchaseReturnItemID,
                    PurchaseInvoiceItemID = i.PurchaseInvoiceItemID,
                    ProductID = i.ProductID,
                    ProductName = i.Product.ProductName,
                    SKU = i.Product.SKU,
                    ProductUnitID = i.ProductUnitID,
                    UnitName = i.ProductUnit.Unit.UnitName,
                    Quantity = i.Quantity,
                    ConvertedQuantity = i.ConvertedQuantity,
                    RefundUnitCost = i.RefundUnitCost,
                    RefundAmount = i.RefundAmount,
                    Reason = i.Reason,
                    ReturnCondition = i.ReturnCondition
                }).ToList()
            };
        }

        public async Task<PurchaseInvoice?> GetPurchaseInvoiceForReturnAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default)
        {
            var invoice = await _context.PurchaseInvoices
                .Include(i => i.Company)
                .Include(i => i.Warehouse)
                .Include(i => i.Items)
                    .ThenInclude(item => item.Product)
                .Include(i => i.Items)
                    .ThenInclude(item => item.ProductUnit)
                        .ThenInclude(pu => pu.Unit)
                .FirstOrDefaultAsync(i => i.PurchaseInvoiceID == purchaseInvoiceId && !i.IsDeleted, cancellationToken);

            if (invoice != null && invoice.Items != null)
            {
                invoice.Items = invoice.Items.Where(item => !item.IsDeleted).ToList();
            }

            return invoice;
        }

        public async Task<List<PurchaseReturnItem>> GetPriorPurchaseReturnItemsAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseReturnItems
                .AsNoTracking()
                .Where(pri => pri.PurchaseReturn.PurchaseInvoiceID == purchaseInvoiceId && !pri.PurchaseReturn.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<string> GeneratePurchaseReturnNumberAsync(CancellationToken cancellationToken = default)
        {
            var maxId = await _context.PurchaseReturns
                .IgnoreQueryFilters()
                .MaxAsync(r => (int?)r.PurchaseReturnID, cancellationToken) ?? 0;

            return $"PR-{(maxId + 1):D5}";
        }

        public async Task AddSalesReturnAsync(SalesReturn salesReturn, CancellationToken cancellationToken = default)
        {
            await _context.SalesReturns.AddAsync(salesReturn, cancellationToken);
        }

        public async Task AddPurchaseReturnAsync(PurchaseReturn purchaseReturn, CancellationToken cancellationToken = default)
        {
            await _context.PurchaseReturns.AddAsync(purchaseReturn, cancellationToken);
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

        public async Task AddCustomerLedgerAsync(CustomerLedger ledger, CancellationToken cancellationToken = default)
        {
            await _context.CustomerLedgers.AddAsync(ledger, cancellationToken);
        }

        public async Task AddCompanyLedgerAsync(CompanyLedger ledger, CancellationToken cancellationToken = default)
        {
            await _context.CompanyLedgers.AddAsync(ledger, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
