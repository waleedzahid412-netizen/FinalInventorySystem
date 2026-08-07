using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Customers;

namespace InventorySystem.Services.Interfaces
{
    public interface ICustomerService
    {
        Task<PagedResult<CustomerListDto>> GetPagedCustomersAsync(CustomerFilterDto filter, CancellationToken cancellationToken = default);
        Task<CustomerInfoDto?> GetCustomerByIdAsync(int customerId, CancellationToken cancellationToken = default);
        Task<EditCustomerDto?> GetCustomerForEditAsync(int customerId, CancellationToken cancellationToken = default);
        Task<CustomerDetailsDto?> GetCustomerDetailsAsync(int customerId, CancellationToken cancellationToken = default);

        Task<OperationResult<int>> CreateCustomerAsync(CreateCustomerDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateCustomerAsync(EditCustomerDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> SoftDeleteCustomerAsync(int customerId, int userId, CancellationToken cancellationToken = default);

        Task<bool> ValidateShopNameAsync(string shopName, int? excludeCustomerId = null, CancellationToken cancellationToken = default);
        Task<bool> ValidatePhoneAsync(string phone, int? excludeCustomerId = null, CancellationToken cancellationToken = default);

        // Async Tab History endpoints
        Task<PagedResult<CustomerSalesHistoryDto>> GetSalesHistoryAsync(int customerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<CustomerPaymentHistoryDto>> GetPaymentHistoryAsync(int customerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<CustomerLedgerEntryDto>> GetLedgerAsync(int customerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    }
}
