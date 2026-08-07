using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Companies;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface ICompanyRepository
    {
        Task<PagedResult<Company>> GetPagedAsync(CompanyFilterDto filter, CancellationToken cancellationToken = default);
        Task<Company?> GetByIdAsync(int companyId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByNameAsync(string name, int? excludeCompanyId = null, CancellationToken cancellationToken = default);
        Task<bool> ExistsByPhoneAsync(string phone, int? excludeCompanyId = null, CancellationToken cancellationToken = default);
        Task<bool> HasHistoricalTransactionsAsync(int companyId, CancellationToken cancellationToken = default);

        Task AddAsync(Company company, CancellationToken cancellationToken = default);
        Task UpdateAsync(Company company, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(int companyId, int userId, CancellationToken cancellationToken = default);

        // Granular financial and history queries for Details screen
        Task<CompanyFinancialSummaryDto?> GetFinancialSummaryAsync(int companyId, CancellationToken cancellationToken = default);
        Task<PagedResult<CompanyPurchaseHistoryDto>> GetPurchaseHistoryAsync(int companyId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<CompanyPaymentHistoryDto>> GetPaymentHistoryAsync(int companyId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<CompanyLedgerEntryDto>> GetLedgerAsync(int companyId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
