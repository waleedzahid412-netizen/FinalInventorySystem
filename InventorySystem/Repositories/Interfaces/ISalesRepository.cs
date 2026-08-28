using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Sales;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface ISalesRepository
    {
        #region 1. Query Operations
        Task<PagedResult<SalesListDto>> GetPagedAsync(SalesFilterDto filter, CancellationToken cancellationToken = default);
        Task<SalesDetailsDto?> GetDetailsByIdAsync(int salesInvoiceId, CancellationToken cancellationToken = default);
        Task<SalesInvoice?> GetByIdAsync(int salesInvoiceId, CancellationToken cancellationToken = default);
        #endregion

        #region 2. Validation / Existence Checks
        Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber, int? excludeId = null, CancellationToken cancellationToken = default);
        Task<Customer?> GetCustomerForInvoiceAsync(int customerId, CancellationToken cancellationToken = default);
        Task<bool> WarehouseExistsAsync(int warehouseId, CancellationToken cancellationToken = default);
        Task<bool> SupplierExistsAsync(int SupplierID, CancellationToken cancellationToken = default);
        Task<bool> CompanyExistsAsync(int companyId, CancellationToken cancellationToken = default);
        Task<bool> BookerExistsAsync(int bookerId, CancellationToken cancellationToken = default);
        Task<int?> GetBookerCompanyIdAsync(int bookerId, CancellationToken cancellationToken = default);
        Task<string?> GetCompanyNameAsync(int companyId, CancellationToken cancellationToken = default);
        Task<bool> SalespersonExistsAsync(int userId, CancellationToken cancellationToken = default);
        Task<int?> GetProductCompanyIdAsync(int productId, CancellationToken cancellationToken = default);
        Task<string?> GetProductNameAsync(int productId, CancellationToken cancellationToken = default);
        Task<bool> ProductExistsAsync(int productId, CancellationToken cancellationToken = default);
        Task<ProductUnit?> GetProductUnitAsync(int productUnitId, CancellationToken cancellationToken = default);
        #endregion

        #region 3. Financial & History Queries
        Task<SalesFinancialSummaryDto?> GetFinancialSummaryAsync(int salesInvoiceId, CancellationToken cancellationToken = default);
        Task<PagedResult<SalesPaymentHistoryDto>> GetPaymentHistoryAsync(int salesInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<SalesLedgerEntryDto>> GetLedgerByInvoiceAsync(int salesInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        #endregion

        #region 4. Write Operations (inside transaction)
        Task AddInvoiceAsync(SalesInvoice invoice, CancellationToken cancellationToken = default);
        Task AddInvoiceItemAsync(SalesInvoiceItem item, CancellationToken cancellationToken = default);
        Task<InventoryStock?> GetInventoryStockTrackedAsync(int productId, int warehouseId, CancellationToken cancellationToken = default);
        Task AddInventoryStockAsync(InventoryStock stock, CancellationToken cancellationToken = default);
        Task AddInventoryTransactionAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default);
        Task AddCustomerLedgerAsync(CustomerLedger ledger, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
        #endregion
    }
}
