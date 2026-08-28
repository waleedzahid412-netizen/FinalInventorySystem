using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Reports;

namespace InventorySystem.ViewModels.Reports
{
    public class CompanyStockReportViewModel
    {
        public CompanyStockReportFilterDto Filter { get; set; } = new();
        public PagedResult<CompanyStockReportRowDto> Rows { get; set; } = new();
        public CompanyStockReportSummaryDto Summary { get; set; } = new();

        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> StockStatusOptions { get; set; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "All Stock Statuses" },
            new SelectListItem { Value = "InStock", Text = "In Stock" },
            new SelectListItem { Value = "LowStock", Text = "Low Stock" },
            new SelectListItem { Value = "OutOfStock", Text = "Out of Stock" }
        };

        public string ScopeLabel { get; set; } = "All Companies";
        public bool IsAllCompanies { get; set; }
    }
}
