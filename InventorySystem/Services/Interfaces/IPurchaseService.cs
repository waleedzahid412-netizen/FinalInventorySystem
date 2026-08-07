using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Purchases;

namespace InventorySystem.Services.Interfaces
{
    public interface IPurchaseService
    {
        Task<OperationResult<int>> CreateAndFinalizePurchaseInvoiceAsync(CreatePurchaseInvoiceDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> CanEditPurchaseInvoiceAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default);
        Task<OperationResult<int>> UpdatePurchaseInvoiceAsync(UpdatePurchaseInvoiceDto dto, int userId, CancellationToken cancellationToken = default);
        Task<PagedResult<PurchaseListDto>> GetPagedPurchasesAsync(PurchaseFilterDto filter, CancellationToken cancellationToken = default);
        Task<PurchaseDetailsDto?> GetPurchaseDetailsAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default);
        Task<PagedResult<PurchasePaymentHistoryDto>> GetPaymentHistoryTabAsync(int purchaseInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<PurchaseLedgerEntryDto>> GetLedgerTabAsync(int purchaseInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    }
}
