using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Analytics;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class AnalyticsController : Controller
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly IAnalyticsRepository _analyticsRepository;
        private readonly ILookupService _lookupService;
        private readonly ICompanyContext _companyContext;
        private readonly IUserCompanyAccessService _companyAccess;
        private readonly ILogger<AnalyticsController> _logger;
        private readonly IPdfService _pdfService;

        public AnalyticsController(
            IAnalyticsService analyticsService,
            IAnalyticsRepository analyticsRepository,
            ILookupService lookupService,
            ICompanyContext companyContext,
            IUserCompanyAccessService companyAccess,
            ILogger<AnalyticsController> logger,
            IPdfService pdfService)
        {
            _analyticsService = analyticsService;
            _analyticsRepository = analyticsRepository;
            _lookupService = lookupService;
            _companyContext = companyContext;
            _companyAccess = companyAccess;
            _logger = logger;
            _pdfService = pdfService;
        }

        private async Task<AnalyticsFilterDto> DefaultFilterAsync(CancellationToken cancellationToken)
        {
            var filter = new AnalyticsFilterDto { Preset = "ThisMonth" };
            // HasCompany only — All Companies / unscoped leave CompanyID null (consolidated).
            if (await _companyContext.TryResolveAsync(cancellationToken) && _companyContext.HasCompany)
            {
                filter.CompanyID = _companyContext.CompanyID;
            }
            return filter;
        }

        /// <summary>
        /// Pins AJAX filter company from ambient scope so the client cannot escape HasCompany.
        /// All Companies forces null. Non-admin users without resolved scope are rejected.
        /// </summary>
        private async Task<IActionResult?> ApplyAmbientCompanyScopeAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken)
        {
            if (!await _companyContext.TryResolveAsync(cancellationToken) || !_companyContext.HasResolvedScope)
            {
                if (!await _companyAccess.IsCurrentUserAdminAsync(cancellationToken))
                {
                    return Unauthorized(new { success = false, message = "Select a company before viewing analytics." });
                }

                return null;
            }

            if (_companyContext.HasCompany)
            {
                filter.CompanyID = _companyContext.CompanyID;
            }
            else if (_companyContext.IsAllCompanies)
            {
                filter.CompanyID = null;
            }

            return null;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            await PopulateDropdownsAsync(showBooker: true, cancellationToken);
            ViewData["AnalyticsSection"] = "Overview";
            var filter = await DefaultFilterAsync(cancellationToken);
            OverviewAnalyticsDto model;
            try
            {
                model = await _analyticsService.GetOverviewAsync(filter, "Daily", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Analytics Overview.");
                model = new OverviewAnalyticsDto();
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Sales(CancellationToken cancellationToken)
        {
            await PopulateDropdownsAsync(showBooker: true, cancellationToken);
            ViewData["AnalyticsSection"] = "Sales";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Customers(CancellationToken cancellationToken)
        {
            await PopulateDropdownsAsync(showBooker: true, cancellationToken);
            ViewData["AnalyticsSection"] = "Customers";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Customer(int id, CancellationToken cancellationToken)
        {
            await PopulateDropdownsAsync(showBooker: true, cancellationToken);
            ViewData["AnalyticsSection"] = "Customers";
            ViewData["HideCustomerFilter"] = true;
            var filter = await DefaultFilterAsync(cancellationToken);
            filter.CustomerID = id;
            CustomerDetailDto? model;
            try
            {
                model = await _analyticsService.GetCustomerDetailAsync(id, filter, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer analytics {CustomerId}.", id);
                model = null;
            }

            if (model == null)
                return NotFound();

            return View("CustomerDetail", model);
        }

        [HttpGet]
        public async Task<IActionResult> Products(CancellationToken cancellationToken)
        {
            await PopulateDropdownsAsync(showBooker: false, cancellationToken);
            ViewData["AnalyticsSection"] = "Products";
            ViewData["HideCustomerFilter"] = false;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Product(int id, CancellationToken cancellationToken)
        {
            await PopulateDropdownsAsync(showBooker: false, cancellationToken);
            ViewData["AnalyticsSection"] = "Products";
            var filter = await DefaultFilterAsync(cancellationToken);
            ProductDetailDto? model;
            try
            {
                model = await _analyticsService.GetProductDetailAsync(id, filter, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product analytics {ProductId}.", id);
                model = null;
            }

            if (model == null)
                return NotFound();

            return View("ProductDetail", model);
        }

        [HttpGet]
        public async Task<IActionResult> Bookers(CancellationToken cancellationToken)
        {
            await PopulateDropdownsAsync(showBooker: true, cancellationToken);
            ViewData["AnalyticsSection"] = "Bookers";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Booker(int id, CancellationToken cancellationToken)
        {
            await PopulateDropdownsAsync(showBooker: true, cancellationToken);
            ViewData["AnalyticsSection"] = "Bookers";
            ViewData["HideBookerFilter"] = true;
            int? bookerId = id == 0 ? null : id;
            var filter = await DefaultFilterAsync(cancellationToken);
            filter.BookerID = bookerId.HasValue ? bookerId : -1;
            BookerDetailDto? model;
            try
            {
                model = await _analyticsService.GetBookerDetailAsync(bookerId, filter, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading booker analytics {BookerId}.", id);
                model = null;
            }

            if (model == null)
                return NotFound();

            return View("BookerDetail", model);
        }

        [HttpPost]
        public async Task<IActionResult> GetOverview([FromBody] AnalyticsFilterDto filter, [FromQuery] string interval = "Daily", CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetOverviewAsync(filter, interval, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOverview.");
                return Json(new OverviewAnalyticsDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetKpiSummary([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetKpiSummaryAsync(filter, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetKpiSummary.");
                return Json(new AnalyticsKpiSummaryDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetSalesTrend([FromBody] AnalyticsFilterDto filter, [FromQuery] string interval = "Daily", CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetSalesTrendAsync(filter, interval, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSalesTrend.");
                return Json(new List<SalesTrendItemDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetTopProducts([FromBody] AnalyticsFilterDto filter, [FromQuery] string sortBy = "Revenue", [FromQuery] int topCount = 10, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetTopProductsAsync(filter, sortBy, topCount, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTopProducts.");
                return Json(new List<TopProductDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetTopCustomers([FromBody] AnalyticsFilterDto filter, [FromQuery] string sortBy = "Revenue", [FromQuery] int topCount = 10, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetTopCustomersAsync(filter, sortBy, topCount, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTopCustomers.");
                return Json(new List<TopCustomerDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetCategorySales([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetCategorySalesAsync(filter, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCategorySales.");
                return Json(new List<CategorySalesDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetPaymentAnalytics([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetPaymentAnalyticsAsync(filter, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPaymentAnalytics.");
                return Json(new PaymentAnalyticsDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetInventoryInsights([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetInventoryInsightsAsync(filter, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetInventoryInsights.");
                return Json(new InventoryInsightsDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetStockRiskItems([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetStockRiskItemsAsync(filter, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStockRiskItems.");
                return Json(new List<StockRiskItemDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetInventoryMovement([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetInventoryMovementAsync(filter, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetInventoryMovement.");
                return Json(new InventoryMovementDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetPromotionPerformance([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetPromotionPerformanceAsync(filter, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPromotionPerformance.");
                return Json(new PromotionPerformanceDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetTopCompanies([FromBody] AnalyticsFilterDto filter, [FromQuery] int topCount = 5, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetTopCompaniesAsync(filter, topCount, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTopCompanies.");
                return Json(new List<TopCompanyDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetBusinessInsights([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetBusinessInsightsAsync(filter, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBusinessInsights.");
                return Json(new List<BusinessInsightDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetSalesAnalytics([FromBody] AnalyticsFilterDto filter, [FromQuery] string interval = "Daily", CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetSalesAnalyticsAsync(filter, interval, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSalesAnalytics.");
                return Json(new SalesAnalyticsDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetSalesDrilldown([FromBody] AnalyticsFilterDto filter, [FromQuery] DateTime bucketStart, [FromQuery] DateTime bucketEnd, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetSalesDrilldownAsync(filter, bucketStart, bucketEnd, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSalesDrilldown.");
                return Json(new List<InvoiceDrilldownDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetCustomerAnalytics([FromBody] AnalyticsFilterDto filter, [FromQuery] string sortBy = "Revenue", CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetCustomerAnalyticsAsync(filter, sortBy, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCustomerAnalytics.");
                return Json(new CustomerAnalyticsDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetCustomerDetail([FromBody] AnalyticsFilterDto filter, [FromQuery] int id, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetCustomerDetailAsync(id, filter, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCustomerDetail.");
                return Json(null);
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetProductAnalytics([FromBody] AnalyticsFilterDto filter, [FromQuery] string sortBy = "Quantity", CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetProductAnalyticsAsync(filter, sortBy, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetProductAnalytics.");
                return Json(new ProductAnalyticsDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetProductDetail([FromBody] AnalyticsFilterDto filter, [FromQuery] int id, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetProductDetailAsync(id, filter, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetProductDetail.");
                return Json(null);
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetBookerAnalytics([FromBody] AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            try
            {
                return Json(await _analyticsService.GetBookerAnalyticsAsync(filter, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBookerAnalytics.");
                return Json(new BookerAnalyticsDto());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetBookerDetail([FromBody] AnalyticsFilterDto filter, [FromQuery] int id, CancellationToken cancellationToken = default)
        {
            filter ??= new AnalyticsFilterDto();
            var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
            if (scopeError != null)
                return scopeError;
            int? bookerId = id == 0 ? null : id;
            try
            {
                return Json(await _analyticsService.GetBookerDetailAsync(bookerId, filter, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetBookerDetail.");
                return Json(null);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            [FromQuery] string preset = "ThisMonth",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? warehouseId = null,
            [FromQuery] int? customerId = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] int? bookerId = null,
            [FromQuery] int? companyId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var filter = new AnalyticsFilterDto
                {
                    Preset = preset,
                    StartDate = startDate,
                    EndDate = endDate,
                    WarehouseID = warehouseId,
                    CustomerID = customerId,
                    CategoryID = categoryId,
                    BookerID = bookerId,
                    CompanyID = companyId
                };
                var scopeError = await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);
                if (scopeError != null)
                    return scopeError;

                var kpi = await _analyticsService.GetKpiSummaryAsync(filter, cancellationToken);
                var inv = await _analyticsService.GetInventoryInsightsAsync(filter, cancellationToken);
                var insights = await _analyticsService.GetBusinessInsightsAsync(filter, cancellationToken);
                var risk = await _analyticsService.GetStockRiskItemsAsync(filter, cancellationToken);

                string rangeLabel = preset;
                if (string.Equals(preset, "Custom", StringComparison.OrdinalIgnoreCase) && startDate.HasValue && endDate.HasValue)
                    rangeLabel = $"{startDate:dd MMM yyyy} – {endDate:dd MMM yyyy}";

                byte[] pdfBytes = _pdfService.GenerateAnalyticsReportPdf(kpi, inv, insights, risk, rangeLabel);
                return File(pdfBytes, "application/pdf", $"WIMS_Analytics_Report_{preset}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Analytics PDF report.");
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task PopulateDropdownsAsync(bool showBooker, CancellationToken cancellationToken)
        {
            try
            {
                var warehouses = await _analyticsRepository.GetWarehousesAsync(cancellationToken);
                var customers = await _analyticsRepository.GetCustomersAsync(cancellationToken);
                var categories = await _analyticsRepository.GetCategoriesAsync(cancellationToken);
                var bookers = await _analyticsRepository.GetBookersAsync(cancellationToken);
                var companies = await _lookupService.GetCompaniesAsync(cancellationToken);

                int? defaultCompanyId = null;
                await _companyContext.TryResolveAsync(cancellationToken);
                if (_companyContext.HasCompany)
                {
                    defaultCompanyId = _companyContext.CompanyID;
                }

                ViewBag.HideCompanyFilter = _companyContext.HasCompany;
                ViewBag.ScopedCompanyName = _companyContext.HasCompany ? _companyContext.CompanyName : null;

                ViewBag.Warehouses = warehouses.Select(w => new SelectListItem { Value = w.Id.ToString(), Text = w.Name }).ToList();
                ViewBag.Customers = customers.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList();
                ViewBag.Categories = categories.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList();
                ViewBag.Bookers = bookers.Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name }).ToList();
                ViewBag.Companies = companies.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name,
                    Selected = defaultCompanyId.HasValue && c.Id == defaultCompanyId.Value
                }).ToList();
                ViewBag.ShowBookerFilter = showBooker;
                ViewBag.DefaultCompanyID = defaultCompanyId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating Analytics dropdowns.");
                ViewBag.Warehouses = new List<SelectListItem>();
                ViewBag.Customers = new List<SelectListItem>();
                ViewBag.Categories = new List<SelectListItem>();
                ViewBag.Bookers = new List<SelectListItem>();
                ViewBag.Companies = new List<SelectListItem>();
                ViewBag.ShowBookerFilter = showBooker;
                ViewBag.HideCompanyFilter = false;
                ViewBag.ScopedCompanyName = null;
            }
        }
    }
}
