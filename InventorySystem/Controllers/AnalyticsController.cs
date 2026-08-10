using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventorySystem.Data;
using InventorySystem.DTOs.Analytics;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Controllers
{
    public class AnalyticsController : Controller
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AnalyticsController> _logger;
        private readonly IPdfService _pdfService;

        public AnalyticsController(IAnalyticsService analyticsService, ApplicationDbContext context, ILogger<AnalyticsController> logger, IPdfService pdfService)
        {
            _analyticsService = analyticsService;
            _context = context;
            _logger = logger;
            _pdfService = pdfService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            await PopulateDropdownsAsync(cancellationToken);

            AnalyticsKpiSummaryDto kpiData;
            try
            {
                var defaultFilter = new AnalyticsFilterDto { Preset = "ThisMonth" };
                kpiData = await _analyticsService.GetKpiSummaryAsync(defaultFilter, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating KPI summary for Analytics Index view.");
                kpiData = new AnalyticsKpiSummaryDto();
            }

            return View(kpiData ?? new AnalyticsKpiSummaryDto());
        }

        [HttpPost]
        public async Task<IActionResult> GetKpiSummary([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken)
        {
            try
            {
                if (filter == null) filter = new AnalyticsFilterDto();
                var kpiData = await _analyticsService.GetKpiSummaryAsync(filter, cancellationToken);
                return Json(kpiData ?? new AnalyticsKpiSummaryDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetKpiSummary API endpoint.");
                return Json(new AnalyticsKpiSummaryDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetSalesTrend([FromBody] AnalyticsFilterDto filter, [FromQuery] string interval = "Daily", CancellationToken cancellationToken = default)
        {
            try
            {
                if (filter == null) filter = new AnalyticsFilterDto();
                var data = await _analyticsService.GetSalesTrendAsync(filter, interval, cancellationToken);
                return Json(data ?? new List<SalesTrendItemDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSalesTrend API endpoint.");
                return Json(new List<SalesTrendItemDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetTopProducts([FromBody] AnalyticsFilterDto filter, [FromQuery] string sortBy = "Revenue", [FromQuery] int topCount = 10, CancellationToken cancellationToken = default)
        {
            try
            {
                if (filter == null) filter = new AnalyticsFilterDto();
                var data = await _analyticsService.GetTopProductsAsync(filter, sortBy, topCount, cancellationToken);
                return Json(data ?? new List<TopProductDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTopProducts API endpoint.");
                return Json(new List<TopProductDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetTopCustomers([FromBody] AnalyticsFilterDto filter, [FromQuery] string sortBy = "Revenue", [FromQuery] int topCount = 10, CancellationToken cancellationToken = default)
        {
            try
            {
                if (filter == null) filter = new AnalyticsFilterDto();
                var data = await _analyticsService.GetTopCustomersAsync(filter, sortBy, topCount, cancellationToken);
                return Json(data ?? new List<TopCustomerDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTopCustomers API endpoint.");
                return Json(new List<TopCustomerDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetCategorySales([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            try
            {
                if (filter == null) filter = new AnalyticsFilterDto();
                var data = await _analyticsService.GetCategorySalesAsync(filter, cancellationToken);
                return Json(data ?? new List<CategorySalesDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCategorySales API endpoint.");
                return Json(new List<CategorySalesDto>());
            }
        }

        // ===== WAVE 3 API ENDPOINTS =====

        [HttpPost]
        public async Task<IActionResult> GetPaymentAnalytics([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            try
            {
                if (filter == null) filter = new AnalyticsFilterDto();
                var data = await _analyticsService.GetPaymentAnalyticsAsync(filter, cancellationToken);
                return Json(data ?? new PaymentAnalyticsDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPaymentAnalytics API endpoint.");
                return Json(new PaymentAnalyticsDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetInventoryInsights([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            try
            {
                if (filter == null) filter = new AnalyticsFilterDto();
                var data = await _analyticsService.GetInventoryInsightsAsync(filter, cancellationToken);
                return Json(data ?? new InventoryInsightsDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetInventoryInsights API endpoint.");
                return Json(new InventoryInsightsDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetStockRiskItems([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            try
            {
                if (filter == null) filter = new AnalyticsFilterDto();
                var data = await _analyticsService.GetStockRiskItemsAsync(filter, cancellationToken);
                return Json(data ?? new List<StockRiskItemDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStockRiskItems API endpoint.");
                return Json(new List<StockRiskItemDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetInventoryMovement([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            try
            {
                if (filter == null) filter = new AnalyticsFilterDto();
                var data = await _analyticsService.GetInventoryMovementAsync(filter, cancellationToken);
                return Json(data ?? new InventoryMovementDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetInventoryMovement API endpoint.");
                return Json(new InventoryMovementDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetPromotionPerformance([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            try
            {
                if (filter == null) filter = new AnalyticsFilterDto();
                var data = await _analyticsService.GetPromotionPerformanceAsync(filter, cancellationToken);
                return Json(data ?? new PromotionPerformanceDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPromotionPerformance API endpoint.");
                return Json(new PromotionPerformanceDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetTopCompanies([FromBody] AnalyticsFilterDto filter, [FromQuery] int topCount = 5, CancellationToken cancellationToken = default)
        {
            try
            {
                if (filter == null) filter = new AnalyticsFilterDto();
                var data = await _analyticsService.GetTopCompaniesAsync(filter, topCount, cancellationToken);
                return Json(data ?? new List<TopCompanyDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTopCompanies API endpoint.");
                return Json(new List<TopCompanyDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetBusinessInsights([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            try
            {
                if (filter == null) filter = new AnalyticsFilterDto();
                var data = await _analyticsService.GetBusinessInsightsAsync(filter, cancellationToken);
                return Json(data ?? new List<BusinessInsightDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBusinessInsights API endpoint.");
                return Json(new List<BusinessInsightDto>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf([FromQuery] string preset = "ThisMonth", [FromQuery] int? warehouseId = null, [FromQuery] int? customerId = null, [FromQuery] int? categoryId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var filter = new AnalyticsFilterDto
                {
                    Preset = preset,
                    WarehouseID = warehouseId,
                    CustomerID = customerId,
                    CategoryID = categoryId
                };

                var kpi = await _analyticsService.GetKpiSummaryAsync(filter, cancellationToken);
                var inv = await _analyticsService.GetInventoryInsightsAsync(filter, cancellationToken);
                var insights = await _analyticsService.GetBusinessInsightsAsync(filter, cancellationToken);
                var risk = await _analyticsService.GetStockRiskItemsAsync(filter, cancellationToken);

                byte[] pdfBytes = _pdfService.GenerateAnalyticsReportPdf(kpi, inv, insights, risk, preset);
                return File(pdfBytes, "application/pdf", $"WIMS_Analytics_Report_{preset}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Analytics PDF report.");
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task PopulateDropdownsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var warehouses = await _context.Warehouses.AsNoTracking()
                    .Where(w => w.IsActive && !w.IsDeleted)
                    .OrderBy(w => w.Name)
                    .Select(w => new SelectListItem { Value = w.WarehouseID.ToString(), Text = w.Name })
                    .ToListAsync(cancellationToken);

                var customers = await _context.Customers.AsNoTracking()
                    .Where(c => c.IsActive && !c.IsDeleted)
                    .OrderBy(c => c.ShopName)
                    .Select(c => new SelectListItem { Value = c.CustomerID.ToString(), Text = c.ShopName })
                    .ToListAsync(cancellationToken);

                var categories = await _context.Categories.AsNoTracking()
                    .Where(c => c.IsActive && !c.IsDeleted)
                    .OrderBy(c => c.Name)
                    .Select(c => new SelectListItem { Value = c.CategoryID.ToString(), Text = c.Name })
                    .ToListAsync(cancellationToken);

                ViewBag.Warehouses = warehouses;
                ViewBag.Customers = customers;
                ViewBag.Categories = categories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating dropdowns for Analytics Index view.");
                ViewBag.Warehouses = new List<SelectListItem>();
                ViewBag.Customers = new List<SelectListItem>();
                ViewBag.Categories = new List<SelectListItem>();
            }
        }
    }
}
