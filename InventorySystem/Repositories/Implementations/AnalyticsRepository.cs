using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Analytics;
using InventorySystem.Helpers;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services;

namespace InventorySystem.Repositories.Implementations
{
    public class AnalyticsRepository : IAnalyticsRepository
    {
        private readonly ApplicationDbContext _context;

        public AnalyticsRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<AnalyticsSalesInvoiceRow>> GetSalesInvoiceRowsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var query = _context.SalesInvoices.AsNoTracking()
                .Where(s => s.InvoiceDate >= startDate && s.InvoiceDate <= endDate);

            query = ApplySalesHeaderFilters(query, filter);

            if (AnalyticsFilterHelper.HasCategory(filter))
            {
                int categoryId = filter.CategoryID!.Value;
                query = query.Where(s => s.Items.Any(i => !i.IsDeleted && i.ProductID != null && i.Product != null && i.Product.CategoryID == categoryId));
            }

            return await query.Select(s => new AnalyticsSalesInvoiceRow
            {
                InvoiceID = s.InvoiceID,
                InvoiceNumber = s.InvoiceNumber,
                InvoiceDate = s.InvoiceDate,
                CustomerID = s.CustomerID,
                CustomerName = s.Customer.ShopName,
                WarehouseID = s.WarehouseID,
                CompanyID = s.CompanyID,
                CompanyName = s.Company != null ? s.Company.CompanyName : string.Empty,
                BookerID = s.BookerID,
                BookerName = s.Booker != null ? s.Booker.Name : null,
                AreaID = s.AreaID,
                AreaName = s.Area != null ? s.Area.AreaName : null,
                SubAreaID = s.SubAreaID,
                SubAreaName = s.SubArea != null ? s.SubArea.SubAreaName : null,
                GrandTotal = s.GrandTotal,
                DiscountTotal = s.DiscountTotal,
                PaidAmount = s.PaidAmount,
                PaymentStatus = s.PaymentStatus,
                ReturnedAmount = s.SalesReturns.Where(r => !r.IsDeleted).Sum(r => (decimal?)r.NetRefundAmount) ?? 0m
            }).ToListAsync(cancellationToken);
        }

        public async Task<List<AnalyticsSalesItemRow>> GetSalesItemRowsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var query = _context.SalesInvoiceItems.AsNoTracking()
                .Where(i => i.ProductID != null && i.SalesInvoice.InvoiceDate >= startDate && i.SalesInvoice.InvoiceDate <= endDate);

            query = ApplySalesItemFilters(query, filter);

