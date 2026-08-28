using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Reports;

namespace InventorySystem.Services.Interfaces
{
    public interface ICompanyStockReportService
    {
        Task<CompanyStockReportResultDto> GetReportAsync(
            CompanyStockReportFilterDto filter,
            CancellationToken cancellationToken = default);

        /// <summary>All filtered rows (no paging) for Excel/PDF export.</summary>
        Task<CompanyStockReportResultDto> GetExportDataAsync(
            CompanyStockReportFilterDto filter,
            CancellationToken cancellationToken = default);

        byte[] GenerateExcel(CompanyStockReportResultDto data, string scopeLabel, bool includeCompanyColumn);
    }
}
