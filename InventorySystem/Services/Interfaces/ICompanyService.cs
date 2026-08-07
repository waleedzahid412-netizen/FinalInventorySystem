using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Companies;

namespace InventorySystem.Services.Interfaces
{
    public interface ICompanyService
    {
        Task<PagedResult<CompanyListItemDto>> GetPagedCompaniesAsync(CompanyFilterDto filter, CancellationToken cancellationToken = default);
        Task<CompanyDetailsDto?> GetCompanyDetailsAsync(int companyId, CancellationToken cancellationToken = default);
        Task<EditCompanyDto?> GetCompanyForEditAsync(int companyId, CancellationToken cancellationToken = default);

        Task<OperationResult<int>> CreateCompanyAsync(CreateCompanyDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateCompanyAsync(EditCompanyDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> SoftDeleteCompanyAsync(int companyId, int userId, CancellationToken cancellationToken = default);

        Task<bool> ValidateCompanyNameAsync(string name, int? excludeCompanyId = null, CancellationToken cancellationToken = default);
        Task<bool> ValidatePhoneAsync(string phone, int? excludeCompanyId = null, CancellationToken cancellationToken = default);

        Task<PagedResult<CompanyPurchaseHistoryDto>> GetPurchaseHistoryAsync(int companyId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<CompanyPaymentHistoryDto>> GetPaymentHistoryAsync(int companyId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<CompanyLedgerEntryDto>> GetLedgerAsync(int companyId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    }
}
