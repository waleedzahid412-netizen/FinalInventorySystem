using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Purchases;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface IPurchaseRepository
    {
        #region 1. Query Operations
        Task<PagedResult<PurchaseListDto>> GetPagedAsync(PurchaseFilterDto filter, CancellationToken cancellationToken = default);
        Task<PurchaseDetailsDto?> GetDetailsByIdAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default);
        Task<PurchaseInvoice?> GetByIdAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default);
        #endregion

        #region 2. Validation / Existence Checks
        Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber, int? excludeId = null, CancellationToken cancellationToken = default);
        Task<bool> CompanyExistsAsync(int companyId, CancellationToken cancellationToken = default);
        Task<bool> WarehouseExistsAsync(int warehouseId, CancellationToken cancellationToken = default);
        Task<bool> ProductExistsAsync(int productId, CancellationToken cancellationToken = default);
        Task<ProductUnit?> GetProductUnitAsync(int productUnitId, CancellationToken cancellationToken = default);
        #endregion

        #region 3. Financial Queries (Details screen tabs)
        Task<PurchaseFinancialSummaryDto?> GetFinancialSummaryAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default);
        Task<PagedResult<PurchasePaymentHistoryDto>> GetPaymentHistoryAsync(int purchaseInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<PurchaseLedgerEntryDto>> GetLedgerByInvoiceAsync(int purchaseInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        #endregion

        #region 4. Write Operations (used inside atomic transaction)
        Task AddInvoiceAsync(PurchaseInvoice invoice, CancellationToken cancellationToken = default);
        Task AddInvoiceItemAsync(PurchaseInvoiceItem item, CancellationToken cancellationToken = default);
        Task<InventoryStock?> GetInventoryStockTrackedAsync(int productId, int warehouseId, CancellationToken cancellationToken = default);
        Task AddInventoryStockAsync(InventoryStock stock, CancellationToken cancellationToken = default);
        Task AddInventoryTransactionAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default);
        Task AddCompanyLedgerAsync(CompanyLedger ledger, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
        #endregion
    }
}