            return await query.Select(i => new AnalyticsSalesItemRow
            {
                InvoiceID = i.InvoiceID,
                InvoiceDate = i.SalesInvoice.InvoiceDate,
                CustomerID = i.SalesInvoice.CustomerID,
                CustomerName = i.SalesInvoice.Customer.ShopName,
                ProductID = i.ProductID,
                ProductName = i.Product != null ? i.Product.ProductName : (i.CustomItemName ?? string.Empty),
                CategoryID = i.Product != null ? i.Product.CategoryID : null,
                CategoryName = i.Product != null && i.Product.Category != null ? i.Product.Category.Name : "Uncategorized",
                Quantity = i.Quantity,
                ConvertedQuantity = i.ConvertedQuantity,
                UnitPrice = i.UnitPrice,
                DiscountAmount = i.DiscountAmount,
                AveragePurchaseCost = i.Product != null ? i.Product.AveragePurchaseCost : 0m,
                CostOfGoodsSold = i.CostOfGoodsSold
            }).ToListAsync(cancellationToken);
        }

        public async Task<List<AnalyticsReturnRow>> GetReturnRowsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var query = _context.SalesReturns.AsNoTracking()
                .Where(r => r.ReturnDate >= startDate && r.ReturnDate <= endDate);

            query = ApplySalesReturnFilters(query, filter);

            if (AnalyticsFilterHelper.HasCategory(filter))
            {
                int categoryId = filter.CategoryID!.Value;

                var itemQuery = _context.SalesReturnItems.AsNoTracking()
                    .Where(i => i.SalesReturn.ReturnDate >= startDate && i.SalesReturn.ReturnDate <= endDate
                        && i.ProductID != null && i.Product != null && i.Product.CategoryID == categoryId);

                itemQuery = ApplySalesReturnItemFilters(itemQuery, filter);

                return await itemQuery
                    .Select(i => new AnalyticsReturnRow
                    {
                        SalesReturnID = i.SalesReturnID,
                        ReturnDate = i.SalesReturn.ReturnDate,
                        CustomerID = i.SalesReturn.CustomerID,
                        InvoiceID = i.SalesReturn.InvoiceID,
                        WarehouseID = i.SalesReturn.WarehouseID ?? (i.SalesReturn.SalesInvoice != null ? i.SalesReturn.SalesInvoice.WarehouseID : null),
                        BookerID = i.SalesReturn.SalesInvoice != null ? i.SalesReturn.SalesInvoice.BookerID : null,
                        NetRefundAmount = i.SalesReturn.NetRefundAmount,
                        ProductID = i.ProductID,
                        CategoryID = i.Product != null ? i.Product.CategoryID : null,
                        LineRefundAmount = i.RefundAmount,
                        HasLineSplit = true
                    }).ToListAsync(cancellationToken);
            }

            return await query.Select(r => new AnalyticsReturnRow
            {
                SalesReturnID = r.SalesReturnID,
                ReturnDate = r.ReturnDate,
                CustomerID = r.CustomerID,
                InvoiceID = r.InvoiceID,
                WarehouseID = r.WarehouseID ?? (r.SalesInvoice != null ? r.SalesInvoice.WarehouseID : null),
                BookerID = r.SalesInvoice != null ? r.SalesInvoice.BookerID : null,
                NetRefundAmount = r.NetRefundAmount,
                LineRefundAmount = r.NetRefundAmount,
                HasLineSplit = false
            }).ToListAsync(cancellationToken);
        }

        public async Task<List<AnalyticsPurchaseInvoiceRow>> GetPurchaseInvoiceRowsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var query = _context.PurchaseInvoices.AsNoTracking()
                .Where(p => p.InvoiceDate >= startDate && p.InvoiceDate <= endDate);

            if (AnalyticsFilterHelper.HasWarehouse(filter))
                query = query.Where(p => p.WarehouseID == filter.WarehouseID);

            if (AnalyticsFilterHelper.HasCompany(filter))
                query = query.Where(p => p.CompanyID == filter.CompanyID);

            if (AnalyticsFilterHelper.HasCategory(filter))
            {
                int categoryId = filter.CategoryID!.Value;
                query = query.Where(p => p.Items.Any(i => !i.IsDeleted && i.Product.CategoryID == categoryId));
            }

            return await query.Select(p => new AnalyticsPurchaseInvoiceRow
            {
                PurchaseInvoiceID = p.PurchaseInvoiceID,
                InvoiceDate = p.InvoiceDate,
                CompanyID = p.CompanyID,
                CompanyName = p.Company.CompanyName,
                WarehouseID = p.WarehouseID,
                GrandTotal = p.GrandTotal
            }).ToListAsync(cancellationToken);
        }

        public async Task<List<AnalyticsPurchaseItemRow>> GetPurchaseItemRowsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var query = _context.PurchaseInvoiceItems.AsNoTracking()
                .Where(i => i.PurchaseInvoice.InvoiceDate >= startDate && i.PurchaseInvoice.InvoiceDate <= endDate);

            if (AnalyticsFilterHelper.HasWarehouse(filter))
                query = query.Where(i => i.PurchaseInvoice.WarehouseID == filter.WarehouseID);

            if (AnalyticsFilterHelper.HasCompany(filter))
                query = query.Where(i => i.PurchaseInvoice.CompanyID == filter.CompanyID);

            if (AnalyticsFilterHelper.HasCategory(filter))
                query = query.Where(i => i.Product.CategoryID == filter.CategoryID);

            return await query.Select(i => new AnalyticsPurchaseItemRow
            {
                ProductID = i.ProductID,
                ProductName = i.Product.ProductName,
                CategoryID = i.Product.CategoryID,
                InvoiceDate = i.PurchaseInvoice.InvoiceDate,
                WarehouseID = i.PurchaseInvoice.WarehouseID,
                ConvertedQuantity = i.ConvertedQuantity,
                TotalCost = i.TotalCost
            }).ToListAsync(cancellationToken);
        }

        public async Task<decimal> GetCustomerPaymentsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var query = _context.CustomerPayments.AsNoTracking()
                .Where(p => p.PaymentDate >= startDate && p.PaymentDate <= endDate);

            if (AnalyticsFilterHelper.HasWarehouse(filter))
                query = query.Where(p => p.SalesInvoice.WarehouseID == filter.WarehouseID);

            if (AnalyticsFilterHelper.HasCustomer(filter))
                query = query.Where(p => p.CustomerID == filter.CustomerID);

            if (AnalyticsFilterHelper.HasBooker(filter))
            {
                if (filter.BookerID == -1)
                    query = query.Where(p => p.SalesInvoice.BookerID == null);
                else
                    query = query.Where(p => p.SalesInvoice.BookerID == filter.BookerID);
            }

            if (AnalyticsFilterHelper.HasCompany(filter))
                query = query.Where(p => p.SalesInvoice.CompanyID == filter.CompanyID);

            return await query.SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        }

        public async Task<decimal> GetCompanyPaymentsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var query = _context.CompanyPayments.AsNoTracking()
                .Where(p => p.PaymentDate >= startDate && p.PaymentDate <= endDate);

            if (AnalyticsFilterHelper.HasWarehouse(filter))
                query = query.Where(p => p.PurchaseInvoice.WarehouseID == filter.WarehouseID);

            return await query.SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        }

        public async Task<decimal> GetCustomerReceivablesAsync(int? customerId, int? companyId, CancellationToken cancellationToken = default)
        {
            var salesQuery = _context.SalesInvoices.AsNoTracking().Where(i => !i.IsDeleted);

            if (companyId.HasValue && companyId.Value > 0)
                salesQuery = salesQuery.Where(i => i.CompanyID == companyId.Value);

            if (customerId.HasValue && customerId.Value > 0)
                salesQuery = salesQuery.Where(i => i.CustomerID == customerId.Value);

            return await DashboardMetricsHelper.SumCustomerReceivablesAsync(salesQuery, cancellationToken);
        }

        public async Task<decimal> GetCompanyPayablesAsync(int? companyId, CancellationToken cancellationToken = default)
        {
            var purchaseQuery = _context.PurchaseInvoices.AsNoTracking().Where(i => !i.IsDeleted);

            if (companyId.HasValue && companyId.Value > 0)
                purchaseQuery = purchaseQuery.Where(i => i.CompanyID == companyId.Value);

            return await DashboardMetricsHelper.SumCompanyPayablesAsync(purchaseQuery, cancellationToken);
        }

        public async Task<Dictionary<int, decimal>> GetCustomerLedgerBalancesAsync(IEnumerable<int> customerIds, CancellationToken cancellationToken = default)
        {
            var ids = customerIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, decimal>();

            return await _context.CustomerLedgers.AsNoTracking()
                .Where(l => ids.Contains(l.CustomerID))
                .GroupBy(l => l.CustomerID)
                .Select(g => new { CustomerID = g.Key, Balance = g.Sum(l => l.DebitAmount - l.CreditAmount) })
                .ToDictionaryAsync(x => x.CustomerID, x => x.Balance, cancellationToken);
        }

        public async Task<Dictionary<int, decimal>> GetCompanyLedgerBalancesAsync(IEnumerable<int> companyIds, CancellationToken cancellationToken = default)
        {
            var ids = companyIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, decimal>();

            return await _context.CompanyLedgers.AsNoTracking()
                .Where(l => ids.Contains(l.CompanyID))
                .GroupBy(l => l.CompanyID)
                .Select(g => new { CompanyID = g.Key, Balance = g.Sum(l => l.CreditAmount - l.DebitAmount) })
                .ToDictionaryAsync(x => x.CompanyID, x => x.Balance, cancellationToken);
        }

        public async Task<List<AnalyticsStockRow>> GetStockRowsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.InventoryStocks.AsNoTracking()
                .Include(s => s.Product).ThenInclude(p => p!.Category)
                .Include(s => s.Product).ThenInclude(p => p!.BaseUnit)
                .Include(s => s.Warehouse)
                .Where(s => s.Product != null && !s.Product.IsDeleted);

            if (AnalyticsFilterHelper.HasWarehouse(filter))
                query = query.Where(s => s.WarehouseID == filter.WarehouseID);

            if (AnalyticsFilterHelper.HasCategory(filter))
                query = query.Where(s => s.Product.CategoryID == filter.CategoryID);

            if (AnalyticsFilterHelper.HasCompany(filter))
                query = query.Where(s => s.Product.CompanyID == filter.CompanyID);

            return await query.Select(s => new AnalyticsStockRow
            {
                ProductID = s.ProductID,
                ProductName = s.Product.ProductName,
                CategoryID = s.Product.CategoryID,
                CategoryName = s.Product.Category != null ? s.Product.Category.Name : "Uncategorized",
                BaseUnit = s.Product.BaseUnit != null ? s.Product.BaseUnit.UnitName : "Units",
                ReorderLevel = s.Product.ReorderLevel,
                AveragePurchaseCost = s.Product.AveragePurchaseCost,
                WarehouseID = s.WarehouseID,
                WarehouseName = s.Warehouse.Name,
                Quantity = s.Quantity
            }).ToListAsync(cancellationToken);
        }

        public async Task<List<AnalyticsMovementRow>> GetMovementRowsAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var query = _context.InventoryTransactions.AsNoTracking()
                .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate);

            if (AnalyticsFilterHelper.HasWarehouse(filter))
                query = query.Where(t => t.WarehouseID == filter.WarehouseID);

            if (AnalyticsFilterHelper.HasCategory(filter))
                query = query.Where(t => t.Product.CategoryID == filter.CategoryID);

            if (AnalyticsFilterHelper.HasCompany(filter))
                query = query.Where(t => t.Product.CompanyID == filter.CompanyID);

            return await query
                .GroupBy(t => t.TransactionType)
                .Select(g => new AnalyticsMovementRow
                {
                    TransactionType = g.Key,
                    Quantity = g.Sum(t => t.Quantity < 0 ? -t.Quantity : t.Quantity)
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<decimal> GetInvoicePromotionAmountAsync(
            AnalyticsFilterDto filter, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var query = _context.InvoicePromotions.AsNoTracking()
                .Where(p => p.SalesInvoice.InvoiceDate >= startDate && p.SalesInvoice.InvoiceDate <= endDate);

            if (AnalyticsFilterHelper.HasWarehouse(filter))
                query = query.Where(p => p.SalesInvoice.WarehouseID == filter.WarehouseID);

            if (AnalyticsFilterHelper.HasCustomer(filter))
                query = query.Where(p => p.SalesInvoice.CustomerID == filter.CustomerID);

            if (AnalyticsFilterHelper.HasBooker(filter))
            {
                if (filter.BookerID == -1)
                    query = query.Where(p => p.SalesInvoice.BookerID == null);
                else
                    query = query.Where(p => p.SalesInvoice.BookerID == filter.BookerID);
            }

            if (AnalyticsFilterHelper.HasCompany(filter))
                query = query.Where(p => p.SalesInvoice.CompanyID == filter.CompanyID);

            return await query.SumAsync(p => (decimal?)p.DiscountAmount, cancellationToken) ?? 0m;
        }

        public async Task<List<AnalyticsLookupItem>> GetWarehousesAsync(CancellationToken cancellationToken = default) =>
            await _context.Warehouses.AsNoTracking().Where(w => w.IsActive)
                .OrderBy(w => w.Name)
                .Select(w => new AnalyticsLookupItem { Id = w.WarehouseID, Name = w.Name })
                .ToListAsync(cancellationToken);

        public async Task<List<AnalyticsLookupItem>> GetCustomersAsync(CancellationToken cancellationToken = default) =>
            await _context.Customers.AsNoTracking().Where(c => c.IsActive)
                .OrderBy(c => c.ShopName)
                .Select(c => new AnalyticsLookupItem { Id = c.CustomerID, Name = c.ShopName })
                .ToListAsync(cancellationToken);

        public async Task<List<AnalyticsLookupItem>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
            await _context.Categories.AsNoTracking().Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new AnalyticsLookupItem { Id = c.CategoryID, Name = c.Name })
                .ToListAsync(cancellationToken);

        public async Task<List<AnalyticsLookupItem>> GetBookersAsync(CancellationToken cancellationToken = default) =>
            await _context.Bookers.AsNoTracking().Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .Select(b => new AnalyticsLookupItem { Id = b.BookerID, Name = b.Name })
                .ToListAsync(cancellationToken);

        public async Task<string?> GetCustomerNameAsync(int customerId, CancellationToken cancellationToken = default) =>
            await _context.Customers.AsNoTracking().Where(c => c.CustomerID == customerId).Select(c => c.ShopName).FirstOrDefaultAsync(cancellationToken);

        public async Task<string?> GetProductNameAsync(int productId, CancellationToken cancellationToken = default) =>
            await _context.Products.AsNoTracking().Where(p => p.ProductID == productId).Select(p => p.ProductName).FirstOrDefaultAsync(cancellationToken);

        public async Task<string?> GetBookerNameAsync(int bookerId, CancellationToken cancellationToken = default) =>
            await _context.Bookers.AsNoTracking().Where(b => b.BookerID == bookerId).Select(b => b.Name).FirstOrDefaultAsync(cancellationToken);

        private static IQueryable<Models.Entities.SalesInvoice> ApplySalesHeaderFilters(
            IQueryable<Models.Entities.SalesInvoice> query, AnalyticsFilterDto filter)
        {
            if (AnalyticsFilterHelper.HasWarehouse(filter))
                query = query.Where(s => s.WarehouseID == filter.WarehouseID);

            if (AnalyticsFilterHelper.HasCustomer(filter))
                query = query.Where(s => s.CustomerID == filter.CustomerID);

            if (AnalyticsFilterHelper.HasCompany(filter))
                query = query.Where(s => s.CompanyID == filter.CompanyID);

            if (AnalyticsFilterHelper.HasBooker(filter))
            {
                if (filter.BookerID == -1)
                    query = query.Where(s => s.BookerID == null);
                else
                    query = query.Where(s => s.BookerID == filter.BookerID);
            }

            return query;
        }

        private static IQueryable<Models.Entities.SalesInvoiceItem> ApplySalesItemFilters(
            IQueryable<Models.Entities.SalesInvoiceItem> query, AnalyticsFilterDto filter)
        {
            if (AnalyticsFilterHelper.HasWarehouse(filter))
                query = query.Where(i => i.SalesInvoice.WarehouseID == filter.WarehouseID);

            if (AnalyticsFilterHelper.HasCustomer(filter))
                query = query.Where(i => i.SalesInvoice.CustomerID == filter.CustomerID);

            if (AnalyticsFilterHelper.HasCompany(filter))
                query = query.Where(i => i.SalesInvoice.CompanyID == filter.CompanyID);

            if (AnalyticsFilterHelper.HasCategory(filter))
                query = query.Where(i => i.Product != null && i.Product.CategoryID == filter.CategoryID);

            if (AnalyticsFilterHelper.HasBooker(filter))
            {
                if (filter.BookerID == -1)
                    query = query.Where(i => i.SalesInvoice.BookerID == null);
                else
                    query = query.Where(i => i.SalesInvoice.BookerID == filter.BookerID);
            }

            return query;
        }

        private static IQueryable<Models.Entities.SalesReturn> ApplySalesReturnFilters(
            IQueryable<Models.Entities.SalesReturn> query, AnalyticsFilterDto filter)
        {
            if (AnalyticsFilterHelper.HasWarehouse(filter))
            {
                int warehouseId = filter.WarehouseID!.Value;
                query = query.Where(r => r.WarehouseID == warehouseId || (r.SalesInvoice != null && r.SalesInvoice.WarehouseID == warehouseId));
            }

            if (AnalyticsFilterHelper.HasCustomer(filter))
                query = query.Where(r => r.CustomerID == filter.CustomerID!.Value);

            if (AnalyticsFilterHelper.HasBooker(filter))
            {
                if (filter.BookerID == -1)
                    query = query.Where(r => r.SalesInvoice != null && r.SalesInvoice.BookerID == null);
                else
                    query = query.Where(r => r.SalesInvoice != null && r.SalesInvoice.BookerID == filter.BookerID);
            }

            if (AnalyticsFilterHelper.HasCompany(filter))
            {
                int companyId = filter.CompanyID!.Value;
                query = query.Where(r =>
                    (r.SalesInvoice != null && r.SalesInvoice.CompanyID == companyId) ||
                    (r.InvoiceID == null && r.Items.Any(i => i.Product != null && i.Product.CompanyID == companyId)));
            }

            return query;
        }

        private static IQueryable<Models.Entities.SalesReturnItem> ApplySalesReturnItemFilters(
            IQueryable<Models.Entities.SalesReturnItem> query, AnalyticsFilterDto filter)
        {
            if (AnalyticsFilterHelper.HasWarehouse(filter))
            {
                int warehouseId = filter.WarehouseID!.Value;
                query = query.Where(i => i.SalesReturn.WarehouseID == warehouseId
                    || (i.SalesReturn.SalesInvoice != null && i.SalesReturn.SalesInvoice.WarehouseID == warehouseId));
            }

            if (AnalyticsFilterHelper.HasCustomer(filter))
                query = query.Where(i => i.SalesReturn.CustomerID == filter.CustomerID!.Value);

            if (AnalyticsFilterHelper.HasBooker(filter))
            {
                if (filter.BookerID == -1)
                    query = query.Where(i => i.SalesReturn.SalesInvoice != null && i.SalesReturn.SalesInvoice.BookerID == null);
                else
                    query = query.Where(i => i.SalesReturn.SalesInvoice != null && i.SalesReturn.SalesInvoice.BookerID == filter.BookerID);
            }

            if (AnalyticsFilterHelper.HasCompany(filter))
            {
                int companyId = filter.CompanyID!.Value;
                query = query.Where(i =>
                    (i.SalesReturn.SalesInvoice != null && i.SalesReturn.SalesInvoice.CompanyID == companyId) ||
                    (i.SalesReturn.InvoiceID == null && i.SalesReturn.Items.Any(x => x.Product != null && x.Product.CompanyID == companyId)));
            }

            return query;
        }
    }
}
