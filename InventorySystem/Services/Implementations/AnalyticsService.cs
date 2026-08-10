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

        // ===== WAVE 3 SERVICE METHODS =====

        public async Task<PaymentAnalyticsDto> GetPaymentAnalyticsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var (startDate, endDate, _, _) = ResolveDates(filter);

            var custPayments = await _context.CustomerPayments.AsNoTracking()
                .Where(p => !p.IsDeleted && p.PaymentDate >= startDate && p.PaymentDate <= endDate)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            var compPayments = await _context.CompanyPayments.AsNoTracking()
                .Where(p => !p.IsDeleted && p.PaymentDate >= startDate && p.PaymentDate <= endDate)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            var customerReceivables = await _context.CustomerLedgers.AsNoTracking()
                .Where(l => l.Customer != null && !l.Customer.IsDeleted)
                .SumAsync(l => (decimal?)(l.DebitAmount - l.CreditAmount), cancellationToken) ?? 0m;

            var companyPayables = await _context.CompanyLedgers.AsNoTracking()
                .Where(l => l.Company != null && !l.Company.IsDeleted)
                .SumAsync(l => (decimal?)(l.CreditAmount - l.DebitAmount), cancellationToken) ?? 0m;

            var salesQuery = _context.SalesInvoices.AsNoTracking()
                .Where(s => !s.IsDeleted && s.InvoiceDate >= startDate && s.InvoiceDate <= endDate);

            if (filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0)
                salesQuery = salesQuery.Where(s => s.WarehouseID == filter.WarehouseID.Value);
            if (filter.CustomerID.HasValue && filter.CustomerID.Value > 0)
                salesQuery = salesQuery.Where(s => s.CustomerID == filter.CustomerID.Value);

            var statusGroups = await salesQuery
                .GroupBy(s => s.PaymentStatus ?? "UNPAID")
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(s => s.GrandTotal)
                })
                .ToListAsync(cancellationToken);

            decimal grandTotalAll = statusGroups.Sum(g => g.TotalAmount);

            var breakdowns = statusGroups.Select(g => new InvoiceStatusBreakdownDto
            {
                Status = g.Status,
                Count = g.Count,
                TotalAmount = g.TotalAmount,
                Percentage = grandTotalAll > 0 ? Math.Round((g.TotalAmount / grandTotalAll) * 100m, 1) : 0m
            }).ToList();

            return new PaymentAnalyticsDto
            {
                CustomerPaymentsCollected = custPayments,
                CompanyPaymentsMade = compPayments,
                TotalOutstandingReceivables = Math.Max(0m, customerReceivables),
                TotalOutstandingPayables = Math.Max(0m, companyPayables),
                InvoiceStatuses = breakdowns
            };
        }

        public async Task<InventoryInsightsDto> GetInventoryInsightsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var stockQuery = _context.InventoryStocks.AsNoTracking()
                .Include(s => s.Product).ThenInclude(p => p.Category)
                .Where(s => !s.Product.IsDeleted);

            if (filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0)
                stockQuery = stockQuery.Where(s => s.WarehouseID == filter.WarehouseID.Value);
            if (filter.CategoryID.HasValue && filter.CategoryID.Value > 0)
                stockQuery = stockQuery.Where(s => s.Product.CategoryID == filter.CategoryID.Value);

            var stocks = await stockQuery.ToListAsync(cancellationToken);

            var productStockGrouped = stocks
                .GroupBy(s => new { s.ProductID, s.Product.ProductName, s.Product.ReorderLevel, s.Product.AveragePurchaseCost, CategoryName = s.Product.Category != null ? s.Product.Category.Name : "Uncategorized" })
                .Select(g => new
                {
                    g.Key.ProductID,
                    g.Key.ProductName,
                    g.Key.CategoryName,
                    TotalStock = g.Sum(s => s.Quantity),
                    ReorderLevel = g.Key.ReorderLevel,
                    StockValue = g.Sum(s => s.Quantity * g.Key.AveragePurchaseCost)
                })
                .ToList();

            decimal totalInventoryValue = productStockGrouped.Sum(p => Math.Max(0m, p.StockValue));
            int totalProducts = productStockGrouped.Count;
            int lowStockCount = productStockGrouped.Count(p => p.TotalStock > 0 && p.TotalStock <= p.ReorderLevel);
            int outOfStockCount = productStockGrouped.Count(p => p.TotalStock <= 0);

            var categoryGrouped = productStockGrouped
                .GroupBy(p => p.CategoryName)
                .Select(g => new CategoryInventoryValueDto
                {
                    CategoryName = g.Key,
                    TotalValue = g.Sum(p => Math.Max(0m, p.StockValue)),
                    ProductCount = g.Count(),
                    Percentage = totalInventoryValue > 0 ? Math.Round((g.Sum(p => Math.Max(0m, p.StockValue)) / totalInventoryValue) * 100m, 1) : 0m
                })
                .OrderByDescending(c => c.TotalValue)
                .ToList();

            return new InventoryInsightsDto
            {
                TotalInventoryValue = totalInventoryValue,
                TotalProductCount = totalProducts,
                LowStockProductCount = lowStockCount,
                OutOfStockProductCount = outOfStockCount,
                ValueByCategory = categoryGrouped
            };
        }

        public async Task<List<StockRiskItemDto>> GetStockRiskItemsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var stockQuery = _context.InventoryStocks.AsNoTracking()
                .Include(s => s.Product).ThenInclude(p => p.Category)
                .Include(s => s.Product).ThenInclude(p => p.BaseUnit)
                .Where(s => !s.Product.IsDeleted);

            if (filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0)
                stockQuery = stockQuery.Where(s => s.WarehouseID == filter.WarehouseID.Value);
            if (filter.CategoryID.HasValue && filter.CategoryID.Value > 0)
                stockQuery = stockQuery.Where(s => s.Product.CategoryID == filter.CategoryID.Value);

            var stocks = await stockQuery.ToListAsync(cancellationToken);

            var riskItems = stocks
                .GroupBy(s => new { 
                    s.ProductID, 
                    s.Product.ProductName, 
                    s.Product.ReorderLevel, 
                    CategoryName = s.Product.Category != null ? s.Product.Category.Name : "Uncategorized",
                    BaseUnitName = s.Product.BaseUnit != null ? s.Product.BaseUnit.UnitName : "Units"
                })
                .Select(g => new
                {
                    g.Key.ProductID,
                    g.Key.ProductName,
                    g.Key.CategoryName,
                    CurrentStock = g.Sum(s => s.Quantity),
                    ReorderLevel = g.Key.ReorderLevel,
                    BaseUnit = g.Key.BaseUnitName
                })
                .Where(p => p.CurrentStock <= p.ReorderLevel)
                .OrderBy(p => p.CurrentStock)
                .Select(p => new StockRiskItemDto
                {
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    CategoryName = p.CategoryName,
                    CurrentStock = p.CurrentStock,
                    ReorderLevel = p.ReorderLevel,
                    BaseUnit = p.BaseUnit,
                    RiskLevel = p.CurrentStock <= 0 ? "OUT_OF_STOCK" : "LOW_STOCK"
                })
                .Take(15)
                .ToList();

            return riskItems;
        }

        public async Task<InventoryMovementDto> GetInventoryMovementAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var (startDate, endDate, _, _) = ResolveDates(filter);

            var txQuery = _context.InventoryTransactions.AsNoTracking()
                .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate);

            if (filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0)
                txQuery = txQuery.Where(t => t.WarehouseID == filter.WarehouseID.Value);

            var grouped = await txQuery
                .GroupBy(t => t.TransactionType)
                .Select(g => new
                {
                    Type = g.Key,
                    Quantity = g.Sum(t => Math.Abs(t.Quantity))
                })
                .ToListAsync(cancellationToken);

            decimal purchased = grouped.Where(g => g.Type.Contains("PURCHASE", StringComparison.OrdinalIgnoreCase)).Sum(g => g.Quantity);
            decimal sold = grouped.Where(g => g.Type.Contains("SALE", StringComparison.OrdinalIgnoreCase)).Sum(g => g.Quantity);
            decimal returned = grouped.Where(g => g.Type.Contains("RETURN", StringComparison.OrdinalIgnoreCase)).Sum(g => g.Quantity);
            decimal adjusted = grouped.Where(g => g.Type.Contains("ADJUSTMENT", StringComparison.OrdinalIgnoreCase)).Sum(g => g.Quantity);

            return new InventoryMovementDto
            {
                PurchasedQuantity = purchased,
                SoldQuantity = sold,
                SalesReturnQuantity = returned,
                AdjustmentQuantity = adjusted
            };
        }

        public async Task<PromotionPerformanceDto> GetPromotionPerformanceAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var (startDate, endDate, _, _) = ResolveDates(filter);

            var salesQuery = _context.SalesInvoices.AsNoTracking()
                .Where(s => !s.IsDeleted && s.InvoiceDate >= startDate && s.InvoiceDate <= endDate);

            if (filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0)
                salesQuery = salesQuery.Where(s => s.WarehouseID == filter.WarehouseID.Value);
            if (filter.CustomerID.HasValue && filter.CustomerID.Value > 0)
                salesQuery = salesQuery.Where(s => s.CustomerID == filter.CustomerID.Value);

            var invoices = await salesQuery
                .Select(s => new { s.InvoiceID, s.DiscountTotal, s.GrandTotal })
                .ToListAsync(cancellationToken);

            decimal totalDiscount = invoices.Sum(i => i.DiscountTotal);
            int invoicesWithDiscount = invoices.Count(i => i.DiscountTotal > 0);

            var promoInvoices = await _context.InvoicePromotions.AsNoTracking()
                .Where(p => !p.SalesInvoice.IsDeleted && p.SalesInvoice.InvoiceDate >= startDate && p.SalesInvoice.InvoiceDate <= endDate)
                .SumAsync(p => (decimal?)p.DiscountAmount, cancellationToken) ?? 0m;

            return new PromotionPerformanceDto
            {
                TotalDiscountAmount = totalDiscount,
                PromoInvoicesCount = invoicesWithDiscount,
                RegularDiscountsCount = invoicesWithDiscount,
                TotalInvoicePromotionsAmount = promoInvoices,
                TotalRegularDiscountsAmount = Math.Max(0m, totalDiscount - promoInvoices)
            };
        }

        public async Task<List<TopCompanyDto>> GetTopCompaniesAsync(AnalyticsFilterDto filter, int topCount = 5, CancellationToken cancellationToken = default)
        {
            var (startDate, endDate, _, _) = ResolveDates(filter);

            var query = _context.PurchaseInvoices.AsNoTracking()
                .Include(p => p.Company)
                .Where(p => !p.IsDeleted && p.InvoiceDate >= startDate && p.InvoiceDate <= endDate);

            if (filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0)
                query = query.Where(p => p.WarehouseID == filter.WarehouseID.Value);

            var grouped = await query
                .GroupBy(p => new { p.CompanyID, p.Company.CompanyName })
                .Select(g => new
                {
                    g.Key.CompanyID,
                    CompanyName = g.Key.CompanyName,
                    TotalPurchases = g.Sum(p => p.GrandTotal),
                    InvoiceCount = g.Count()
                })
                .OrderByDescending(c => c.TotalPurchases)
                .Take(topCount)
                .ToListAsync(cancellationToken);

            var companyIds = grouped.Select(c => c.CompanyID).ToList();
            var payables = await _context.CompanyLedgers.AsNoTracking()
                .Where(l => companyIds.Contains(l.CompanyID))
                .GroupBy(l => l.CompanyID)
                .Select(g => new { CompanyID = g.Key, Balance = g.Sum(l => l.CreditAmount - l.DebitAmount) })
                .ToDictionaryAsync(x => x.CompanyID, x => x.Balance, cancellationToken);

            return grouped.Select(c => new TopCompanyDto
            {
                CompanyID = c.CompanyID,
                CompanyName = c.CompanyName,
                TotalPurchases = c.TotalPurchases,
                InvoiceCount = c.InvoiceCount,
                OutstandingPayable = Math.Max(0m, payables.ContainsKey(c.CompanyID) ? payables[c.CompanyID] : 0m)
            }).ToList();
        }

        public async Task<List<BusinessInsightDto>> GetBusinessInsightsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var insights = new List<BusinessInsightDto>();
            var kpi = await GetKpiSummaryAsync(filter, cancellationToken);
            var inv = await GetInventoryInsightsAsync(filter, cancellationToken);

            // 1. Stock Out Emergency
            if (inv.OutOfStockProductCount > 0)
            {
                insights.Add(new BusinessInsightDto
                {
                    Type = "DANGER",
                    Title = "Critical Stockout Warning",
                    Message = $"{inv.OutOfStockProductCount} product(s) are currently completely out of stock. Immediate reorder recommended to prevent revenue loss.",
                    Icon = "bi-exclamation-octagon-fill"
                });
            }

            // 2. Low Stock Warning
            if (inv.LowStockProductCount > 0)
            {
                insights.Add(new BusinessInsightDto
                {
                    Type = "WARNING",
                    Title = "Reorder Level Alert",
                    Message = $"{inv.LowStockProductCount} product(s) have fallen below their minimum reorder levels. Check the stock risk table.",
                    Icon = "bi-triangle-fill"
                });
            }

            // 3. Customer Receivables Collection
            if (kpi.CustomerReceivables.CurrentValue > 0)
            {
                insights.Add(new BusinessInsightDto
                {
                    Type = "INFO",
                    Title = "Outstanding Receivables",
                    Message = $"Total customer receivables balance is PKR {kpi.CustomerReceivables.CurrentValue:N2}. Prioritize collection from high-balance accounts.",
                    Icon = "bi-person-badge"
                });
            }

            // 4. Sales Growth Trend
            if (kpi.TotalSales.PercentageChange > 10m)
            {
                insights.Add(new BusinessInsightDto
                {
                    Type = "SUCCESS",
                    Title = "Sales Surge",
                    Message = $"Gross sales increased by {kpi.TotalSales.PercentageChange:0.0}% compared to the previous period!",
                    Icon = "bi-graph-up-arrow"
                });
            }
            else if (kpi.TotalSales.PercentageChange < -10m)
            {
                insights.Add(new BusinessInsightDto
                {
                    Type = "WARNING",
                    Title = "Sales Decline Alert",
                    Message = $"Gross sales dropped by {Math.Abs(kpi.TotalSales.PercentageChange):0.0}% compared to the previous period.",
                    Icon = "bi-graph-down-arrow"
                });
            }

            // 5. Returns Rate
            if (kpi.TotalSales.CurrentValue > 0)
            {
                decimal returnRate = (kpi.TotalReturns.CurrentValue / kpi.TotalSales.CurrentValue) * 100m;
                if (returnRate > 5m)
                {
                    insights.Add(new BusinessInsightDto
                    {
                        Type = "WARNING",
                        Title = "High Return Rate",
                        Message = $"Sales returns equal {returnRate:0.1}% of gross sales for this period. Review product quality and customer feedback.",
                        Icon = "bi-arrow-return-left"
                    });
                }
            }

            if (!insights.Any())
            {
                insights.Add(new BusinessInsightDto
                {
                    Type = "SUCCESS",
                    Title = "Stable System Performance",
                    Message = "All key performance indicators and inventory levels are within normal operational parameters.",
                    Icon = "bi-check-circle-fill"
                });
            }

            return insights;
        }
    }
}
