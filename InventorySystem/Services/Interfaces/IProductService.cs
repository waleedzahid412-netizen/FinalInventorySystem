using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Products;

namespace InventorySystem.Services.Interfaces
{
    public interface IProductService
    {
        Task<PagedResult<ProductListItemDto>> GetPagedProductsAsync(ProductFilterDto filter, CancellationToken cancellationToken = default);
        Task<ProductDetailsDto?> GetProductDetailsAsync(int productId, CancellationToken cancellationToken = default);
        Task<EditProductDto?> GetProductForEditAsync(int productId, CancellationToken cancellationToken = default);
        Task<OperationResult<int>> CreateProductAsync(CreateProductDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateProductAsync(EditProductDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> SoftDeleteProductAsync(int productId, int userId, CancellationToken cancellationToken = default);

        Task<bool> ValidateSkuAsync(string sku, int? excludeProductId = null, CancellationToken cancellationToken = default);
        Task<bool> ValidateBarcodeAsync(string barcode, int? excludeProductId = null, CancellationToken cancellationToken = default);

        Task<PagedResult<ProductPurchaseHistoryDto>> GetPurchaseHistoryAsync(int productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<ProductSalesHistoryDto>> GetSalesHistoryAsync(int productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<ProductInventoryTransactionDto>> GetInventoryTransactionsAsync(int productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    }
}
