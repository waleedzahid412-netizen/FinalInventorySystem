using System.Collections.Generic;
using InventorySystem.DTOs.Common;

namespace InventorySystem.DTOs.Reports
{
    public class CompanyStockReportFilterDto
    {
        public int? CompanyID { get; set; }
        public string? SearchTerm { get; set; }
        public int? CategoryID { get; set; }

        /// <summary>
        /// Empty / null = All; InStock | LowStock | OutOfStock
        /// </summary>
        public string? StockStatus { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class CompanyStockReportRowDto
    {
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public string BaseUnitName { get; set; } = "Units";
        public decimal CurrentStock { get; set; }
        public int ReorderLevel { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class CompanyStockReportSummaryDto
    {
        public int TotalProducts { get; set; }
        public decimal TotalStockQuantity { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
    }

    public class CompanyStockReportResultDto
    {
        public PagedResult<CompanyStockReportRowDto> Rows { get; set; } = new();
        public CompanyStockReportSummaryDto Summary { get; set; } = new();
    }
}
