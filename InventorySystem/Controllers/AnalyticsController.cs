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

        public AnalyticsController(IAnalyticsService analyticsService, ApplicationDbContext context, ILogger<AnalyticsController> logger)
        {
            _analyticsService = analyticsService;
            _context = context;
            _logger = logger;
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
