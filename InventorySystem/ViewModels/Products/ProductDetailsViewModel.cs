using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Products;

namespace InventorySystem.ViewModels.Products
{
    public class ProductDetailsViewModel
    {
        public ProductDetailsDto Product { get; set; } = new ProductDetailsDto();

        public string ActiveTab { get; set; } = "inventory"; // inventory | purchase | sales | transactions

        public PagedResult<ProductPurchaseHistoryDto> PurchaseHistory { get; set; } = new PagedResult<ProductPurchaseHistoryDto>();
        public PagedResult<ProductSalesHistoryDto> SalesHistory { get; set; } = new PagedResult<ProductSalesHistoryDto>();
        public PagedResult<ProductInventoryTransactionDto> InventoryTransactions { get; set; } = new PagedResult<ProductInventoryTransactionDto>();
    }
}
