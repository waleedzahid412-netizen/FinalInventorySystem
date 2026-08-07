using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Returns;

namespace InventorySystem.Services.Interfaces
{
    public interface IReturnService
    {
        // ===== SALES RETURNS =====
        Task<OperationResult<SalesReturnEligibilityDto>> GetSalesReturnEligibilityAsync(int salesInvoiceId, CancellationToken cancellationToken = default);
        Task<OperationResult<ClawbackPreviewDto>> PreviewSalesClawbackAsync(PreviewReturnRequest request, CancellationToken cancellationToken = default);
        Task<OperationResult<int>> ProcessSalesReturnAsync(ProcessSalesReturnRequest request, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult<int>> ProcessManualSalesReturnAsync(ProcessManualSalesReturnRequest request, int userId, CancellationToken cancellationToken = default);
        Task<PagedResult<SalesReturnListDto>> GetPagedSalesReturnsAsync(SalesReturnFilterDto filter, CancellationToken cancellationToken = default);
        Task<SalesReturnDetailsDto?> GetSalesReturnDetailsAsync(int salesReturnId, CancellationToken cancellationToken = default);

        // ===== PURCHASE RETURNS =====
        Task<OperationResult<PurchaseReturnEligibilityDto>> GetPurchaseReturnEligibilityAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default);
        Task<OperationResult<int>> ProcessPurchaseReturnAsync(ProcessPurchaseReturnRequest request, int userId, CancellationToken cancellationToken = default);
        Task<PagedResult<PurchaseReturnListDto>> GetPagedPurchaseReturnsAsync(PurchaseReturnFilterDto filter, CancellationToken cancellationToken = default);
        Task<PurchaseReturnDetailsDto?> GetPurchaseReturnDetailsAsync(int purchaseReturnId, CancellationToken cancellationToken = default);
    }
}
