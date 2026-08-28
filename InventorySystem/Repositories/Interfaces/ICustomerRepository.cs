using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Customers;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface ICustomerRepository
    {
        Task<PagedResult<Customer>> GetPagedAsync(CustomerFilterDto filter, CancellationToken cancellationToken = default);
        Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByShopNameAsync(string shopName, int? excludeCustomerId = null, CancellationToken cancellationToken = default);
        Task<bool> ExistsByPhoneAsync(string phone, int? excludeCustomerId = null, CancellationToken cancellationToken = default);
        Task<bool> HasHistoricalTransactionsAsync(int customerId, CancellationToken cancellationToken = default);

        Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
        Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(int customerId, int userId, CancellationToken cancellationToken = default);

        // Financial & History queries (optional companyId soft-scopes via SalesInvoice.CompanyID; null = all companies)
        Task<CustomerFinancialSummaryDto?> GetFinancialSummaryAsync(int customerId, int? companyId = null, CancellationToken cancellationToken = default);
        Task<PagedResult<CustomerSalesHistoryDto>> GetSalesHistoryAsync(int customerId, int pageNumber, int pageSize, int? companyId = null, CancellationToken cancellationToken = default);
        Task<PagedResult<CustomerPaymentHistoryDto>> GetPaymentHistoryAsync(int customerId, int pageNumber, int pageSize, int? companyId = null, CancellationToken cancellationToken = default);
        Task<PagedResult<CustomerLedgerEntryDto>> GetLedgerAsync(int customerId, int pageNumber, int pageSize, int? companyId = null, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
