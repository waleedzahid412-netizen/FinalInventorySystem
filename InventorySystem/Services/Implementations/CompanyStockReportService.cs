using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using InventorySystem.DTOs.Reports;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class CompanyStockReportService : ICompanyStockReportService
    {
        private readonly ICompanyStockReportRepository _repository;

        public CompanyStockReportService(ICompanyStockReportRepository repository)
        {
            _repository = repository;
        }

        public async Task<CompanyStockReportResultDto> GetReportAsync(
            CompanyStockReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var result = await _repository.GetPagedAsync(filter, cancellationToken);
            ApplyStatuses(result);
            return result;
        }

        public async Task<CompanyStockReportResultDto> GetExportDataAsync(
            CompanyStockReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var exportFilter = new CompanyStockReportFilterDto
            {
                CompanyID = filter.CompanyID,
                SearchTerm = filter.SearchTerm,
                CategoryID = filter.CategoryID,
                StockStatus = filter.StockStatus,
                PageNumber = 1,
                PageSize = int.MaxValue
            };

            var result = await _repository.GetPagedAsync(exportFilter, cancellationToken);
            ApplyStatuses(result);
            return result;
        }

        public byte[] GenerateExcel(
            CompanyStockReportResultDto data,
            string scopeLabel,
            bool includeCompanyColumn)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Company Stock");

            sheet.Cell(1, 1).Value = "Company Stock Report";
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 14;

            sheet.Cell(2, 1).Value = $"Scope: {scopeLabel}";
            sheet.Cell(3, 1).Value = $"Generated: {DateTime.Now:dd MMM yyyy HH:mm}";
            sheet.Cell(4, 1).Value =
                $"Products: {data.Summary.TotalProducts} | Total Stock: {data.Summary.TotalStockQuantity:N0} | Low: {data.Summary.LowStockCount} | Out: {data.Summary.OutOfStockCount}";

            var headerRow = 6;
            var col = 1;
            if (includeCompanyColumn)
                sheet.Cell(headerRow, col++).Value = "Company";
            sheet.Cell(headerRow, col++).Value = "Product Name";
            sheet.Cell(headerRow, col++).Value = "SKU";
            sheet.Cell(headerRow, col++).Value = "Base Unit";
            sheet.Cell(headerRow, col++).Value = "Current Stock";
            sheet.Cell(headerRow, col++).Value = "Reorder Level";
            sheet.Cell(headerRow, col).Value = "Status";

            var headerRange = sheet.Range(headerRow, 1, headerRow, col);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            var rowIndex = headerRow + 1;
            foreach (var row in data.Rows.Items)
            {
                col = 1;
                if (includeCompanyColumn)
                    sheet.Cell(rowIndex, col++).Value = row.CompanyName;
                sheet.Cell(rowIndex, col++).Value = row.ProductName;
                sheet.Cell(rowIndex, col++).Value = row.SKU ?? string.Empty;
                sheet.Cell(rowIndex, col++).Value = row.BaseUnitName;
                sheet.Cell(rowIndex, col++).Value = row.CurrentStock;
                sheet.Cell(rowIndex, col++).Value = row.ReorderLevel;
                sheet.Cell(rowIndex, col).Value = row.Status;
                rowIndex++;
            }

            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static void ApplyStatuses(CompanyStockReportResultDto result)
        {
            foreach (var row in result.Rows.Items)
            {
                row.Status = ResolveStatus(row.CurrentStock, row.ReorderLevel);
            }
        }

        internal static string ResolveStatus(decimal currentStock, int reorderLevel)
        {
            if (currentStock <= 0) return "Out of Stock";
            if (currentStock <= reorderLevel) return "Low Stock";
            return "In Stock";
        }
    }
}
