using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Analytics;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IAnalyticsRepository _repository;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(IAnalyticsRepository repository, ILogger<AnalyticsService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<OverviewAnalyticsDto> GetOverviewAsync(AnalyticsFilterDto filter, string interval = "Daily", CancellationToken cancellationToken = default)
        {
            var kpi = await GetKpiSummaryAsync(filter, cancellationToken);
            var inventory = await GetInventoryInsightsAsync(filter, cancellationToken);
            var trend = await GetSalesTrendAsync(filter, interval, cancellationToken);
            var insights = BuildInsights(kpi, inventory);
            return new OverviewAnalyticsDto
            {
                Kpi = kpi,
                SalesTrend = trend,
                Insights = insights,
                Inventory = inventory
            };
        }

        public async Task<AnalyticsKpiSummaryDto> GetKpiSummaryAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var (start, end, prevStart, prevEnd) = AnalyticsFilterHelper.ResolveDates(filter);

            var curr = await BuildPeriodFinancialsAsync(filter, start, end, cancellationToken);
            var prev = await BuildPeriodFinancialsAsync(filter, prevStart, prevEnd, cancellationToken);

            decimal receivables = await _repository.GetCustomerReceivablesAsync(
                AnalyticsFilterHelper.HasCustomer(filter) ? filter.CustomerID : null, cancellationToken);
            decimal payables = await _repository.GetCompanyPayablesAsync(cancellationToken);

            return new AnalyticsKpiSummaryDto
            {
                TotalSales = BuildMetric("Total Sales", curr.Sales, prev.Sales),
                TotalPurchases = BuildMetric("Total Purchases", curr.Purchases, prev.Purchases),
                EstimatedGrossProfit = BuildMetric("Estimated Gross Profit (Avg Cost)", curr.Profit, prev.Profit),
                TotalDiscounts = BuildMetric("Total Discounts", curr.Discounts, prev.Discounts),
                TotalReturns = BuildMetric("Total Returns", curr.Returns, prev.Returns),
                NetSales = BuildMetric("Net Sales", curr.NetSales, prev.NetSales),
                CustomerReceivables = SnapshotMetric("Outstanding Customer Receivables", Math.Max(0m, receivables)),
                CompanyPayables = SnapshotMetric("Outstanding Company Payables", Math.Max(0m, payables))
            };
        }

        public async Task<List<SalesTrendItemDto>> GetSalesTrendAsync(AnalyticsFilterDto filter, string interval = "Daily", CancellationToken cancellationToken = default)
        {
            var (start, end, _, _) = AnalyticsFilterHelper.ResolveDates(filter);
            var invoices = await _repository.GetSalesInvoiceRowsAsync(filter, start, end, cancellationToken);
            var items = AnalyticsFilterHelper.HasCategory(filter)
                ? await _repository.GetSalesItemRowsAsync(filter, start, end, cancellationToken)
                : new List<AnalyticsSalesItemRow>();
            var returns = await _repository.GetReturnRowsAsync(filter, start, end, cancellationToken);
            var purchases = AnalyticsFilterHelper.HasCategory(filter)
                ? (await _repository.GetPurchaseItemRowsAsync(filter, start, end, cancellationToken))
                    .Select(p => new { p.InvoiceDate, Amount = p.TotalCost })
                    .ToList()
                : (await _repository.GetPurchaseInvoiceRowsAsync(filter, start, end, cancellationToken))
                    .Select(p => new { p.InvoiceDate, Amount = p.GrandTotal })
                    .ToList();

            var salesByInvoice = AnalyticsFilterHelper.HasCategory(filter)
                ? items.GroupBy(i => i.InvoiceID).ToDictionary(g => g.Key, g => g.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount)))
                : invoices.ToDictionary(i => i.InvoiceID, i => i.GrandTotal);

            var buckets = new Dictionary<(DateTime Start, DateTime End, string Label), SalesTrendItemDto>();

            foreach (var inv in invoices)
            {
                var (bStart, bEnd, label) = AnalyticsFilterHelper.Bucket(inv.InvoiceDate, interval);
                var key = (bStart, bEnd, label);
                if (!buckets.TryGetValue(key, out var dto))
                {
                    dto = new SalesTrendItemDto { Label = label, BucketStart = bStart, BucketEnd = bEnd };
                    buckets[key] = dto;
                }

                decimal amount = salesByInvoice.TryGetValue(inv.InvoiceID, out var a) ? a : 0m;
                dto.SalesAmount += amount;
                dto.InvoiceCount += 1;
            }

            foreach (var ret in returns)
            {
                var (bStart, bEnd, label) = AnalyticsFilterHelper.Bucket(ret.ReturnDate, interval);
                var key = (bStart, bEnd, label);
                if (!buckets.TryGetValue(key, out var dto))
                {
                    dto = new SalesTrendItemDto { Label = label, BucketStart = bStart, BucketEnd = bEnd };
                    buckets[key] = dto;
                }
                dto.ReturnsAmount += ret.LineRefundAmount;
            }

            foreach (var p in purchases)
            {
                var (bStart, bEnd, label) = AnalyticsFilterHelper.Bucket(p.InvoiceDate, interval);
                var key = (bStart, bEnd, label);
                if (!buckets.TryGetValue(key, out var dto))
                {
                    dto = new SalesTrendItemDto { Label = label, BucketStart = bStart, BucketEnd = bEnd };
                    buckets[key] = dto;
                }
                dto.PurchaseAmount += p.Amount;
            }

            foreach (var dto in buckets.Values)
                dto.NetSalesAmount = dto.SalesAmount - dto.ReturnsAmount;

            return buckets.Values.OrderBy(b => b.BucketStart).ToList();
        }

        public async Task<List<TopProductDto>> GetTopProductsAsync(AnalyticsFilterDto filter, string sortBy = "Revenue", int topCount = 10, CancellationToken cancellationToken = default)
        {
            var (start, end, _, _) = AnalyticsFilterHelper.ResolveDates(filter);
            var items = await _repository.GetSalesItemRowsAsync(filter, start, end, cancellationToken);

            var grouped = items
                .Where(i => i.ProductID.HasValue)
                .GroupBy(i => new { i.ProductID, i.ProductName, i.CategoryName })
                .Select(g => new TopProductDto
                {
                    ProductID = g.Key.ProductID ?? 0,
                    ProductName = g.Key.ProductName,
                    CategoryName = g.Key.CategoryName,
                    QuantitySold = g.Sum(i => i.ConvertedQuantity),
                    Revenue = g.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount)),
                    EstimatedProfit = g.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount) - (i.ConvertedQuantity * i.AveragePurchaseCost))
                });

            bool byQty = string.Equals(sortBy, "Quantity", StringComparison.OrdinalIgnoreCase);
            var sorted = byQty
                ? grouped.OrderByDescending(p => p.QuantitySold).ThenByDescending(p => p.Revenue)
                : grouped.OrderByDescending(p => p.Revenue).ThenByDescending(p => p.QuantitySold);

            return sorted.Take(topCount).ToList();
        }

        public async Task<List<TopCustomerDto>> GetTopCustomersAsync(AnalyticsFilterDto filter, string sortBy = "Revenue", int topCount = 10, CancellationToken cancellationToken = default)
        {
            var page = await GetCustomerAnalyticsAsync(filter, sortBy, cancellationToken);
            return page.Customers.Take(topCount).Select(c => new TopCustomerDto
            {
                CustomerID = c.CustomerID,
                CustomerName = c.CustomerName,
                Revenue = c.Revenue,
                InvoiceCount = c.InvoiceCount,
                OutstandingBalance = c.Outstanding,
                ReturnsAmount = c.Returns,
                LastSaleDate = c.LastSaleDate
            }).ToList();
        }

        public async Task<List<CategorySalesDto>> GetCategorySalesAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var (start, end, _, _) = AnalyticsFilterHelper.ResolveDates(filter);
            var items = await _repository.GetSalesItemRowsAsync(filter, start, end, cancellationToken);
            var grouped = items
                .GroupBy(i => string.IsNullOrWhiteSpace(i.CategoryName) ? "Uncategorized" : i.CategoryName)
                .Select(g => new CategorySalesDto
                {
                    CategoryName = g.Key,
                    SalesAmount = g.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount))
                })
                .OrderByDescending(c => c.SalesAmount)
                .ToList();

            decimal total = grouped.Sum(c => c.SalesAmount);
            foreach (var c in grouped)
                c.Percentage = total > 0 ? Math.Round((c.SalesAmount / total) * 100m, 1) : 0m;
            return grouped;
        }

        public async Task<PaymentAnalyticsDto> GetPaymentAnalyticsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var (start, end, _, _) = AnalyticsFilterHelper.ResolveDates(filter);
            var invoices = await _repository.GetSalesInvoiceRowsAsync(filter, start, end, cancellationToken);
            var custPayments = await _repository.GetCustomerPaymentsAsync(filter, start, end, cancellationToken);
            var compPayments = await _repository.GetCompanyPaymentsAsync(filter, start, end, cancellationToken);
            var receivables = await _repository.GetCustomerReceivablesAsync(
                AnalyticsFilterHelper.HasCustomer(filter) ? filter.CustomerID : null, cancellationToken);
            var payables = await _repository.GetCompanyPayablesAsync(cancellationToken);

            var statuses = invoices
                .Select(i => new
                {
                    Status = AnalyticsFilterHelper.EffectivePaymentStatus(
                        i.PaymentStatus,
                        AnalyticsFilterHelper.InvoiceRemaining(i.GrandTotal, i.PaidAmount, i.ReturnedAmount)),
                    Amount = AnalyticsFilterHelper.HasCategory(filter) ? 0m : i.GrandTotal
                })
                .GroupBy(x => x.Status)
                .Select(g => new InvoiceStatusBreakdownDto
                {
                    Status = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .ToList();

            if (AnalyticsFilterHelper.HasCategory(filter))
            {
                var items = await _repository.GetSalesItemRowsAsync(filter, start, end, cancellationToken);
                var byInvoice = items.GroupBy(i => i.InvoiceID)
                    .ToDictionary(g => g.Key, g => g.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount)));
                statuses = invoices
                    .Select(i => new
                    {
                        Status = AnalyticsFilterHelper.EffectivePaymentStatus(
                            i.PaymentStatus,
                            AnalyticsFilterHelper.InvoiceRemaining(i.GrandTotal, i.PaidAmount, i.ReturnedAmount)),
                        Amount = byInvoice.TryGetValue(i.InvoiceID, out var a) ? a : 0m
                    })
                    .GroupBy(x => x.Status)
                    .Select(g => new InvoiceStatusBreakdownDto
                    {
                        Status = g.Key,
                        Count = g.Count(),
                        TotalAmount = g.Sum(x => x.Amount)
                    })
                    .ToList();
            }

            decimal grand = statuses.Sum(s => s.TotalAmount);
            foreach (var s in statuses)
                s.Percentage = grand > 0 ? Math.Round((s.TotalAmount / grand) * 100m, 1) : 0m;

            return new PaymentAnalyticsDto
            {
                CustomerPaymentsCollected = custPayments,
                CompanyPaymentsMade = compPayments,
                TotalOutstandingReceivables = Math.Max(0m, receivables),
                TotalOutstandingPayables = Math.Max(0m, payables),
                InvoiceStatuses = statuses
            };
        }

        public async Task<InventoryInsightsDto> GetInventoryInsightsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var stocks = await _repository.GetStockRowsAsync(filter, cancellationToken);
            var productStock = stocks
                .GroupBy(s => new { s.ProductID, s.ProductName, s.ReorderLevel, s.AveragePurchaseCost, s.CategoryName })
                .Select(g => new
                {
                    g.Key.CategoryName,
                    TotalStock = g.Sum(s => s.Quantity),
                    ReorderLevel = g.Key.ReorderLevel,
                    StockValue = g.Sum(s => s.Quantity * g.Key.AveragePurchaseCost)
                })
                .ToList();

            decimal totalValue = productStock.Sum(p => Math.Max(0m, p.StockValue));
            var categoryGrouped = productStock
                .GroupBy(p => p.CategoryName)
                .Select(g => new CategoryInventoryValueDto
                {
                    CategoryName = g.Key,
                    TotalValue = g.Sum(p => Math.Max(0m, p.StockValue)),
                    ProductCount = g.Count(),
                    Percentage = 0m
                })
                .OrderByDescending(c => c.TotalValue)
                .ToList();

            foreach (var c in categoryGrouped)
                c.Percentage = totalValue > 0 ? Math.Round((c.TotalValue / totalValue) * 100m, 1) : 0m;

            return new InventoryInsightsDto
            {
                TotalInventoryValue = totalValue,
                TotalProductCount = productStock.Count,
                LowStockProductCount = productStock.Count(p => p.TotalStock > 0 && p.TotalStock <= p.ReorderLevel),
                OutOfStockProductCount = productStock.Count(p => p.TotalStock <= 0),
                ValueByCategory = categoryGrouped
            };
        }

        public async Task<List<StockRiskItemDto>> GetStockRiskItemsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var stocks = await _repository.GetStockRowsAsync(filter, cancellationToken);
            return stocks
                .GroupBy(s => new { s.ProductID, s.ProductName, s.CategoryName, s.ReorderLevel, s.BaseUnit })
                .Select(g => new StockRiskItemDto
                {
                    ProductID = g.Key.ProductID,
                    ProductName = g.Key.ProductName,
                    CategoryName = g.Key.CategoryName,
                    CurrentStock = g.Sum(s => s.Quantity),
                    ReorderLevel = g.Key.ReorderLevel,
                    BaseUnit = g.Key.BaseUnit,
                    RiskLevel = g.Sum(s => s.Quantity) <= 0 ? "OUT_OF_STOCK" : "LOW_STOCK"
                })
                .Where(p => p.CurrentStock <= p.ReorderLevel)
                .OrderBy(p => p.CurrentStock)
                .Take(15)
                .ToList();
        }

        public async Task<InventoryMovementDto> GetInventoryMovementAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var (start, end, _, _) = AnalyticsFilterHelper.ResolveDates(filter);
            var grouped = await _repository.GetMovementRowsAsync(filter, start, end, cancellationToken);

            decimal SumType(string type) => grouped
                .Where(g => string.Equals(g.TransactionType, type, StringComparison.OrdinalIgnoreCase))
                .Sum(g => g.Quantity);

            return new InventoryMovementDto
            {
                PurchasedQuantity = SumType("PURCHASE"),
                SoldQuantity = SumType("SALE"),
                SalesReturnQuantity = SumType("RETURN"),
                AdjustmentQuantity = SumType("ADJUSTMENT")
            };
        }

        public async Task<PromotionPerformanceDto> GetPromotionPerformanceAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var (start, end, _, _) = AnalyticsFilterHelper.ResolveDates(filter);
            var invoices = await _repository.GetSalesInvoiceRowsAsync(filter, start, end, cancellationToken);
            decimal totalDiscount = AnalyticsFilterHelper.HasCategory(filter)
                ? (await _repository.GetSalesItemRowsAsync(filter, start, end, cancellationToken)).Sum(i => i.DiscountAmount)
                : invoices.Sum(i => i.DiscountTotal);
            int invoicesWithDiscount = invoices.Count(i => i.DiscountTotal > 0);
            decimal promo = await _repository.GetInvoicePromotionAmountAsync(filter, start, end, cancellationToken);

            return new PromotionPerformanceDto
            {
                TotalDiscountAmount = totalDiscount,
                PromoInvoicesCount = invoicesWithDiscount,
                RegularDiscountsCount = invoicesWithDiscount,
                TotalInvoicePromotionsAmount = promo,
                TotalRegularDiscountsAmount = Math.Max(0m, totalDiscount - promo)
            };
        }

        public async Task<List<TopCompanyDto>> GetTopCompaniesAsync(AnalyticsFilterDto filter, int topCount = 5, CancellationToken cancellationToken = default)
        {
            var (start, end, _, _) = AnalyticsFilterHelper.ResolveDates(filter);
            var purchases = await _repository.GetPurchaseInvoiceRowsAsync(filter, start, end, cancellationToken);
            var grouped = purchases
                .GroupBy(p => new { p.CompanyID, p.CompanyName })
                .Select(g => new TopCompanyDto
                {
                    CompanyID = g.Key.CompanyID,
                    CompanyName = g.Key.CompanyName,
                    TotalPurchases = g.Sum(p => p.GrandTotal),
                    InvoiceCount = g.Count()
                })
                .OrderByDescending(c => c.TotalPurchases)
                .Take(topCount)
                .ToList();

            var payables = await _repository.GetCompanyLedgerBalancesAsync(grouped.Select(c => c.CompanyID), cancellationToken);
            foreach (var c in grouped)
                c.OutstandingPayable = Math.Max(0m, payables.TryGetValue(c.CompanyID, out var b) ? b : 0m);
            return grouped;
        }

        public async Task<List<BusinessInsightDto>> GetBusinessInsightsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var kpi = await GetKpiSummaryAsync(filter, cancellationToken);
            var inv = await GetInventoryInsightsAsync(filter, cancellationToken);
            return BuildInsights(kpi, inv);
        }

        public async Task<SalesAnalyticsDto> GetSalesAnalyticsAsync(AnalyticsFilterDto filter, string interval = "Daily", CancellationToken cancellationToken = default)
        {
            var (start, end, _, _) = AnalyticsFilterHelper.ResolveDates(filter);
            var invoices = await _repository.GetSalesInvoiceRowsAsync(filter, start, end, cancellationToken);
            var items = await _repository.GetSalesItemRowsAsync(filter, start, end, cancellationToken);
            var returns = await _repository.GetReturnRowsAsync(filter, start, end, cancellationToken);
            var payments = await GetPaymentAnalyticsAsync(filter, cancellationToken);
            var trends = await GetSalesTrendAsync(filter, interval, cancellationToken);

            bool byCategory = AnalyticsFilterHelper.HasCategory(filter);
            decimal totalSales = byCategory
                ? items.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount))
                : invoices.Sum(i => i.GrandTotal);
            decimal discounts = byCategory ? items.Sum(i => i.DiscountAmount) : invoices.Sum(i => i.DiscountTotal);
            decimal returnsAmt = returns.Sum(r => r.LineRefundAmount);
            int invoiceCount = invoices.Count;

            var areaGroups = invoices
                .GroupBy(i => string.IsNullOrWhiteSpace(i.AreaName) ? "Unassigned" : i.AreaName)
                .Select(g => new NamedAmountDto { Name = g.Key, Amount = byCategory ? 0m : g.Sum(x => x.GrandTotal), Count = g.Count() })
                .OrderByDescending(a => a.Amount)
                .ToList();

            var subAreaGroups = invoices
                .GroupBy(i => string.IsNullOrWhiteSpace(i.SubAreaName) ? "Unassigned" : i.SubAreaName)
                .Select(g => new NamedAmountDto { Name = g.Key, Amount = byCategory ? 0m : g.Sum(x => x.GrandTotal), Count = g.Count() })
                .OrderByDescending(a => a.Amount)
                .ToList();

            if (byCategory)
            {
                var byInvoice = items.GroupBy(i => i.InvoiceID)
                    .ToDictionary(g => g.Key, g => g.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount)));
                foreach (var a in areaGroups)
                {
                    a.Amount = invoices.Where(i => (string.IsNullOrWhiteSpace(i.AreaName) ? "Unassigned" : i.AreaName) == a.Name)
                        .Sum(i => byInvoice.TryGetValue(i.InvoiceID, out var v) ? v : 0m);
                }
                foreach (var a in subAreaGroups)
                {
                    a.Amount = invoices.Where(i => (string.IsNullOrWhiteSpace(i.SubAreaName) ? "Unassigned" : i.SubAreaName) == a.Name)
                        .Sum(i => byInvoice.TryGetValue(i.InvoiceID, out var v) ? v : 0m);
                }
                areaGroups = areaGroups.OrderByDescending(a => a.Amount).ToList();
                subAreaGroups = subAreaGroups.OrderByDescending(a => a.Amount).ToList();
            }

            decimal areaTotal = areaGroups.Sum(a => a.Amount);
            foreach (var a in areaGroups)
                a.Percentage = areaTotal > 0 ? Math.Round((a.Amount / areaTotal) * 100m, 1) : 0m;
            decimal subTotal = subAreaGroups.Sum(a => a.Amount);
            foreach (var a in subAreaGroups)
                a.Percentage = subTotal > 0 ? Math.Round((a.Amount / subTotal) * 100m, 1) : 0m;

            var agingOrder = new[] { "Current", "1–30 days", "31–60 days", "61–90 days", "90+ days" };
            var aging = invoices
                .Select(i => new
                {
                    Bucket = AnalyticsFilterHelper.AgingBucket(i.InvoiceDate, DateTime.Today),
                    Remaining = AnalyticsFilterHelper.InvoiceRemaining(i.GrandTotal, i.PaidAmount, i.ReturnedAmount)
                })
                .Where(x => x.Remaining > 0.001m)
                .GroupBy(x => x.Bucket)
                .Select(g => new AgingBucketDto
                {
                    Bucket = g.Key,
                    InvoiceCount = g.Count(),
                    OutstandingAmount = g.Sum(x => x.Remaining)
                })
                .ToList();

            var agingFull = agingOrder.Select(name =>
                aging.FirstOrDefault(a => a.Bucket == name) ?? new AgingBucketDto { Bucket = name }).ToList();

            return new SalesAnalyticsDto
            {
                TotalSales = totalSales,
                NetSales = totalSales - returnsAmt,
                Returns = returnsAmt,
                AverageInvoiceValue = invoiceCount > 0 ? totalSales / invoiceCount : 0m,
                InvoiceCount = invoiceCount,
                DiscountRate = totalSales > 0 ? Math.Round((discounts / totalSales) * 100m, 1) : 0m,
                TotalDiscounts = discounts,
                Trends = trends,
                SalesByArea = areaGroups,
                SalesBySubArea = subAreaGroups,
                PaymentStatuses = payments.InvoiceStatuses,
                Aging = agingFull,
                Payments = payments
            };
        }

        public async Task<List<InvoiceDrilldownDto>> GetSalesDrilldownAsync(
            AnalyticsFilterDto filter, DateTime bucketStart, DateTime bucketEnd, CancellationToken cancellationToken = default)
        {
            var invoices = await _repository.GetSalesInvoiceRowsAsync(filter, bucketStart, bucketEnd, cancellationToken);
            return invoices
                .OrderBy(i => i.InvoiceDate)
                .Select(i => new InvoiceDrilldownDto
                {
                    InvoiceID = i.InvoiceID,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDate = i.InvoiceDate,
                    CustomerName = i.CustomerName,
                    GrandTotal = i.GrandTotal,
                    Outstanding = AnalyticsFilterHelper.InvoiceRemaining(i.GrandTotal, i.PaidAmount, i.ReturnedAmount),
                    PaymentStatus = AnalyticsFilterHelper.EffectivePaymentStatus(
                        i.PaymentStatus,
                        AnalyticsFilterHelper.InvoiceRemaining(i.GrandTotal, i.PaidAmount, i.ReturnedAmount))
                })
                .ToList();
        }

        public async Task<CustomerAnalyticsDto> GetCustomerAnalyticsAsync(AnalyticsFilterDto filter, string sortBy = "Revenue", CancellationToken cancellationToken = default)
        {
            var (start, end, _, _) = AnalyticsFilterHelper.ResolveDates(filter);
            var invoices = await _repository.GetSalesInvoiceRowsAsync(filter, start, end, cancellationToken);
            var items = AnalyticsFilterHelper.HasCategory(filter)
                ? await _repository.GetSalesItemRowsAsync(filter, start, end, cancellationToken)
                : new List<AnalyticsSalesItemRow>();
            var returns = await _repository.GetReturnRowsAsync(filter, start, end, cancellationToken);

            var revenueByInvoice = AnalyticsFilterHelper.HasCategory(filter)
                ? items.GroupBy(i => i.InvoiceID).ToDictionary(g => g.Key, g => g.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount)))
                : invoices.ToDictionary(i => i.InvoiceID, i => i.GrandTotal);

            var ranked = invoices
                .GroupBy(i => new { i.CustomerID, i.CustomerName })
                .Select(g => new CustomerRankedDto
                {
                    CustomerID = g.Key.CustomerID,
                    CustomerName = g.Key.CustomerName,
                    AreaName = g.Select(x => x.AreaName).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)),
                    Revenue = g.Sum(x => revenueByInvoice.TryGetValue(x.InvoiceID, out var v) ? v : 0m),
                    InvoiceCount = g.Count(),
                    LastSaleDate = g.Max(x => x.InvoiceDate),
                    HasSalesInPeriod = true
                })
                .ToList();

            var returnsByCustomer = returns.GroupBy(r => r.CustomerID).ToDictionary(g => g.Key, g => g.Sum(r => r.LineRefundAmount));
            foreach (var c in ranked)
                c.Returns = returnsByCustomer.TryGetValue(c.CustomerID, out var r) ? r : 0m;

            var balances = await _repository.GetCustomerLedgerBalancesAsync(ranked.Select(c => c.CustomerID), cancellationToken);
            foreach (var c in ranked)
                c.Outstanding = Math.Max(0m, balances.TryGetValue(c.CustomerID, out var b) ? b : 0m);

            ranked = SortCustomers(ranked, sortBy);

            var allCustomers = await _repository.GetCustomersAsync(cancellationToken);
            var activeIds = ranked.Select(c => c.CustomerID).ToHashSet();
            var inactive = allCustomers
                .Where(c => !activeIds.Contains(c.Id))
                .Select(c => new CustomerRankedDto
                {
                    CustomerID = c.Id,
                    CustomerName = c.Name,
                    HasSalesInPeriod = false
                })
                .OrderBy(c => c.CustomerName)
                .ToList();

            return new CustomerAnalyticsDto { Customers = ranked, InactiveCustomers = inactive };
        }

        public async Task<CustomerDetailDto?> GetCustomerDetailAsync(int customerId, AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter.CustomerID = customerId;
            var (start, end, _, _) = AnalyticsFilterHelper.ResolveDates(filter);
            var name = await _repository.GetCustomerNameAsync(customerId, cancellationToken);
            if (string.IsNullOrEmpty(name))
                return null;

            var invoices = await _repository.GetSalesInvoiceRowsAsync(filter, start, end, cancellationToken);
            var items = await _repository.GetSalesItemRowsAsync(filter, start, end, cancellationToken);
            var returns = await _repository.GetReturnRowsAsync(filter, start, end, cancellationToken);
            var payments = await _repository.GetCustomerPaymentsAsync(filter, start, end, cancellationToken);
            var outstanding = await _repository.GetCustomerReceivablesAsync(customerId, cancellationToken);
            var trend = await GetSalesTrendAsync(filter, "Daily", cancellationToken);

            bool byCategory = AnalyticsFilterHelper.HasCategory(filter);
            decimal billed = byCategory
                ? items.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount))
                : invoices.Sum(i => i.GrandTotal);

            var productMix = items
                .Where(i => i.ProductID.HasValue)
                .GroupBy(i => new { i.ProductID, i.ProductName, i.CategoryName })
                .Select(g => new TopProductDto
                {
                    ProductID = g.Key.ProductID ?? 0,
                    ProductName = g.Key.ProductName,
                    CategoryName = g.Key.CategoryName,
                    QuantitySold = g.Sum(i => i.ConvertedQuantity),
                    Revenue = g.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount)),
                    EstimatedProfit = g.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount) - (i.ConvertedQuantity * i.AveragePurchaseCost))
                })
                .OrderByDescending(p => p.Revenue)
                .Take(10)
                .ToList();

            return new CustomerDetailDto
            {
                CustomerID = customerId,
                CustomerName = name,
                AreaName = invoices.Select(i => i.AreaName).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)),
                SubAreaName = invoices.Select(i => i.SubAreaName).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)),
                Revenue = billed,
                Returns = returns.Sum(r => r.LineRefundAmount),
                Outstanding = Math.Max(0m, outstanding),
                PaymentsCollected = payments,
                Billed = billed,
                InvoiceCount = invoices.Count,
                SalesTrend = trend,
                ProductMix = productMix
            };
        }

        public async Task<ProductAnalyticsDto> GetProductAnalyticsAsync(AnalyticsFilterDto filter, string sortBy = "Quantity", CancellationToken cancellationToken = default)
        {
            var (start, end, _, _) = AnalyticsFilterHelper.ResolveDates(filter);
            var items = await _repository.GetSalesItemRowsAsync(filter, start, end, cancellationToken);
            var stocks = await _repository.GetStockRowsAsync(filter, cancellationToken);
            var purchased = await _repository.GetPurchaseItemRowsAsync(filter, start, end, cancellationToken);
            var categoryMix = await GetCategorySalesAsync(filter, cancellationToken);
            var risk = await GetStockRiskItemsAsync(filter, cancellationToken);
            var movement = await GetInventoryMovementAsync(filter, cancellationToken);
            var inventory = await GetInventoryInsightsAsync(filter, cancellationToken);

            var stockByProduct = stocks.GroupBy(s => s.ProductID).ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));

            var ranked = items
                .Where(i => i.ProductID.HasValue)
                .GroupBy(i => new { i.ProductID, i.ProductName, i.CategoryName })
                .Select(g =>
                {
                    decimal qty = g.Sum(i => i.ConvertedQuantity);
                    decimal revenue = g.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount));
                    decimal cost = g.Sum(i => i.ConvertedQuantity * i.AveragePurchaseCost);
                    decimal stock = stockByProduct.TryGetValue(g.Key.ProductID ?? 0, out var s) ? s : 0m;
                    return new ProductRankedDto
                    {
                        ProductID = g.Key.ProductID ?? 0,
                        ProductName = g.Key.ProductName,
                        CategoryName = g.Key.CategoryName,
                        QuantitySoldBase = qty,
                        Revenue = revenue,
                        EstimatedProfit = revenue - cost,
                        CurrentStock = stock,
                        Velocity = stock > 0 ? Math.Round(qty / stock, 3) : null
                    };
                })
                .ToList();

            var soldIds = ranked.Select(p => p.ProductID).ToHashSet();
            var dead = stocks
                .GroupBy(s => new { s.ProductID, s.ProductName, s.CategoryName, s.ReorderLevel })
                .Where(g => !soldIds.Contains(g.Key.ProductID) && g.Sum(s => s.Quantity) > 0)
                .Select(g => new ProductRankedDto
                {
                    ProductID = g.Key.ProductID,
                    ProductName = g.Key.ProductName,
                    CategoryName = g.Key.CategoryName,
                    QuantitySoldBase = 0m,
                    CurrentStock = g.Sum(s => s.Quantity),
                    MoverClass = "Dead",
                    RiskLevel = g.Sum(s => s.Quantity) <= g.Key.ReorderLevel ? (g.Sum(s => s.Quantity) <= 0 ? "OUT_OF_STOCK" : "LOW_STOCK") : string.Empty
                })
                .ToList();

            var soldOrdered = ranked.OrderByDescending(p => p.QuantitySoldBase).ToList();
            int fastCount = Math.Max(1, (int)Math.Ceiling(soldOrdered.Count * 0.2));
            for (int i = 0; i < soldOrdered.Count; i++)
                soldOrdered[i].MoverClass = i < fastCount ? "Fast" : "Slow";

            ranked = SortProducts(soldOrdered, sortBy);
            var fast = soldOrdered.Where(p => p.MoverClass == "Fast").Take(10).ToList();
            var slow = soldOrdered.Where(p => p.MoverClass == "Slow").Take(10).ToList();

            var topPurchased = purchased
                .GroupBy(p => new { p.ProductID, p.ProductName })
                .Select(g => new TopPurchasedProductDto
                {
                    ProductID = g.Key.ProductID,
                    ProductName = g.Key.ProductName,
                    QuantityPurchasedBase = g.Sum(x => x.ConvertedQuantity),
                    PurchaseCost = g.Sum(x => x.TotalCost)
                })
                .OrderByDescending(p => p.QuantityPurchasedBase)
                .Take(10)
                .ToList();

            return new ProductAnalyticsDto
            {
                Products = ranked,
                FastMovers = fast,
                SlowMovers = slow,
                DeadMovers = dead.Take(20).ToList(),
                CategoryMix = categoryMix,
                StockRisk = risk,
                TopPurchased = topPurchased,
                Movement = movement,
                Inventory = inventory
            };
        }

        public async Task<ProductDetailDto?> GetProductDetailAsync(int productId, AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var name = await _repository.GetProductNameAsync(productId, cancellationToken);
            if (string.IsNullOrEmpty(name))
                return null;

            var (start, end, _, _) = AnalyticsFilterHelper.ResolveDates(filter);
            var items = (await _repository.GetSalesItemRowsAsync(filter, start, end, cancellationToken))
                .Where(i => i.ProductID == productId)
                .ToList();
            var stocks = (await _repository.GetStockRowsAsync(filter, cancellationToken))
                .Where(s => s.ProductID == productId)
                .ToList();

            var buckets = items
                .GroupBy(i => AnalyticsFilterHelper.Bucket(i.InvoiceDate, "Daily"))
                .Select(g => new SalesTrendItemDto
                {
                    Label = g.Key.Label,
                    BucketStart = g.Key.BucketStart,
                    BucketEnd = g.Key.BucketEnd,
                    SalesAmount = g.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount)),
                    InvoiceCount = g.Select(i => i.InvoiceID).Distinct().Count()
                })
                .OrderBy(b => b.BucketStart)
                .ToList();

            decimal qty = items.Sum(i => i.ConvertedQuantity);
            decimal revenue = items.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount));
            decimal cost = items.Sum(i => i.ConvertedQuantity * i.AveragePurchaseCost);
            decimal stock = stocks.Sum(s => s.Quantity);
            decimal reorder = stocks.Select(s => s.ReorderLevel).FirstOrDefault();

            return new ProductDetailDto
            {
                ProductID = productId,
                ProductName = name,
                CategoryName = items.Select(i => i.CategoryName).FirstOrDefault() ?? stocks.Select(s => s.CategoryName).FirstOrDefault() ?? string.Empty,
                BaseUnit = stocks.Select(s => s.BaseUnit).FirstOrDefault() ?? "Units",
                QuantitySoldBase = qty,
                Revenue = revenue,
                EstimatedProfit = revenue - cost,
                CurrentStock = stock,
                ReorderLevel = reorder,
                RiskLevel = stock <= 0 ? "OUT_OF_STOCK" : (stock <= reorder ? "LOW_STOCK" : "OK"),
                SalesTrend = buckets,
                Customers = items
                    .GroupBy(i => new { i.CustomerID, i.CustomerName })
                    .Select(g => new ProductCustomerDto
                    {
                        CustomerID = g.Key.CustomerID,
                        CustomerName = g.Key.CustomerName,
                        QuantitySoldBase = g.Sum(i => i.ConvertedQuantity),
                        Revenue = g.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount))
                    })
                    .OrderByDescending(c => c.Revenue)
                    .ToList(),
                WarehouseStock = stocks
                    .GroupBy(s => s.WarehouseName)
                    .Select(g => new WarehouseStockDto { WarehouseName = g.Key, Quantity = g.Sum(s => s.Quantity) })
                    .ToList()
            };
        }

        public async Task<BookerAnalyticsDto> GetBookerAnalyticsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            var (start, end, _, _) = AnalyticsFilterHelper.ResolveDates(filter);
            var invoices = await _repository.GetSalesInvoiceRowsAsync(filter, start, end, cancellationToken);
            var items = AnalyticsFilterHelper.HasCategory(filter)
                ? await _repository.GetSalesItemRowsAsync(filter, start, end, cancellationToken)
                : new List<AnalyticsSalesItemRow>();
            var returns = await _repository.GetReturnRowsAsync(filter, start, end, cancellationToken);

            bool byCategory = AnalyticsFilterHelper.HasCategory(filter);
            var revenueByInvoice = byCategory
                ? items.GroupBy(i => i.InvoiceID).ToDictionary(g => g.Key, g => g.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount)))
                : invoices.ToDictionary(i => i.InvoiceID, i => i.GrandTotal);

            var discountByInvoice = byCategory
                ? items.GroupBy(i => i.InvoiceID).ToDictionary(g => g.Key, g => g.Sum(i => i.DiscountAmount))
                : invoices.ToDictionary(i => i.InvoiceID, i => i.DiscountTotal);

            var bookers = invoices
                .GroupBy(i => new { i.BookerID, Name = string.IsNullOrWhiteSpace(i.BookerName) ? "Unassigned" : i.BookerName })
                .Select(g => new BookerRankedDto
                {
                    BookerID = g.Key.BookerID,
                    BookerName = g.Key.Name,
                    InvoiceCount = g.Count(),
                    Revenue = g.Sum(x => revenueByInvoice.TryGetValue(x.InvoiceID, out var v) ? v : 0m),
                    Discounts = g.Sum(x => discountByInvoice.TryGetValue(x.InvoiceID, out var d) ? d : 0m),
                    Outstanding = g.Sum(x => AnalyticsFilterHelper.InvoiceRemaining(x.GrandTotal, x.PaidAmount, x.ReturnedAmount)),
                    UniqueCustomers = g.Select(x => x.CustomerID).Distinct().Count()
                })
                .OrderByDescending(b => b.Revenue)
                .ToList();

            var returnsByBooker = returns
                .GroupBy(r => r.BookerID ?? -1)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.LineRefundAmount));
            foreach (var b in bookers)
            {
                int key = b.BookerID ?? -1;
                b.Returns = returnsByBooker.TryGetValue(key, out var r) ? r : 0m;
            }

            var topCustomers = invoices
                .GroupBy(i => new { i.CustomerID, i.CustomerName })
                .Select(g => new TopCustomerDto
                {
                    CustomerID = g.Key.CustomerID,
                    CustomerName = g.Key.CustomerName,
                    Revenue = g.Sum(x => revenueByInvoice.TryGetValue(x.InvoiceID, out var v) ? v : 0m),
                    InvoiceCount = g.Count()
                })
                .OrderByDescending(c => c.Revenue)
                .Take(10)
                .ToList();

            var topProducts = await GetTopProductsAsync(filter, "Revenue", 10, cancellationToken);

            return new BookerAnalyticsDto
            {
                Revenue = bookers.Sum(b => b.Revenue),
                InvoiceCount = bookers.Sum(b => b.InvoiceCount),
                UniqueCustomers = invoices.Select(i => i.CustomerID).Distinct().Count(),
                Outstanding = bookers.Sum(b => b.Outstanding),
                Bookers = bookers,
                TopCustomers = topCustomers,
                TopProducts = topProducts
            };
        }

        public async Task<BookerDetailDto?> GetBookerDetailAsync(int? bookerId, AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter.BookerID = bookerId.HasValue && bookerId.Value > 0 ? bookerId : -1;
            string name = "Unassigned";
            if (bookerId.HasValue && bookerId.Value > 0)
            {
                var found = await _repository.GetBookerNameAsync(bookerId.Value, cancellationToken);
                if (string.IsNullOrEmpty(found))
                    return null;
                name = found;
            }

            var summary = await GetBookerAnalyticsAsync(filter, cancellationToken);
            var trend = await GetSalesTrendAsync(filter, "Daily", cancellationToken);
            var customers = await GetCustomerAnalyticsAsync(filter, "Revenue", cancellationToken);
            var payments = await GetPaymentAnalyticsAsync(filter, cancellationToken);
            var bookerRow = summary.Bookers.FirstOrDefault();

            return new BookerDetailDto
            {
                BookerID = bookerId.HasValue && bookerId.Value > 0 ? bookerId : null,
                BookerName = name,
                Revenue = bookerRow?.Revenue ?? 0m,
                Returns = bookerRow?.Returns ?? 0m,
                Discounts = bookerRow?.Discounts ?? 0m,
                Outstanding = bookerRow?.Outstanding ?? 0m,
                InvoiceCount = bookerRow?.InvoiceCount ?? 0,
                SalesTrend = trend,
                Customers = customers.Customers,
                PaymentMix = payments.InvoiceStatuses,
                TopProducts = summary.TopProducts
            };
        }

        private async Task<(decimal Sales, decimal Purchases, decimal Discounts, decimal Returns, decimal NetSales, decimal Profit)> BuildPeriodFinancialsAsync(
            AnalyticsFilterDto filter, DateTime start, DateTime end, CancellationToken cancellationToken)
        {
            var invoices = await _repository.GetSalesInvoiceRowsAsync(filter, start, end, cancellationToken);
            var items = await _repository.GetSalesItemRowsAsync(filter, start, end, cancellationToken);
            var returns = await _repository.GetReturnRowsAsync(filter, start, end, cancellationToken);

            bool byCategory = AnalyticsFilterHelper.HasCategory(filter);
            decimal sales = byCategory
                ? items.Sum(i => AnalyticsFilterHelper.LineRevenue(i.Quantity, i.UnitPrice, i.DiscountAmount))
                : invoices.Sum(i => i.GrandTotal);
            decimal discounts = byCategory ? items.Sum(i => i.DiscountAmount) : invoices.Sum(i => i.DiscountTotal);
            decimal returnAmt = returns.Sum(r => r.LineRefundAmount);
            decimal purchases = byCategory
                ? (await _repository.GetPurchaseItemRowsAsync(filter, start, end, cancellationToken)).Sum(p => p.TotalCost)
                : (await _repository.GetPurchaseInvoiceRowsAsync(filter, start, end, cancellationToken)).Sum(p => p.GrandTotal);
            decimal cogs = items.Sum(i => i.ConvertedQuantity * i.AveragePurchaseCost);
            decimal netSales = sales - returnAmt;
            decimal profit = netSales - cogs;
            return (sales, purchases, discounts, returnAmt, netSales, profit);
        }

        private static List<BusinessInsightDto> BuildInsights(AnalyticsKpiSummaryDto kpi, InventoryInsightsDto inv)
        {
            var insights = new List<BusinessInsightDto>();
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

            if (inv.LowStockProductCount > 0)
            {
                insights.Add(new BusinessInsightDto
                {
                    Type = "WARNING",
                    Title = "Reorder Level Alert",
                    Message = $"{inv.LowStockProductCount} product(s) have fallen below their minimum reorder levels.",
                    Icon = "bi-triangle-fill"
                });
            }

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

            if (kpi.TotalSales.PercentageChange > 10m)
            {
                insights.Add(new BusinessInsightDto
                {
                    Type = "SUCCESS",
                    Title = "Sales Surge",
                    Message = $"Gross sales increased by {kpi.TotalSales.PercentageChange:0.0}% compared to the previous period.",
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

            if (kpi.TotalSales.CurrentValue != 0)
            {
                decimal returnRate = kpi.TotalSales.CurrentValue == 0 ? 0 : (kpi.TotalReturns.CurrentValue / Math.Abs(kpi.TotalSales.CurrentValue)) * 100m;
                if (returnRate > 5m)
                {
                    insights.Add(new BusinessInsightDto
                    {
                        Type = "WARNING",
                        Title = "High Return Rate",
                        Message = $"Sales returns equal {returnRate:0.1}% of gross sales for this period.",
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

        private static KpiMetricDto BuildMetric(string title, decimal current, decimal previous)
        {
            decimal change = 0m;
            if (previous != 0)
                change = Math.Round(((current - previous) / Math.Abs(previous)) * 100m, 1);
            else if (current != 0)
                change = 100m;

            return new KpiMetricDto
            {
                Title = title,
                CurrentValue = current,
                PreviousValue = previous,
                PercentageChange = change
            };
        }

        private static KpiMetricDto SnapshotMetric(string title, decimal value) =>
            new KpiMetricDto
            {
                Title = title,
                CurrentValue = value,
                PreviousValue = value,
                PercentageChange = 0m,
                IsSnapshot = true
            };

        private static List<CustomerRankedDto> SortCustomers(List<CustomerRankedDto> ranked, string sortBy)
        {
            return (sortBy ?? "Revenue").ToLowerInvariant() switch
            {
                "invoicecount" => ranked.OrderByDescending(c => c.InvoiceCount).ThenByDescending(c => c.Revenue).ToList(),
                "returns" => ranked.OrderByDescending(c => c.Returns).ThenByDescending(c => c.Revenue).ToList(),
                "outstanding" => ranked.OrderByDescending(c => c.Outstanding).ThenByDescending(c => c.Revenue).ToList(),
                "lastsaledate" => ranked.OrderByDescending(c => c.LastSaleDate).ThenByDescending(c => c.Revenue).ToList(),
                _ => ranked.OrderByDescending(c => c.Revenue).ThenByDescending(c => c.InvoiceCount).ToList()
            };
        }

        private static List<ProductRankedDto> SortProducts(List<ProductRankedDto> ranked, string sortBy)
        {
            return (sortBy ?? "Quantity").ToLowerInvariant() switch
            {
                "revenue" => ranked.OrderByDescending(p => p.Revenue).ToList(),
                "profit" => ranked.OrderByDescending(p => p.EstimatedProfit).ToList(),
                "stock" => ranked.OrderByDescending(p => p.CurrentStock).ToList(),
                "velocity" => ranked.OrderByDescending(p => p.Velocity ?? 0m).ToList(),
                _ => ranked.OrderByDescending(p => p.QuantitySoldBase).ToList()
            };
        }
    }
}
