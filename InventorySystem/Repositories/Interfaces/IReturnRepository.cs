using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Returns;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface IReturnRepository
    {
        // ===== SALES RETURNS QUERIES =====
        Task<PagedResult<SalesReturnListDto>> GetPagedSalesReturnsAsync(SalesReturnFilterDto filter, CancellationToken cancellationToken = default);
        Task<SalesReturnDetailsDto?> GetSalesReturnDetailsAsync(int salesReturnId, CancellationToken cancellationToken = default);
        Task<SalesInvoice?> GetSalesInvoiceForReturnAsync(int salesInvoiceId, CancellationToken cancellationToken = default);
        Task<List<SalesReturnItem>> GetPriorSalesReturnItemsAsync(int salesInvoiceId, CancellationToken cancellationToken = default);
        Task<string> GenerateSalesReturnNumberAsync(CancellationToken cancellationToken = default);

        // ===== PURCHASE RETURNS QUERIES =====
        Task<PagedResult<PurchaseReturnListDto>> GetPagedPurchaseReturnsAsync(PurchaseReturnFilterDto filter, CancellationToken cancellationToken = default);
        Task<PurchaseReturnDetailsDto?> GetPurchaseReturnDetailsAsync(int purchaseReturnId, CancellationToken cancellationToken = default);
        Task<PurchaseInvoice?> GetPurchaseInvoiceForReturnAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default);
        Task<List<PurchaseReturnItem>> GetPriorPurchaseReturnItemsAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default);
        Task<string> GeneratePurchaseReturnNumberAsync(CancellationToken cancellationToken = default);

        // ===== WRITE OPERATIONS =====
        Task AddSalesReturnAsync(SalesReturn salesReturn, CancellationToken cancellationToken = default);
        Task AddPurchaseReturnAsync(PurchaseReturn purchaseReturn, CancellationToken cancellationToken = default);
        Task<InventoryStock?> GetInventoryStockTrackedAsync(int productId, int warehouseId, CancellationToken cancellationToken = default);
        Task AddInventoryStockAsync(InventoryStock stock, CancellationToken cancellationToken = default);
        Task AddInventoryTransactionAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default);
        Task AddCustomerLedgerAsync(CustomerLedger ledger, CancellationToken cancellationToken = default);
        Task AddCompanyLedgerAsync(CompanyLedger ledger, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
