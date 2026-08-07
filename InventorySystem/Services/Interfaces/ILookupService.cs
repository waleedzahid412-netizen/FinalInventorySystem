using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Products;

namespace InventorySystem.Services.Interfaces
{
    public interface ILookupService
    {
        Task<List<LookupItemDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
        Task<List<LookupItemDto>> GetUnitsAsync(CancellationToken cancellationToken = default);
        Task<List<LookupItemDto>> GetCompaniesAsync(CancellationToken cancellationToken = default);
        Task<List<LookupItemDto>> GetCustomersAsync(CancellationToken cancellationToken = default);
        Task<List<LookupItemDto>> GetAreasAsync(CancellationToken cancellationToken = default);
        Task<List<LookupItemDto>> GetSubAreasAsync(int? areaId = null, CancellationToken cancellationToken = default);
        Task<ProductFormDropdownsDto> GetProductFormDropdownsAsync(CancellationToken cancellationToken = default);

        // ===== PURCHASE & SALES LOOKUPS =====
        Task<List<LookupItemDto>> GetWarehousesAsync(CancellationToken cancellationToken = default);
        Task<List<LookupItemDto>> GetProductsByCompanyAsync(int companyId, CancellationToken cancellationToken = default);
        Task<List<DTOs.Purchases.ProductUnitLookupDto>> GetProductUnitsAsync(int productId, CancellationToken cancellationToken = default);
        Task<DTOs.Purchases.ProductInfoDto?> GetProductInfoAsync(int productId, CancellationToken cancellationToken = default);

        // ===== SALES LOOKUPS =====
        Task<List<LookupItemDto>> GetProductsAsync(CancellationToken cancellationToken = default);
        Task<List<LookupItemDto>> GetDeliveryPersonsAsync(CancellationToken cancellationToken = default);
        Task<DTOs.Sales.CustomerInfoDto?> GetCustomerInfoAsync(int customerId, CancellationToken cancellationToken = default);
        Task<DTOs.Sales.ProductUnitPriceDto?> GetProductUnitPriceAsync(int productId, int productUnitId, CancellationToken cancellationToken = default);
        Task<DTOs.Sales.AvailableStockDto> GetAvailableStockAsync(int productId, int warehouseId, int productUnitId, CancellationToken cancellationToken = default);
    }
}
