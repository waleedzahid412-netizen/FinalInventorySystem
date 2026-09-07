using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Authorization;
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Helpers;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Home;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICompanyContext _companyContext;
        private readonly IFifoCostingService _fifoCostingService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ApplicationDbContext context,
            ICompanyContext companyContext,
            IFifoCostingService fifoCostingService,
            ILogger<HomeController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _companyContext = companyContext ?? throw new ArgumentNullException(nameof(companyContext));
            _fifoCostingService = fifoCostingService ?? throw new ArgumentNullException(nameof(fifoCostingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var today = DateTime.UtcNow.Date;
            var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            await _companyContext.TryResolveAsync(cancellationToken);
            bool isAllCompanies = _companyContext.IsAllCompanies;
            bool hasResolvedScope = _companyContext.HasResolvedScope;
            bool showAll = isAllCompanies || (!_companyContext.HasCompany && !hasResolvedScope);
            int? companyId = _companyContext.HasCompany ? _companyContext.CompanyID : null;

            // 1. Sales & returns (net sales = gross invoice total − returns in period)
            var salesQuery = _context.SalesInvoices.AsNoTracking().Where(i => !i.IsDeleted);
            if (companyId.HasValue)
                salesQuery = salesQuery.Where(i => i.CompanyID == companyId.Value);

            var returnsQuery = _context.SalesReturns.AsNoTracking().Where(r => !r.IsDeleted);
            if (companyId.HasValue)
            {
                int cid = companyId.Value;
                returnsQuery = returnsQuery.Where(r =>
                    (r.SalesInvoice != null && r.SalesInvoice.CompanyID == cid) ||
                    (r.InvoiceID == null && r.Items.Any(i => i.Product != null && i.Product.CompanyID == cid)));
            }

            decimal salesToday = await DashboardMetricsHelper.SumGrossSalesAsync(salesQuery, today, cancellationToken);
            decimal salesThisMonth = await DashboardMetricsHelper.SumGrossSalesAsync(salesQuery, startOfMonth, cancellationToken);
            decimal returnsToday = await DashboardMetricsHelper.SumReturnsAsync(returnsQuery, today, cancellationToken);
            decimal returnsThisMonth = await DashboardMetricsHelper.SumReturnsAsync(returnsQuery, startOfMonth, cancellationToken);

            decimal netSalesToday = DashboardMetricsHelper.ComputeNetSales(salesToday, returnsToday);
            decimal netSalesThisMonth = DashboardMetricsHelper.ComputeNetSales(salesThisMonth, returnsThisMonth);

            // 2. Purchases Totals
            var purchaseQuery = _context.PurchaseInvoices.AsNoTracking().Where(i => !i.IsDeleted);
            if (companyId.HasValue)
                purchaseQuery = purchaseQuery.Where(i => i.CompanyID == companyId.Value);

            decimal purchasesToday = await purchaseQuery
                .Where(i => i.InvoiceDate >= today)
                .SumAsync(i => (decimal?)i.GrandTotal, cancellationToken) ?? 0m;

            decimal purchasesThisMonth = await purchaseQuery
                .Where(i => i.InvoiceDate >= startOfMonth)
                .SumAsync(i => (decimal?)i.GrandTotal, cancellationToken) ?? 0m;

            // 3. Outstanding Balances
            decimal customerReceivables = await DashboardMetricsHelper.SumCustomerReceivablesAsync(salesQuery, cancellationToken);

            decimal supplierPayables = await purchaseQuery
                .Where(i => i.PaymentStatus != "PAID")
                .SumAsync(i => (decimal?)(i.GrandTotal - i.PaidAmount), cancellationToken) ?? 0m;

            // 4. Inventory Valuation (FIFO cost layers — BR-004)
            decimal inventoryValue = await _fifoCostingService.GetInventoryValueAsync(companyId, null, cancellationToken);

            // 5. Today Returns Count
            int todayReturnsCount = await returnsQuery
                .Where(r => r.ReturnDate >= today)
                .CountAsync(cancellationToken);

            // 6. Low Stock Products & Count
            var productQuery = _context.Products.AsNoTracking().Where(p => !p.IsDeleted && p.IsActive);
            if (companyId.HasValue)
                productQuery = productQuery.Where(p => p.CompanyID == companyId.Value);

            var productStocks = await productQuery
                .Select(p => new
                {
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    SKU = p.SKU,
                    ReorderLevel = (decimal)p.ReorderLevel,
                    CurrentStock = _context.InventoryStocks
                        .Where(s => !s.IsDeleted && s.ProductID == p.ProductID)
                        .Sum(s => (decimal?)s.Quantity) ?? 0m
                })
                .Where(x => x.ReorderLevel > 0 && x.CurrentStock <= x.ReorderLevel)
                .ToListAsync(cancellationToken);

            int lowStockCount = productStocks.Count;

            var lowStockList = productStocks
                .OrderBy(x => x.CurrentStock)
                .Take(5)
                .Select(x => new DashboardLowStockProduct
                {
                    ProductID = x.ProductID,
                    ProductName = x.ProductName,
                    SKU = x.SKU ?? "—",
                    CurrentStock = x.CurrentStock,
                    MinStockLevel = x.ReorderLevel
                })
                .ToList();

            // 7. Recent Activity Lists
            var recentSales = await salesQuery
                .Include(i => i.Customer)
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .Select(i => new DashboardSaleItem
                {
                    InvoiceID = i.InvoiceID,
                    InvoiceNumber = i.InvoiceNumber,
                    CustomerName = i.Customer != null ? i.Customer.ShopName : "Walk-in Customer",
                    InvoiceDate = i.InvoiceDate,
                    TotalAmount = i.GrandTotal,
                    PaymentStatus = i.PaymentStatus
                })
                .ToListAsync(cancellationToken);

            var recentPurchases = await purchaseQuery
                .Include(i => i.Company)
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .Select(i => new DashboardPurchaseItem
                {
                    InvoiceID = i.PurchaseInvoiceID,
                    InvoiceNumber = i.InvoiceNumber,
                    SupplierName = i.Company != null ? i.Company.CompanyName : "Supplier",
                    InvoiceDate = i.InvoiceDate,
                    TotalAmount = i.GrandTotal,
                    PaymentStatus = i.PaymentStatus
                })
                .ToListAsync(cancellationToken);

            var recentReturns = await returnsQuery
                .Include(r => r.Customer)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .Select(r => new DashboardReturnItem
                {
                    SalesReturnID = r.SalesReturnID,
                    ReturnNumber = r.ReturnNumber,
                    CustomerName = r.Customer != null ? r.Customer.ShopName : "Customer",
                    ReturnDate = r.ReturnDate,
                    NetRefundAmount = r.NetRefundAmount,
                    ReturnType = r.ReturnType ?? "INVOICE"
                })
                .ToListAsync(cancellationToken);

            var viewModel = new DashboardViewModel
            {
                NetSalesToday = netSalesToday,
                NetSalesThisMonth = netSalesThisMonth,
                TotalPurchasesToday = purchasesToday,
                TotalPurchasesThisMonth = purchasesThisMonth,
                CustomerReceivables = customerReceivables,
                SupplierPayables = supplierPayables,
                InventoryValue = inventoryValue,
                LowStockCount = lowStockCount,
                TodayReturnsCount = todayReturnsCount,
                RecentSales = recentSales,
                RecentPurchases = recentPurchases,
                RecentReturns = recentReturns,
                LowStockProducts = lowStockList,
                ShowAllCompanies = showAll,
                IsAllCompaniesMode = isAllCompanies,
                IsUnscoped = !hasResolvedScope,
                ScopedCompanyName = _companyContext.HasCompany ? _companyContext.CompanyName : (_companyContext.IsAllCompanies ? "All Companies" : string.Empty),
                HasCompanyScope = hasResolvedScope,
                ScopedCompanyId = _companyContext.HasCompany ? _companyContext.CompanyID : null
            };

            return View(viewModel);
        }

        [HttpGet]
        [SkipPermissionCheck]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [SkipPermissionCheck]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
