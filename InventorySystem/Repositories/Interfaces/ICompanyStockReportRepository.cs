using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Reports;

namespace InventorySystem.Repositories.Interfaces
{
    public interface ICompanyStockReportRepository
    {
        Task<CompanyStockReportResultDto> GetPagedAsync(
            CompanyStockReportFilterDto filter,
            CancellationToken cancellationToken = default);
    }
}
