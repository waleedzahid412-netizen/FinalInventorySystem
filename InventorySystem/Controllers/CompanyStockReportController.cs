using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventorySystem.DTOs.Reports;
using InventorySystem.Filters;
using InventorySystem.Helpers;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Reports;

namespace InventorySystem.Controllers
{
    [Authorize]
    [RequireCompanyScope]
    public class CompanyStockReportController : Controller
    {
        private readonly ICompanyStockReportService _reportService;
        private readonly ILookupService _lookupService;
        private readonly ICompanyContext _companyContext;
        private readonly IPdfService _pdfService;

        public CompanyStockReportController(
            ICompanyStockReportService reportService,
            ILookupService lookupService,
            ICompanyContext companyContext,
            IPdfService pdfService)
        {
            _reportService = reportService;
            _lookupService = lookupService;
            _companyContext = companyContext;
            _pdfService = pdfService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            [FromQuery] CompanyStockReportFilterDto filter,
            CancellationToken cancellationToken)
        {
            await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);

            var result = await _reportService.GetReportAsync(filter, cancellationToken);
            var categories = await _lookupService.GetCategoriesAsync(
                _companyContext.HasCompany ? _companyContext.CompanyID : null,
                cancellationToken);

            var viewModel = new CompanyStockReportViewModel
            {
                Filter = filter,
                Rows = result.Rows,
                Summary = result.Summary,
                Categories = categories.ToSelectList(filter.CategoryID),
                ScopeLabel = CompanyScopeGuards.DisplayName(_companyContext),
                IsAllCompanies = !_companyContext.HasCompany
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            [FromQuery] CompanyStockReportFilterDto filter,
            CancellationToken cancellationToken)
        {
            await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);

            var data = await _reportService.GetExportDataAsync(filter, cancellationToken);
            var scopeLabel = CompanyScopeGuards.DisplayName(_companyContext);
            var includeCompany = !_companyContext.HasCompany;
            var bytes = _reportService.GenerateExcel(data, scopeLabel, includeCompany);

            var fileName = $"Company_Stock_Report_{SanitizeFilePart(scopeLabel)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            [FromQuery] CompanyStockReportFilterDto filter,
            CancellationToken cancellationToken)
        {
            await ApplyAmbientCompanyScopeAsync(filter, cancellationToken);

            var data = await _reportService.GetExportDataAsync(filter, cancellationToken);
            var scopeLabel = CompanyScopeGuards.DisplayName(_companyContext);
            var includeCompany = !_companyContext.HasCompany;
            var bytes = _pdfService.GenerateCompanyStockReportPdf(
                data.Rows.Items,
                data.Summary,
                scopeLabel,
                includeCompany);

            var fileName = $"Company_Stock_Report_{SanitizeFilePart(scopeLabel)}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            return File(bytes, "application/pdf", fileName);
        }

        private async Task ApplyAmbientCompanyScopeAsync(
            CompanyStockReportFilterDto filter,
            CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            if (_companyContext.HasCompany)
                filter.CompanyID = _companyContext.CompanyID;
            else
                filter.CompanyID = null;
        }

        private static string SanitizeFilePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "AllCompanies";
            foreach (var c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace(' ', '_');
        }
    }
}
