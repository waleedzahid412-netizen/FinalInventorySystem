using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Analytics;
using InventorySystem.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace InventorySystem.Services.Implementations
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(ApplicationDbContext context, ILogger<AnalyticsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<AnalyticsKpiSummaryDto> GetKpiSummaryAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var (startDate, endDate, prevStartDate, prevEndDate) = ResolveDates(filter);

            // Base queries
            var salesQuery = _context.SalesInvoices.AsNoTracking().Where(s => !s.IsDeleted);
            var purchaseQuery = _context.PurchaseInvoices.AsNoTracking().Where(p => !p.IsDeleted);
            var returnsQuery = _context.SalesReturns.AsNoTracking().Where(r => !r.IsDeleted);
            var salesItemsQuery = _context.SalesInvoiceItems.AsNoTracking()
                .Include(i => i.Product)
                .Where(i => !i.IsDeleted && !i.SalesInvoice.IsDeleted);

            // Optional filters
            if (filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0)
            {
                salesQuery = salesQuery.Where(s => s.WarehouseID == filter.WarehouseID.Value);
                purchaseQuery = purchaseQuery.Where(p => p.WarehouseID == filter.WarehouseID.Value);
                returnsQuery = returnsQuery.Where(r => r.WarehouseID == filter.WarehouseID.Value || (r.SalesInvoice != null && r.SalesInvoice.WarehouseID == filter.WarehouseID.Value));
                salesItemsQuery = salesItemsQuery.Where(i => i.SalesInvoice.WarehouseID == filter.WarehouseID.Value);
            }

            if (filter.CustomerID.HasValue && filter.CustomerID.Value > 0)
            {
                salesQuery = salesQuery.Where(s => s.CustomerID == filter.CustomerID.Value);
                returnsQuery = returnsQuery.Where(r => r.CustomerID == filter.CustomerID.Value);
                salesItemsQuery = salesItemsQuery.Where(i => i.SalesInvoice.CustomerID == filter.CustomerID.Value);
            }

            if (filter.CategoryID.HasValue && filter.CategoryID.Value > 0)
            {
                salesItemsQuery = salesItemsQuery.Where(i => i.Product.CategoryID == filter.CategoryID.Value);
            }

            // Current Period Aggregations
            var currentSalesInvoices = await salesQuery
                .Where(s => s.InvoiceDate >= startDate && s.InvoiceDate <= endDate)
                .Select(s => new { s.GrandTotal, s.DiscountTotal })
                .ToListAsync(cancellationToken);

            decimal currTotalSales = currentSalesInvoices.Sum(s => s.GrandTotal);
            decimal currTotalDiscounts = currentSalesInvoices.Sum(s => s.DiscountTotal);

            decimal currTotalPurchases = await purchaseQuery
                .Where(p => p.InvoiceDate >= startDate && p.InvoiceDate <= endDate)
                .SumAsync(p => (decimal?)p.GrandTotal, cancellationToken) ?? 0m;

            decimal currTotalReturns = await returnsQuery
                .Where(r => r.ReturnDate >= startDate && r.ReturnDate <= endDate)
                .SumAsync(r => (decimal?)r.NetRefundAmount, cancellationToken) ?? 0m;

            decimal currNetSales = Math.Max(0m, currTotalSales - currTotalReturns);

            // Current Estimated Cost of Goods Sold
            var currentItemsCost = await salesItemsQuery
                .Where(i => i.SalesInvoice.InvoiceDate >= startDate && i.SalesInvoice.InvoiceDate <= endDate)
                .SumAsync(i => (decimal?)(i.ConvertedQuantity * i.Product.AveragePurchaseCost), cancellationToken) ?? 0m;

            decimal currEstimatedProfit = Math.Max(0m, currNetSales - currentItemsCost);

            // Previous Period Aggregations
            var prevSalesInvoices = await salesQuery
                .Where(s => s.InvoiceDate >= prevStartDate && s.InvoiceDate <= prevEndDate)
                .Select(s => new { s.GrandTotal, s.DiscountTotal })
                .ToListAsync(cancellationToken);

            decimal prevTotalSales = prevSalesInvoices.Sum(s => s.GrandTotal);
            decimal prevTotalDiscounts = prevSalesInvoices.Sum(s => s.DiscountTotal);

            decimal prevTotalPurchases = await purchaseQuery
                .Where(p => p.InvoiceDate >= prevStartDate && p.InvoiceDate <= prevEndDate)
                .SumAsync(p => (decimal?)p.GrandTotal, cancellationToken) ?? 0m;

            decimal prevTotalReturns = await returnsQuery
                .Where(r => r.ReturnDate >= prevStartDate && r.ReturnDate <= prevEndDate)
                .SumAsync(r => (decimal?)r.NetRefundAmount, cancellationToken) ?? 0m;

            decimal prevNetSales = Math.Max(0m, prevTotalSales - prevTotalReturns);

            var prevItemsCost = await salesItemsQuery
                .Where(i => i.SalesInvoice.InvoiceDate >= prevStartDate && i.SalesInvoice.InvoiceDate <= prevEndDate)
                .SumAsync(i => (decimal?)(i.ConvertedQuantity * i.Product.AveragePurchaseCost), cancellationToken) ?? 0m;

            decimal prevEstimatedProfit = Math.Max(0m, prevNetSales - prevItemsCost);

            // Outstanding Receivables & Payables (Cumulative snapshots)
            decimal customerReceivables = await _context.CustomerLedgers.AsNoTracking()
                .Where(l => !l.Customer.IsDeleted)
                .SumAsync(l => (decimal?)(l.DebitAmount - l.CreditAmount), cancellationToken) ?? 0m;

            decimal supplierPayables = await _context.CompanyLedgers.AsNoTracking()
                .Where(l => !l.Company.IsDeleted)
                .SumAsync(l => (decimal?)(l.CreditAmount - l.DebitAmount), cancellationToken) ?? 0m;

            return new AnalyticsKpiSummaryDto
            {
                TotalSales = BuildMetric("Total Sales", currTotalSales, prevTotalSales),
                TotalPurchases = BuildMetric("Total Purchases", currTotalPurchases, prevTotalPurchases),
                EstimatedGrossProfit = BuildMetric("Estimated Gross Profit", currEstimatedProfit, prevEstimatedProfit),
                TotalDiscounts = BuildMetric("Total Discounts", currTotalDiscounts, prevTotalDiscounts),
                TotalReturns = BuildMetric("Total Returns", currTotalReturns, prevTotalReturns),
                NetSales = BuildMetric("Net Sales", currNetSales, prevNetSales),
                CustomerReceivables = BuildMetric("Outstanding Customer Receivables", Math.Max(0m, customerReceivables), Math.Max(0m, customerReceivables)),
                CompanyPayables = BuildMetric("Outstanding Company Payables", Math.Max(0m, supplierPayables), Math.Max(0m, supplierPayables))
            };
        }

        public async Task<List<SalesTrendItemDto>> GetSalesTrendAsync(AnalyticsFilterDto filter, string interval = "Daily", CancellationToken cancellationToken = default)
        {
            var (startDate, endDate, _, _) = ResolveDates(filter);
            var query = _context.SalesInvoices.AsNoTracking().Where(s => !s.IsDeleted && s.InvoiceDate >= startDate && s.InvoiceDate <= endDate);

            if (filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0)
                query = query.Where(s => s.WarehouseID == filter.WarehouseID.Value);
            if (filter.CustomerID.HasValue && filter.CustomerID.Value > 0)
                query = query.Where(s => s.CustomerID == filter.CustomerID.Value);

            var rawInvoices = await query
                .Select(s => new { s.InvoiceDate, s.GrandTotal })
                .ToListAsync(cancellationToken);

            var result = new List<SalesTrendItemDto>();
            string mode = (interval ?? "Daily").ToLowerInvariant();

            if (mode == "monthly")
            {
                var grouped = rawInvoices
                    .GroupBy(s => new { s.InvoiceDate.Year, s.InvoiceDate.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

                foreach (var g in grouped)
                {
                    string label = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy");
                    result.Add(new SalesTrendItemDto
                    {
                        Label = label,
                        SalesAmount = g.Sum(x => x.GrandTotal),
                        InvoiceCount = g.Count()
                    });
                }
            }
            else if (mode == "weekly")
            {
                var grouped = rawInvoices
                    .GroupBy(s => System.Globalization.ISOWeek.GetWeekOfYear(s.InvoiceDate))
                    .OrderBy(g => g.Key);

                foreach (var g in grouped)
                {
                    result.Add(new SalesTrendItemDto
                    {
                        Label = $"Week {g.Key}",
                        SalesAmount = g.Sum(x => x.GrandTotal),
                        InvoiceCount = g.Count()
                    });
                }
            }
            else
            {
                // Daily (Default)
                var grouped = rawInvoices
                    .GroupBy(s => s.InvoiceDate.Date)
                    .OrderBy(g => g.Key);

                foreach (var g in grouped)
                {
                    result.Add(new SalesTrendItemDto
                    {
                        Label = g.Key.ToString("dd MMM"),
                        SalesAmount = g.Sum(x => x.GrandTotal),
                        InvoiceCount = g.Count()
                    });
                }
            }

            return result;
        }

        public async Task<List<TopProductDto>> GetTopProductsAsync(AnalyticsFilterDto filter, string sortBy = "Revenue", int topCount = 10, CancellationToken cancellationToken = default)
        {
            var (startDate, endDate, _, _) = ResolveDates(filter);
            var query = _context.SalesInvoiceItems.AsNoTracking()
                .Include(i => i.Product).ThenInclude(p => p.Category)
                .Where(i => !i.IsDeleted && !i.SalesInvoice.IsDeleted && i.SalesInvoice.InvoiceDate >= startDate && i.SalesInvoice.InvoiceDate <= endDate);

            if (filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0)
                query = query.Where(i => i.SalesInvoice.WarehouseID == filter.WarehouseID.Value);
            if (filter.CustomerID.HasValue && filter.CustomerID.Value > 0)
                query = query.Where(i => i.SalesInvoice.CustomerID == filter.CustomerID.Value);
            if (filter.CategoryID.HasValue && filter.CategoryID.Value > 0)
                query = query.Where(i => i.Product.CategoryID == filter.CategoryID.Value);

            var grouped = await query
                .GroupBy(i => new { i.ProductID, i.Product.ProductName, CategoryName = i.Product.Category.Name, i.Product.AveragePurchaseCost })
                .Select(g => new
                {
                    g.Key.ProductID,
                    g.Key.ProductName,
                    g.Key.CategoryName,
                    QuantitySold = g.Sum(i => i.Quantity),
                    ConvertedQty = g.Sum(i => i.ConvertedQuantity),
                    Revenue = g.Sum(i => (i.Quantity * i.UnitPrice) - i.DiscountAmount),
                    Cost = g.Sum(i => i.ConvertedQuantity * g.Key.AveragePurchaseCost)
                })
                .ToListAsync(cancellationToken);

            bool isQuantitySort = string.Equals(sortBy, "Quantity", StringComparison.OrdinalIgnoreCase);

            var sorted = isQuantitySort
                ? grouped.OrderByDescending(p => p.QuantitySold).ThenByDescending(p => p.Revenue)
                : grouped.OrderByDescending(p => p.Revenue).ThenByDescending(p => p.QuantitySold);

            return sorted.Take(topCount).Select(p => new TopProductDto
            {
                ProductID = p.ProductID,
                ProductName = p.ProductName,
                CategoryName = p.CategoryName,
                QuantitySold = p.QuantitySold,
                Revenue = p.Revenue,
                EstimatedProfit = Math.Max(0m, p.Revenue - p.Cost)
            }).ToList();
        }

        public async Task<List<TopCustomerDto>> GetTopCustomersAsync(AnalyticsFilterDto filter, string sortBy = "Revenue", int topCount = 10, CancellationToken cancellationToken = default)
        {
            var (startDate, endDate, _, _) = ResolveDates(filter);
            var query = _context.SalesInvoices.AsNoTracking()
                .Include(s => s.Customer)
                .Where(s => !s.IsDeleted && s.InvoiceDate >= startDate && s.InvoiceDate <= endDate);

            if (filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0)
                query = query.Where(s => s.WarehouseID == filter.WarehouseID.Value);
            if (filter.CustomerID.HasValue && filter.CustomerID.Value > 0)
                query = query.Where(s => s.CustomerID == filter.CustomerID.Value);

            var grouped = await query
                .GroupBy(s => new { s.CustomerID, s.Customer.ShopName })
                .Select(g => new
                {
                    g.Key.CustomerID,
                    CustomerName = g.Key.ShopName,
                    Revenue = g.Sum(s => s.GrandTotal),
                    InvoiceCount = g.Count()
                })
                .OrderByDescending(c => c.Revenue)
                .Take(topCount)
                .ToListAsync(cancellationToken);

            var customerIds = grouped.Select(c => c.CustomerID).ToList();
            var balances = await _context.CustomerLedgers.AsNoTracking()
                .Where(l => customerIds.Contains(l.CustomerID))
                .GroupBy(l => l.CustomerID)
                .Select(g => new { CustomerID = g.Key, Balance = g.Sum(l => l.DebitAmount - l.CreditAmount) })
                .ToDictionaryAsync(x => x.CustomerID, x => x.Balance, cancellationToken);

            return grouped.Select(c => new TopCustomerDto
            {
                CustomerID = c.CustomerID,
                CustomerName = c.CustomerName,
                Revenue = c.Revenue,
                InvoiceCount = c.InvoiceCount,
                OutstandingBalance = Math.Max(0m, balances.ContainsKey(c.CustomerID) ? balances[c.CustomerID] : 0m)
            }).ToList();
        }

        public async Task<List<CategorySalesDto>> GetCategorySalesAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var (startDate, endDate, _, _) = ResolveDates(filter);
            var query = _context.SalesInvoiceItems.AsNoTracking()
                .Include(i => i.Product).ThenInclude(p => p.Category)
                .Where(i => !i.IsDeleted && !i.SalesInvoice.IsDeleted && i.SalesInvoice.InvoiceDate >= startDate && i.SalesInvoice.InvoiceDate <= endDate);

            if (filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0)
                query = query.Where(i => i.SalesInvoice.WarehouseID == filter.WarehouseID.Value);
            if (filter.CustomerID.HasValue && filter.CustomerID.Value > 0)
                query = query.Where(i => i.SalesInvoice.CustomerID == filter.CustomerID.Value);
            if (filter.CategoryID.HasValue && filter.CategoryID.Value > 0)
                query = query.Where(i => i.Product.CategoryID == filter.CategoryID.Value);

            var grouped = await query
                .GroupBy(i => i.Product.Category.Name)
                .Select(g => new
                {
                    CategoryName = g.Key,
                    SalesAmount = g.Sum(i => (i.Quantity * i.UnitPrice) - i.DiscountAmount)
                })
                .OrderByDescending(c => c.SalesAmount)
                .ToListAsync(cancellationToken);

            decimal totalSales = grouped.Sum(c => c.SalesAmount);
            return grouped.Select(c => new CategorySalesDto
            {
                CategoryName = c.CategoryName,
                SalesAmount = c.SalesAmount,
                Percentage = totalSales > 0 ? Math.Round((c.SalesAmount / totalSales) * 100m, 1) : 0m
            }).ToList();
        }

        private KpiMetricDto BuildMetric(string title, decimal current, decimal previous)
        {
            decimal change = 0m;
            if (previous > 0)
            {
                change = Math.Round(((current - previous) / previous) * 100m, 1);
            }
            else if (current > 0)
            {
                change = 100m;
            }

            return new KpiMetricDto
            {
                Title = title,
                CurrentValue = current,
                PreviousValue = previous,
                PercentageChange = change
            };
        }

        private (DateTime startDate, DateTime endDate, DateTime prevStartDate, DateTime prevEndDate) ResolveDates(AnalyticsFilterDto filter)
        {
            DateTime now = DateTime.UtcNow;
            DateTime startDate;
            DateTime endDate = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59, DateTimeKind.Utc);

            switch ((filter.Preset ?? "ThisMonth").ToLowerInvariant())
            {
                case "today":
                    startDate = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
                    break;
                case "last7days":
                    startDate = endDate.AddDays(-7).Date;
                    break;
                case "last30days":
                    startDate = endDate.AddDays(-30).Date;
                    break;
                case "lastmonth":
                    var lastMonth = now.AddMonths(-1);
                    startDate = new DateTime(lastMonth.Year, lastMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    endDate = new DateTime(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month), 23, 59, 59, DateTimeKind.Utc);
                    break;
                case "thisyear":
                    startDate = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    break;
                case "custom":
                    startDate = filter.StartDate ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    endDate = filter.EndDate.HasValue 
                        ? new DateTime(filter.EndDate.Value.Year, filter.EndDate.Value.Month, filter.EndDate.Value.Day, 23, 59, 59, DateTimeKind.Utc)
                        : endDate;
                    break;
                case "thismonth":
                default:
                    startDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    break;
            }

            TimeSpan duration = endDate - startDate;
            DateTime prevEndDate = startDate.AddSeconds(-1);
            DateTime prevStartDate = prevEndDate - duration;

            return (startDate, endDate, prevStartDate, prevEndDate);
        }
    }
}
