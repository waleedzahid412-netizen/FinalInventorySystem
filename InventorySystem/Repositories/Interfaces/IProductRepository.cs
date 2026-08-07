using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Products;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface IProductRepository
    {
        Task<PagedResult<Product>> GetPagedAsync(ProductFilterDto filter, CancellationToken cancellationToken = default);
        Task<Product?> GetByIdWithUnitsAsync(int productId, CancellationToken cancellationToken = default);
        Task<bool> IsSkuUniqueAsync(string sku, int? excludeProductId = null, CancellationToken cancellationToken = default);
        Task<bool> IsBarcodeUniqueAsync(string barcode, int? excludeProductId = null, CancellationToken cancellationToken = default);
        Task AddAsync(Product product, CancellationToken cancellationToken = default);
        Task UpdateAsync(Product product, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(int productId, int userId, CancellationToken cancellationToken = default);

        // Granular query methods for Product Details screen
        Task<InventorySummaryDto?> GetInventorySummaryAsync(int productId, CancellationToken cancellationToken = default);
        Task<List<WarehouseStockSummaryDto>> GetWarehouseStocksAsync(int productId, CancellationToken cancellationToken = default);
        Task<PagedResult<ProductPurchaseHistoryDto>> GetPurchaseHistoryAsync(int productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<ProductSalesHistoryDto>> GetSalesHistoryAsync(int productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<ProductInventoryTransactionDto>> GetInventoryTransactionsAsync(int productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
