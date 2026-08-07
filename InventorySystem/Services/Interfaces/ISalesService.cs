using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Sales;

namespace InventorySystem.Services.Interfaces
{
    public interface ISalesService
    {
        Task<OperationResult<int>> CreateAndFinalizeSalesInvoiceAsync(CreateSalesInvoiceDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> CanEditSalesInvoiceAsync(int salesInvoiceId, CancellationToken cancellationToken = default);
        Task<OperationResult<int>> UpdateSalesInvoiceAsync(UpdateSalesInvoiceDto dto, int userId, CancellationToken cancellationToken = default);
        Task<PagedResult<SalesListDto>> GetPagedSalesAsync(SalesFilterDto filter, CancellationToken cancellationToken = default);
        Task<SalesDetailsDto?> GetSalesDetailsAsync(int salesInvoiceId, CancellationToken cancellationToken = default);
        Task<PagedResult<SalesPaymentHistoryDto>> GetPaymentHistoryTabAsync(int salesInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<SalesLedgerEntryDto>> GetLedgerTabAsync(int salesInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    }
}
